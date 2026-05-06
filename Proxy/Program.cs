using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var currentPhase = builder.Configuration.GetValue<string>("Migration:CurrentPhase") 
    ?? "Phase1";
var phaseConfigFile = $"appsettings.{currentPhase}.json";
var phaseConfigPath = Path.Combine(builder.Environment.ContentRootPath, phaseConfigFile);

// 检查配置文件是否存在
if (!File.Exists(phaseConfigPath))
{
    Console.WriteLine($"========================================");
    Console.WriteLine($"ERROR: Configuration file not found!");
    Console.WriteLine($"Expected: {phaseConfigPath}");
    Console.WriteLine($"");
    Console.WriteLine($"Available phases:");
    Console.WriteLine($"  - Phase1-FullLegacy");
    Console.WriteLine($"  - Phase2-MigrateGetUsers");
    Console.WriteLine($"  - Phase3-MigrateAllUsers");
    Console.WriteLine($"");
    Console.WriteLine($"Update 'Migration:CurrentPhase' in appsettings.json");
    Console.WriteLine($"========================================");
    return;
}

Console.WriteLine($"========================================");
Console.WriteLine($"  Migration Phase: {currentPhase}");
Console.WriteLine($"  Config File: {phaseConfigFile}");
Console.WriteLine($"========================================");

builder.Configuration.AddJsonFile(phaseConfigFile, optional: false, reloadOnChange: true);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("X-API-Source", "X-API-Version", "X-Proxy-Phase", "X-RequestId", "X-Response-Time", "X-Served-By");
    });
});

var app = builder.Build();

app.UseCors();

app.Use(async (context, next) =>
{
    var startTime = DateTime.UtcNow;
    var requestId = Guid.NewGuid().ToString("N")[..8];

    // 存储到 HttpContext.Items 以便后续使用
    context.Items["RequestId"] = requestId;
    context.Items["StartTime"] = startTime;
    context.Items["Phase"] = currentPhase;

    app.Logger.LogInformation(
        "[{RequestId}] {Method} {Path} started",
        requestId,
        context.Request.Method,
        context.Request.Path
    );

    // 在响应开始前设置自定义头
    context.Response.OnStarting(() =>
    {
        var elapsed = DateTime.UtcNow - startTime;
        var apiSource = context.Response.Headers["X-API-Source"].FirstOrDefault() ?? "unknown";
        var apiVersion = context.Response.Headers["X-API-Version"].FirstOrDefault() ?? "unknown";
        var statusCode = context.Response.StatusCode;

        // 记录到日志
        app.Logger.LogInformation(
            "[{RequestId}] {Method} {Path} => {StatusCode} ({ApiSource}/{ApiVersion}) [{Elapsed}ms]",
            requestId,
            context.Request.Method,
            context.Request.Path,
            statusCode,
            apiSource,
            apiVersion,
            elapsed.TotalMilliseconds.ToString("F2")
        );

        // 设置响应头
        context.Response.Headers["X-Proxy-Phase"] = currentPhase;
        context.Response.Headers["X-RequestId"] = requestId;
        context.Response.Headers["X-Response-Time"] = $"{elapsed.TotalMilliseconds:F2}ms";

        return Task.CompletedTask;
    });

    await next();
});

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.MapReverseProxy();

app.Run("http://localhost:5000");


