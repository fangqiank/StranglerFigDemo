# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
# Restore and build all projects
dotnet build StranglerFigDemo.slnx

# Run all three services (each in its own terminal)
dotnet run --project LegacyApi    # http://localhost:5001
dotnet run --project ModernApi    # http://localhost:5002
dotnet run --project Proxy        # http://localhost:5000

# Run a specific project
dotnet run --project <ProjectName>
```

No test projects exist. No solution-level `dotnet test` is applicable.

## Architecture

This is a **Strangler Fig pattern** migration demo. Three independent ASP.NET Core 10 Minimal API services cooperate behind a reverse proxy:

```
Client :5000 -> Proxy (YARP) --+--> LegacyApi :5001  (old system)
                               +--> ModernApi :5002  (new system)
```

### Proxy (`:5000`)
YARP-based reverse proxy. Routes are **config-driven** via phase-specific JSON files. The active phase is set in `Proxy/appsettings.json` under `Migration:CurrentPhase`, which selects the corresponding `appsettings.{PhaseName}.json` for route definitions. Phase-specific configs are loaded at startup and cannot be hot-swapped.

### LegacyApi (`:5001`)
Simulates a legacy monolith. In-memory `Dictionary<int, User>` store. Snake_case field names (`user_id`, `user_name`). Endpoints: GET/POST/PUT/DELETE `/api/users`, plus `/api/orders`, `/api/products`. Response headers include `X-API-Source: legacy`.

### ModernApi (`:5002`)
Rewritten version with proper layering: `UserService` (Singleton, in-memory) + typed request/response models (`CreateUserRequest`, `UpdateUserRequest`, `UserResponse`). PascalCase naming, enum-based status (`UserStatus.Active/Inactive/Suspended`). Response headers include `X-API-Source: modern`.

## Migration Phases

Controlled by `Migration:CurrentPhase` in `Proxy/appsettings.json`:

| Phase | Config File | Behavior |
|-------|-------------|----------|
| `Phase1` | `appsettings.Phase1-FullLegacy.json` | All requests -> LegacyApi |
| `Phase2-MigrateGetUsers` | `appsettings.Phase2-MigrateGetUsers.json` | `GET /api/users` -> ModernApi, rest -> LegacyApi |
| `Phase3-MigrateAllUsers` | `appsettings.Phase3-MigrateAllUsers.json` | All `/api/users/*` CRUD -> ModernApi, rest -> LegacyApi |

To advance migration: change `CurrentPhase` value in `Proxy/appsettings.json` and restart Proxy.

## Key Design Points

- **Route ordering**: Modern routes have no explicit Order (default 0), legacy fallback has `Order: 100`. YARP matches lower-order routes first.
- **Response headers** distinguish source: `X-API-Source` (legacy/modern), `X-API-Version`, `X-Proxy-Phase`.
- **No shared data store**: LegacyApi and ModernApi maintain separate in-memory user dictionaries with different schemas and different seed data. They are independent systems.
- **OpenAPI/Scalar**: Both APIs expose `/openapi/v1.json` and Scalar UI in Development mode.
- Both APIs use `app.Run("http://localhost:{port}")` to bind specific ports.

## Adding a New Migration Phase

1. Create `Proxy/appsettings.Phase{N}-{Description}.json` with the desired YARP route/cluster config (copy from an existing phase).
2. Add specific routes that point to `modern-cluster` for newly migrated endpoints.
3. Keep the `legacy-fallback` catch-all route with `Order: 100`.
4. Update `Migration:CurrentPhase` in `Proxy/appsettings.json` to the new phase name.
