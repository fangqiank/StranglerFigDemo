# Strangler Fig Pattern Demo

A cross-platform demo of the **Strangler Fig Pattern** — gradually migrating a legacy Node.js/Express API to a modern .NET 10 API behind a YARP reverse proxy, with zero downtime.

## Architecture

![Architecture](strangler-fig-architecture.svg)

```
Client :5000 → Proxy (YARP) ──┬→ LegacyApiNode :5001  (Node.js + Express + Prisma + SQLite)
                               └→ ModernApi :5002     (.NET 10 + Dapper + SQLite)
```

| Component | Stack | Port | Role |
|-----------|-------|------|------|
| **Proxy** | .NET 10 + YARP | `:5000` | Reverse proxy, config-driven routing, phase management |
| **LegacyApiNode** | Node.js + Express + Prisma | `:5001` | Legacy system with snake_case fields, SQLite persistence |
| **ModernApi** | .NET 10 Minimal APIs + Dapper | `:5002` | Modern system with PascalCase, DI, validation, SQLite persistence |

## How It Works

The proxy uses YARP route configs to decide which backend handles each request. A `CurrentPhase` setting in `appsettings.json` selects the active phase config at startup.

### Migration Phases

| Phase | Config | Routing |
|-------|--------|---------|
| **Phase 1** | `Phase1-FullLegacy` | All requests → LegacyApiNode (Node.js) |
| **Phase 2** | `Phase2-MigrateGetUsers` | `GET /api/users` → ModernApi (.NET), rest → LegacyApiNode |
| **Phase 3** | `Phase3-MigrateAllUsers` | All `/api/users/*` → ModernApi (.NET), rest → LegacyApiNode |

### X-Served-By Header

Each backend returns an `X-Served-By` response header, so you can identify which system served the request:

```
X-Served-By: Node.js/Express (LegacyApi)
X-Served-By: C#/.NET 10 (ModernApi)
```

Additional response headers: `X-API-Source` (legacy/modern), `X-API-Version`, `X-Proxy-Phase`, `X-RequestId`, `X-Response-Time`.

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) 18+

### Run the Demo

**Terminal 1** — Start ModernApi (.NET):

```bash
dotnet run --project ModernApi
```

**Terminal 2** — Run the test script (auto-starts LegacyApiNode + Proxy):

```powershell
.\test-migration.ps1
```

The script will:
1. Start the Node.js LegacyApiNode on port 5001
2. Build the Proxy project
3. Run through all three phases, restarting the proxy between each
4. Display results with `X-Served-By` identification
5. Clean up all started processes

### Run Services Individually

```bash
# LegacyApiNode (Node.js)
cd LegacyApiNode && npm run db:setup   # First time: generate Prisma client + seed data
npm start

# ModernApi (.NET)
dotnet run --project ModernApi          # Auto-creates SQLite DB + seed data on startup

# Proxy (.NET)
dotnet run --project Proxy
```

## Project Structure

```
├── Proxy/                    # YARP reverse proxy (.NET 10)
│   ├── Program.cs            # Proxy pipeline, middleware, CORS
│   ├── appsettings.json      # CurrentPhase selector
│   ├── appsettings.Phase1-FullLegacy.json
│   ├── appsettings.Phase2-MigrateGetUsers.json
│   └── appsettings.Phase3-MigrateAllUsers.json
├── LegacyApiNode/            # Legacy system (Node.js + Express)
│   ├── prisma/
│   │   ├── schema.prisma     # Prisma schema (SQLite)
│   │   └── seed.js           # Seed data (7 users)
│   ├── src/
│   │   ├── index.js          # Express app, all endpoints
│   │   └── data.js           # Prisma client instance
│   └── package.json
├── ModernApi/                # Modern system (.NET 10)
│   ├── Program.cs            # Minimal APIs with DI + DB initialization
│   ├── Models/User.cs        # User model, DTOs, enums
│   └── Services/UserService.cs  # Dapper + SQLite CRUD
├── test-migration.ps1        # Automated test script
├── strangler-fig-architecture.svg
└── CLAUDE.md
```

## Persistence

Both APIs use **SQLite** for data persistence with independent databases:

| API | ORM | Database File | Seed Data |
|-----|-----|---------------|-----------|
| LegacyApiNode | Prisma 5 | `LegacyApiNode/legacyapi.db` | 7 users (Prisma seed) |
| ModernApi | Dapper | `ModernApi/modernapi.db` | 5 users (auto-created on startup) |

- **LegacyApiNode**: Schema defined in `prisma/schema.prisma`, migrations via `npx prisma db push`, seeding via `node prisma/seed.js`
- **ModernApi**: Table auto-created on startup in `Program.cs`, seed data inserted if table is empty

## API Endpoints

| Method | Endpoint | LegacyApiNode | ModernApi |
|--------|----------|---------------|-----------|
| GET | `/health` | Phase 1 | Phase 1-3 |
| GET | `/api/users` | Phase 1 | Phase 2-3 |
| GET | `/api/users/{id}` | Phase 1-2 | Phase 3 |
| POST | `/api/users` | - | Phase 3 |
| POST | `/api/users/create` | Phase 1-2 | - |
| PUT | `/api/users/{id}` | Phase 1-2 | Phase 3 |
| DELETE | `/api/users/{id}` | - | Phase 3 |
| GET | `/api/products` | Phase 1-3 | - |
| GET | `/api/orders` | Phase 1-3 | - |

## Data Model Comparison

| Field | LegacyApiNode (Node.js) | ModernApi (.NET) |
|-------|-------------------------|-------------------|
| ID | `user_id` (snake_case) | `Id` (PascalCase) |
| Name | `user_name` | `Name` [Required] |
| Email | `user_email` | `Email` [EmailAddress] |
| Date | `created_date` (string) | `CreatedAt` (DateTime) |
| Status | `status_code` ("A"/"I") | `Status` (UserStatus enum) |

## License

MIT
