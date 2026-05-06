# test-migration.ps1
# Strangler Fig Pattern Migration Test Script
# Uses Node.js LegacyApi + .NET ModernApi + .NET Proxy

$ErrorActionPreference = "Continue"
$ProxyUrl = "http://localhost:5000"
$ProjectRoot = $PSScriptRoot
$ProxyAppSettings = Join-Path $ProjectRoot "Proxy\appsettings.json"

# Track processes started by this script for cleanup
$script:LegacyApiProcess = $null

# ============================================
# Utility: Kill processes on a given port
# ============================================
function Stop-PortProcess {
    param([int]$Port)

    $procIds = @()
    $lines = netstat -ano | findstr ":$Port " | findstr "LISTENING"
    foreach ($line in $lines) {
        $parts = $line.Trim() -split '\s+'
        $id = [int]$parts[-1]
        if ($id -notin $procIds -and $id -gt 0) {
            $procIds += $id
        }
    }
    if ($procIds.Count -eq 0) { return }

    foreach ($id in $procIds) {
        taskkill /F /PID $id /T 2>$null | Out-Null
    }

    for ($i = 0; $i -lt 10; $i++) {
        $still = netstat -ano | findstr ":$Port " | findstr "LISTENING"
        if (-not $still) { return }
        Start-Sleep -Seconds 1
    }
    Write-Host "  WARNING: Port $Port still in use after 10s" -ForegroundColor Yellow
}

# ============================================
# Config: Set migration phase in appsettings.json
# ============================================
function Set-MigrationPhase {
    param([string]$Phase)
    $content = [System.IO.File]::ReadAllText($ProxyAppSettings)
    $content = $content -replace '"CurrentPhase":\s*"[^"]*"', "`"CurrentPhase`": `"$Phase`""
    [System.IO.File]::WriteAllText($ProxyAppSettings, $content, [System.Text.UTF8Encoding]::new($false))
}

# ============================================
# Node.js LegacyApi management
# ============================================
function Start-LegacyApiNode {
    $nodeDir = Join-Path $ProjectRoot "LegacyApiNode"

    # Check if already running
    try {
        Invoke-WebRequest -Uri "http://localhost:5001/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop | Out-Null
        Write-Host "  LegacyApi (Node.js) :5001 - already running" -ForegroundColor Green
        return
    } catch {}

    # Kill stale process on port 5001
    Stop-PortProcess 5001

    # Start Node.js LegacyApi
    $script:LegacyApiProcess = Start-Process -FilePath "node" -ArgumentList "src/index.js" -WorkingDirectory $nodeDir -PassThru -WindowStyle Hidden

    # Wait for ready
    for ($i = 0; $i -lt 15; $i++) {
        try {
            Invoke-WebRequest -Uri "http://localhost:5001/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop | Out-Null
            Write-Host "  LegacyApi (Node.js) :5001 - started" -ForegroundColor Green
            return
        } catch {}
        Start-Sleep -Seconds 1
    }

    Write-Host "  ERROR: LegacyApi (Node.js) failed to start on port 5001" -ForegroundColor Red
    exit 1
}

function Stop-LegacyApiNode {
    if ($script:LegacyApiProcess -and -not $script:LegacyApiProcess.HasExited) {
        Stop-Process -Id $script:LegacyApiProcess.Id -Force -ErrorAction SilentlyContinue
        Write-Host "  LegacyApi (Node.js) stopped" -ForegroundColor Gray
    }
}

# ============================================
# Proxy management
# ============================================
function Restart-Proxy {
    param([string]$Phase)

    Write-Host "  Switching to $Phase..." -ForegroundColor Gray
    Set-MigrationPhase $Phase
    Stop-PortProcess 5000

    Start-Process -FilePath "dotnet" -ArgumentList "run --project Proxy --no-build" -WorkingDirectory $ProjectRoot -WindowStyle Hidden

    for ($i = 0; $i -lt 15; $i++) {
        try {
            Invoke-WebRequest -Uri "$ProxyUrl/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop | Out-Null
            Write-Host "  Proxy ready on port 5000`n" -ForegroundColor Green
            return
        } catch {}
        Start-Sleep -Seconds 1
    }

    Write-Host "  ERROR: Proxy failed to start within 30s" -ForegroundColor Red
    exit 1
}

