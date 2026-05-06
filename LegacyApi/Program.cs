using LegacyApi.Models;
using Scalar.AspNetCore;
using System.Net.NetworkInformation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddOpenApi();

var app = builder.Build();
var legacyUserStore = new Dictionary<int, User>
{
    { 1, new User { user_id = 1, user_name = "Alice Johnson", user_email = "alice@legacy.com", created_date = "2023-01-15", status_code = "A" } },
    { 2, new User { user_id = 2, user_name = "Bob Smith", user_email = "bob@legacy.com", created_date = "2023-02-20", status_code = "A" } },
    { 3, new User { user_id = 3, user_name = "Charlie Brown", user_email = "charlie@legacy.com", created_date = "2023-03-10", status_code = "I" } },
    { 4, new User { user_id = 4, user_name = "Diana Prince", user_email = "diana@legacy.com", created_date = "2023-04-05", status_code = "A" } },
    { 5, new User { user_id = 5, user_name = "Eve Wilson", user_email = "eve@legacy.com", created_date = "2023-05-12", status_code = "A" } }
};

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.Use(async (HttpContext context, Func<Task> next) =>
{
   context.Response.OnStarting(() =>
   {
       context.Response.Headers["X-API-Source"] = "legacy";
       context.Response.Headers["X-API-Version"] = "2.3.1";
       context.Response.Headers["X-System-Type"] = "monolithic-legacy";
       return Task.CompletedTask;
   });

   await next();
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy", 
    system = "legacy",
    version = "2.3.1",
    uptime = "127 days"
}));

app.MapGet("api/users", (string? status, int? page, int? limit) =>
{
    var users = legacyUserStore.Values.AsEnumerable();

    if (!string.IsNullOrEmpty(status))
    {
        users = users.Where(u => u.status_code.Equals(status, StringComparison.OrdinalIgnoreCase));
    }

    var currentPage = page ?? 1;
    var pageSize = limit ?? 10;
    var totalUsers = users.Count();
    var pagedUsers = users.Skip((currentPage - 1) * pageSize).Take(pageSize);

    return Results.Ok(new
    {
        success = true,
        message = "Users retrieved successfully",
        data = pagedUsers.Select(u => new
        {
            id = u.user_id,
            name = u.user_name,
            email = u.user_email,
            created = u.created_date,
            active = u.status_code == "A"
        }),
        pagination = new
        {
            current_page = currentPage,
            per_page = pageSize,
            total_items = totalUsers,
            total_pages = (int)Math.Ceiling((double)totalUsers / pageSize)
        },
        source = "legacy-system"
    });
});

app.MapGet("/api/users/{user_id:int}", (int user_id) =>
{
    if (legacyUserStore.TryGetValue(user_id, out var user))
    {
        return Results.Ok(new
        {
            success = true,
            data = new
            {
                id = user.user_id,
                name = user.user_name,
                email = user.user_email,
                created = user.created_date,
                active = user.status_code == "A"
            },
            source = "legacy-system"
        });
    }

    return Results.NotFound(new
    {
        success = false,
        error = "User not found",
        error_code = "USER_404",
        source = "legacy-system"
    });
});

app.MapPost("/api/users", (HttpRequest request) => Results.Ok(new
{
    success = true,
    message = "This endpoint is complex in legacy system",
    note = "Requires form parsing, validation middleware, etc.",
    source = "legacy-system"
}));

app.MapPost("/api/users/create", (UserCreateRequest request) =>
{
    var newId = legacyUserStore.Keys.Max() + 1;
    var newUser = new User
    {
        user_id = newId,
        user_name = request.name,
        user_email = request.email,
        created_date = DateTime.Now.ToString("yyyy-MM-dd"),
        status_code = "A"
    };

    legacyUserStore[newId] = newUser;

    return Results.Created($"/api/users/{newId}", new
    {
        success = true,
        message = "User created",
        data = new
        {
            id = newUser.user_id,
            name = newUser.user_name,
            email = newUser.user_email
        },
        source = "legacy-system"
    });
});

app.MapPut("/api/users/{user_id:int}", (int user_id, UserUpdateRequest request) =>
{
    if (legacyUserStore.TryGetValue(user_id, out var user))
    {
        if (!string.IsNullOrEmpty(request.name))
            user.user_name = request.name;
        if (!string.IsNullOrEmpty(request.email))
            user.user_email = request.email;
        if (!string.IsNullOrEmpty(request.status))
            user.status_code = request.status;

        return Results.Ok(new
        {
            success = true,
            message = "User updated",
            source = "legacy-system"
        });
    }

    return Results.NotFound(new { success = false, error = "User not found", source = "legacy-system" });
});

app.MapDelete("/api/users/{user_id:int}", (int user_id) =>
{
    if (legacyUserStore.Remove(user_id))
    {
        return Results.Ok(new
        {
            success = true,
            message = "User deleted",
            source = "legacy-system"
        });
    }

    return Results.NotFound(new { success = false, error = "User not found", source = "legacy-system" });
});

app.MapGet("/api/orders", () => Results.Ok(new { message = "Orders from legacy system", source = "legacy-system" }));
app.MapGet("/api/products", () => Results.Ok(new { message = "Products from legacy system", source = "legacy-system" }));

app.Run("http://localhost:5001");

public abstract record UserCreateRequest(string name, string email);
public abstract record UserUpdateRequest(string? name, string? email, string? status);