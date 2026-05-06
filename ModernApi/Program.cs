using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using ModernApi.Models;
using ModernApi.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var dbPath = Path.Combine(builder.Environment.ContentRootPath, "modernapi.db");
var db = new SqliteConnection($"Data Source={dbPath}");
db.Open();
InitializeDatabase(db);

builder.Services.AddSingleton(db);
builder.Services.AddSingleton<UserService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.Use(async (HttpContext context, Func<Task> next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-API-Source"] = "modern";
        context.Response.Headers["X-API-Version"] = "10.0.0";
        context.Response.Headers["X-Framework"] = ".NET-10";
        context.Response.Headers["X-Architecture"] = "modern-minimal-api";
        context.Response.Headers["X-Served-By"] = "C#/.NET 10 (ModernApi)";
        return Task.CompletedTask;
    });

    await next();
});

var usersGroup = app.MapGroup("/api/users")
    .WithTags("Users");

usersGroup.MapGet("/", async (
    [FromServices] UserService userService,
    [FromQuery] string? status,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10
    ) =>
{
    UserStatus? statusFilter = status?.ToLower() switch
    {
        "active" => UserStatus.Active,
        "inactive" => UserStatus.Inactive,
        "suspended" => UserStatus.Suspended,
        _ => null
    };

    var users = await userService.GetAllUsersAsync(statusFilter, page, pageSize);

    return Results.Ok(new
    {
        data = users,
        pagination = new
        {
            page,
            pageSize,
            hasNext = false,
            hasPrevious = page > 1
        },
        source = "modern-dotnet10"
    });
})
.WithName("GetUsers")
.WithDescription("获取所有用户（现代化实现）");

usersGroup.MapGet("/{id:int}", async (int id, [FromServices] UserService service) =>
{
    var user = await service.GetUserByIdAsync(id);

    return user is not null
        ? Results.Ok(new { data = user, source = "modern-dotnet10" })
        : Results.NotFound(new { error = "User not found", id, source = "modern-dotnet10" });
})
.WithName("GetUserById")
.WithDescription("获取指定用户");

usersGroup.MapPost("/", async (
    [FromBody] CreateUserRequest request,
    [FromServices] UserService service) =>
{
    if (!IsValidEmail(request.Email))
    {
        return Results.BadRequest(new { error = "Invalid email format", source = "modern-dotnet10" });
    }

    var user = await service.CreateUserAsync(request);

    return Results.Created($"/api/users/{user.Id}", new
    {
        data = user,
        source = "modern-dotnet10"
    });
})
.WithName("CreateUser")
.WithDescription("创建新用户")
.Produces<UserResponse>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest);

usersGroup.MapPut("/{id:int}", async (
    int id,
    [FromBody] UpdateUserRequest request,
    [FromServices] UserService service) =>
{
    var updated = await service.UpdateUserAsync(id, request);

    return updated
        ? Results.Ok(new { message = "User updated", source = "modern-dotnet10" })
        : Results.NotFound(new { error = "User not found", id, source = "modern-dotnet10" });
})
.WithName("UpdateUser")
.WithDescription("更新用户信息");

usersGroup.MapDelete("/{id:int}", async (
    int id,
    [FromServices] UserService service) =>
{
    var deleted = await service.DeleteUserAsync(id);

    return deleted
        ? Results.Ok(new { message = "User deleted", source = "modern-dotnet10" })
        : Results.NotFound(new { error = "User not found", id, source = "modern-dotnet10" });
})
.WithName("DeleteUser")
.WithDescription("删除用户");

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    service = "modern-api",
    version = "10.0.0",
    timestamp = DateTime.UtcNow
}));

bool IsValidEmail(string email)
{
    try
    {
        var addr = new System.Net.Mail.MailAddress(email);
        return addr.Address == email;
    }
    catch
    {
        return false;
    }
}

app.Run("http://localhost:5002");

static void InitializeDatabase(SqliteConnection db)
{
    db.Execute("""
        CREATE TABLE IF NOT EXISTS Users (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Email TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            Status INTEGER NOT NULL DEFAULT 1
        )
        """);

    var count = db.ExecuteScalar<int>("SELECT COUNT(*) FROM Users");
    if (count == 0)
    {
        db.Execute("""
            INSERT INTO Users (Name, Email, CreatedAt, Status) VALUES
            ('Alice Johnson', 'alice@modern.com', '2025-08-01T00:00:00Z', 1),
            ('Bob Smith', 'bob@modern.com', '2025-08-15T00:00:00Z', 1),
            ('Charlie Brown', 'charlie@modern.com', '2025-09-01T00:00:00Z', 2),
            ('Diana Prince', 'diana@modern.com', '2025-09-15T00:00:00Z', 1),
            ('Eve Wilson', 'eve@modern.com', '2025-10-01T00:00:00Z', 3)
            """);
    }
}