# ============================================
# Endpoint tester
# ============================================
function Test-Endpoint {
    param(
        [string]$Method,
        [string]$Endpoint,
        [string]$Description,
        [string]$Body = $null,
        [ValidateSet("LegacyApi (Node.js)", "ModernApi (.NET)")]
        [string]$ServedBy = "LegacyApi (Node.js)"
    )

    Write-Host "[$Description]" -ForegroundColor Yellow
    Write-Host "$Method $Endpoint" -ForegroundColor Gray
    if ($ServedBy -eq "ModernApi (.NET)") {
        Write-Host "  Served: $ServedBy" -ForegroundColor Cyan
    } else {
        Write-Host "  Served: $ServedBy" -ForegroundColor Green
    }

    try {
        $params = @{
            Uri             = "$ProxyUrl$Endpoint"
            Method          = $Method
            UseBasicParsing = $true
            ErrorAction     = "Stop"
        }

        if ($Method -in "POST", "PUT") {
            if (-not $Body) {
                $Body = @{name = "Test User"; email = "test@example.com" } | ConvertTo-Json
            }
            $params.Body = $Body
            $params.ContentType = "application/json"
        }

        try {
            $response = Invoke-WebRequest @params
        }
        catch [System.Net.WebException] {
            $response = $_.Exception.Response
            if (-not $response) { throw }
            $stream = $response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $respBody = $reader.ReadToEnd()
            $statusCode = [int]$response.StatusCode

            $source = $response.Headers["X-API-Source"]
            $phase = $response.Headers["X-Proxy-Phase"]

            Write-Host "  Status: $statusCode" -ForegroundColor Yellow
            Write-Host "  Source: $source" -ForegroundColor $(if ($source -eq "modern") { "Cyan" } else { "Magenta" })
            Write-Host "  Phase:  $phase" -ForegroundColor Gray
            $servedByHeader = $response.Headers["X-Served-By"]
            if ($servedByHeader) { Write-Host "  From:   $servedByHeader" -ForegroundColor Gray }
            $content = $respBody | ConvertFrom-Json
            if ($content.error) { Write-Host "  Error:  $($content.error)" -ForegroundColor Yellow }
            if ($content.message) { Write-Host "  Msg:    $($content.message)" -ForegroundColor Gray }
            return
        }

        $source = $response.Headers["X-API-Source"]
        $version = $response.Headers["X-API-Version"]
        $phase = $response.Headers["X-Proxy-Phase"]
        $time = $response.Headers["X-Response-Time"]
        $servedByHeader = $response.Headers["X-Served-By"]

        Write-Host "  Status: $($response.StatusCode)" -ForegroundColor Green
        if ($source -eq "modern") {
            Write-Host "  Source: $source (v$version)" -ForegroundColor Cyan
        }
        else {
            Write-Host "  Source: $source (v$version)" -ForegroundColor Magenta
        }
        Write-Host "  Phase:  $phase" -ForegroundColor Gray
        Write-Host "  Time:   $time" -ForegroundColor Gray
        if ($servedByHeader) { Write-Host "  From:   $servedByHeader" -ForegroundColor Gray }

        $content = $response.Content | ConvertFrom-Json
        if ($content.data) {
            Write-Host "  Data:   $($content.data | ConvertTo-Json -Compress)" -ForegroundColor Gray
        }
        if ($content.message) {
            Write-Host "  Msg:    $($content.message)" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "  Error:  $($_.Exception.Message)" -ForegroundColor Red
    }
}

# ============================================
# Prerequisites
# ============================================
Write-Host "`nChecking prerequisites..." -ForegroundColor White

# Start Node.js LegacyApi (auto-managed)
Start-LegacyApiNode

# Check ModernApi (.NET, must be started manually)
$modernReady = $false
try { Invoke-WebRequest -Uri "http://localhost:5002/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop | Out-Null; $modernReady = $true } catch {}
if (-not $modernReady) {
    Write-Host "  ERROR: ModernApi not running on port 5002. Run: dotnet run --project ModernApi" -ForegroundColor Red
    Stop-LegacyApiNode
    exit 1
}
Write-Host "  ModernApi (.NET)  :5002 - OK" -ForegroundColor Green

# Build Proxy once
Write-Host "  Building Proxy..." -ForegroundColor Gray -NoNewline
dotnet build (Join-Path $ProjectRoot "Proxy\Proxy.csproj") --verbosity quiet 2>$null | Out-Null
Write-Host " OK" -ForegroundColor Green

# Save original config
$originalConfig = [System.IO.File]::ReadAllText($ProxyAppSettings)

# ============================================
# Phase 1: Full Legacy
# ============================================
Write-Host "`n"
Write-Host ("=" * 60) -ForegroundColor White
Write-Host "PHASE 1: Full Legacy (All traffic to Node.js LegacyApi)" -ForegroundColor White
Write-Host ("=" * 60) -ForegroundColor White

Restart-Proxy "Phase1-FullLegacy"

Test-Endpoint "GET" "/health" "Health Check" -ServedBy "LegacyApi (Node.js)"
Test-Endpoint "GET" "/api/users" "Get All Users" -ServedBy "LegacyApi (Node.js)"
Test-Endpoint "GET" "/api/users/1" "Get User by ID (1)" -ServedBy "LegacyApi (Node.js)"
Test-Endpoint "POST" "/api/users/create" "Create User" -ServedBy "LegacyApi (Node.js)"
Test-Endpoint "PUT" "/api/users/1" "Update User (1)" -ServedBy "LegacyApi (Node.js)"
Test-Endpoint "GET" "/api/products" "Products List" -ServedBy "LegacyApi (Node.js)"

# ============================================
# Phase 2: Migrate GET /api/users
# ============================================
Write-Host "`n"
Write-Host ("=" * 60) -ForegroundColor White
Write-Host "PHASE 2: Migrate GET /api/users (User list migration)" -ForegroundColor White
Write-Host ("=" * 60) -ForegroundColor White

Restart-Proxy "Phase2-MigrateGetUsers"

Test-Endpoint "GET" "/api/users" "Get All Users" -ServedBy "ModernApi (.NET)"
Test-Endpoint "GET" "/api/users/1" "Get User by ID (1)" -ServedBy "LegacyApi (Node.js)"
Test-Endpoint "POST" "/api/users/create" "Create User" -ServedBy "LegacyApi (Node.js)"
Test-Endpoint "GET" "/api/products" "Products List" -ServedBy "LegacyApi (Node.js)"

# ============================================
# Phase 3: All User Endpoints Migrated
# ============================================
Write-Host "`n"
Write-Host ("=" * 60) -ForegroundColor White
Write-Host "PHASE 3: All User Endpoints Migrated" -ForegroundColor White
Write-Host ("=" * 60) -ForegroundColor White

Restart-Proxy "Phase3-MigrateAllUsers"

Test-Endpoint "GET" "/api/users" "Get All Users" -ServedBy "ModernApi (.NET)"
Test-Endpoint "GET" "/api/users/1" "Get User by ID (1)" -ServedBy "ModernApi (.NET)"
Test-Endpoint "POST" "/api/users" "Create User" -ServedBy "ModernApi (.NET)"
Test-Endpoint "PUT" "/api/users/1" "Update User (1)" -ServedBy "ModernApi (.NET)"
Test-Endpoint "DELETE" "/api/users/999" "Delete User (not found)" -ServedBy "ModernApi (.NET)"
Test-Endpoint "GET" "/api/products" "Products List" -ServedBy "LegacyApi (Node.js)"

# ============================================
# Cleanup & Summary
# ============================================
[System.IO.File]::WriteAllText($ProxyAppSettings, $originalConfig, [System.Text.UTF8Encoding]::new($false))
Write-Host "Config restored." -ForegroundColor Gray

Stop-PortProcess 5000
Write-Host "Proxy stopped." -ForegroundColor Gray
Stop-LegacyApiNode

Write-Host "`n"
Write-Host ("=" * 60) -ForegroundColor White
Write-Host "Migration Demo Complete!" -ForegroundColor Green
Write-Host ("=" * 60) -ForegroundColor White
Write-Host "`nKey observations:"
Write-Host "  - Phase 1: All responses from LegacyApi (Node.js)" -ForegroundColor Magenta
Write-Host "  - Phase 2: GET /api/users from Modern (.NET), others from Legacy (Node.js)" -ForegroundColor Yellow
Write-Host "  - Phase 3: All /api/users/* from Modern (.NET), /api/products from Legacy (Node.js)" -ForegroundColor Cyan
Write-Host "`nStack: LegacyApi (Node.js/Express) + ModernApi (.NET 10) + Proxy (YARP)" -ForegroundColor Gray
Write-Host "The Strangler Fig pattern enables cross-platform gradual migration.`n" -ForegroundColor Gray
