using Dapper;
using Microsoft.Data.Sqlite;
using ModernApi.Models;

namespace ModernApi.Services;

public class UserService(ILogger<UserService> logger, SqliteConnection db)
{
    public async Task<IEnumerable<UserResponse>> GetAllUsersAsync(
        UserStatus? statusFilter = null,
        int page = 1,
        int pageSize = 10)
    {
        const string sql = """
            SELECT * FROM Users
            WHERE (@Status IS NULL OR Status = @Status)
            ORDER BY Id
            LIMIT @PageSize OFFSET @Offset
            """;
        var users = await db.QueryAsync<User>(sql, new
        {
            Status = statusFilter.HasValue ? (int)statusFilter.Value : (int?)null,
            PageSize = pageSize,
            Offset = (page - 1) * pageSize
        });
        return users.Select(MapToResponse);
    }

    public async Task<UserResponse?> GetUserByIdAsync(int id)
    {
        var user = await db.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Id = @Id", new { Id = id });
        return user is not null ? MapToResponse(user) : null;
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request)
    {
        var id = await db.ExecuteScalarAsync<int>("""
            INSERT INTO Users (Name, Email, CreatedAt, Status)
            VALUES (@Name, @Email, @CreatedAt, @Status);
            SELECT last_insert_rowid();
            """, new
        {
            Name = request.Name,
            Email = request.Email,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            Status = (int)UserStatus.Active
        });
        logger.LogInformation("Created new user: {UserId} - {UserName}", id, request.Name);
        return new UserResponse(id, request.Name, request.Email, DateTime.UtcNow, UserStatus.Active.ToString());
    }

    public async Task<bool> UpdateUserAsync(int id, UpdateUserRequest request)
    {
        var sets = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("Id", id);

        if (request.Name is not null) { sets.Add("Name = @Name"); parameters.Add("Name", request.Name); }
        if (request.Email is not null) { sets.Add("Email = @Email"); parameters.Add("Email", request.Email); }
        if (request.Status.HasValue) { sets.Add("Status = @Status"); parameters.Add("Status", (int)request.Status.Value); }

        if (sets.Count == 0)
            return await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users WHERE Id = @Id", new { Id = id }) > 0;

        var rows = await db.ExecuteAsync(
            $"UPDATE Users SET {string.Join(", ", sets)} WHERE Id = @Id", parameters);
        if (rows > 0) logger.LogInformation("Updated user: {UserId}", id);
        return rows > 0;
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        var rows = await db.ExecuteAsync("DELETE FROM Users WHERE Id = @Id", new { Id = id });
        if (rows > 0) logger.LogInformation("Deleted user: {UserId}", id);
        return rows > 0;
    }

    private static UserResponse MapToResponse(User user) =>
        new(user.Id, user.Name, user.Email, user.CreatedAt, user.Status.ToString());
}
