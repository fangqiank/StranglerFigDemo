using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using ModernApi.Models;
using ModernApi.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var dbPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "shared.db"));
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
        CREATE TABLE IF NOT EXISTS User (
            user_id INTEGER PRIMARY KEY AUTOINCREMENT,
            user_name TEXT NOT NULL,
            user_email TEXT NOT NULL,
            created_date TEXT NOT NULL,
            status_code TEXT NOT NULL DEFAULT 'A'
        )
        """);

    var count = db.ExecuteScalar<int>("SELECT COUNT(*) FROM User");
    if (count == 0)
    {
        db.Execute("""
            INSERT INTO User (user_name, user_email, created_date, status_code) VALUES
            ('Alice Johnson', 'alice@legacy.com', '2023-01-15', 'A'),
            ('Bob Smith', 'bob@legacy.com', '2023-02-20', 'A'),
            ('Charlie Brown', 'charlie@legacy.com', '2023-03-10', 'I'),
            ('Diana Prince', 'diana@legacy.com', '2023-04-05', 'A'),
            ('Eve Wilson', 'eve@legacy.com', '2023-05-12', 'A'),
            ('Frank Castle', 'frank@legacy.com', '2023-06-20', 'A'),
            ('Grace Hopper', 'grace@legacy.com', '2023-07-15', 'I')
            """);
    }
}
