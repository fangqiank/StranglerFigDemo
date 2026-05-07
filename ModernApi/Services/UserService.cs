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
            SELECT user_id, user_name, user_email, created_date, status_code
            FROM User
            WHERE (@StatusCode IS NULL OR status_code = @StatusCode)
            ORDER BY user_id
            LIMIT @PageSize OFFSET @Offset
            """;
        var rows = await db.QueryAsync<dynamic>(sql, new
        {
            StatusCode = statusFilter.HasValue ? statusFilter.Value.ToCode() : (string?)null,
            PageSize = pageSize,
            Offset = (page - 1) * pageSize
        });
        return rows.Select(MapToResponse);
    }

    public async Task<UserResponse?> GetUserByIdAsync(int id)
    {
        const string sql = "SELECT user_id, user_name, user_email, created_date, status_code FROM User WHERE user_id = @Id";
        var row = await db.QueryFirstOrDefaultAsync<dynamic>(sql, new { Id = id });
        return row is not null ? MapToResponse(row) : null;
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request)
    {
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var id = await db.ExecuteScalarAsync<int>("""
            INSERT INTO User (user_name, user_email, created_date, status_code)
            VALUES (@Name, @Email, @Date, 'A');
            SELECT last_insert_rowid();
            """, new { Name = request.Name, Email = request.Email, Date = date });
        logger.LogInformation("Created new user: {UserId} - {UserName}", id, request.Name);
        return new UserResponse(id, request.Name, request.Email, DateTime.UtcNow, "Active");
    }

    public async Task<bool> UpdateUserAsync(int id, UpdateUserRequest request)
    {
        var sets = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("Id", id);

        if (request.Name is not null) { sets.Add("user_name = @Name"); parameters.Add("Name", request.Name); }
        if (request.Email is not null) { sets.Add("user_email = @Email"); parameters.Add("Email", request.Email); }
        if (request.Status.HasValue) { sets.Add("status_code = @StatusCode"); parameters.Add("StatusCode", request.Status.Value.ToCode()); }

        if (sets.Count == 0)
            return await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM User WHERE user_id = @Id", new { Id = id }) > 0;

        var rows = await db.ExecuteAsync(
            $"UPDATE User SET {string.Join(", ", sets)} WHERE user_id = @Id", parameters);
        if (rows > 0) logger.LogInformation("Updated user: {UserId}", id);
        return rows > 0;
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        var rows = await db.ExecuteAsync("DELETE FROM User WHERE user_id = @Id", new { Id = id });
        if (rows > 0) logger.LogInformation("Deleted user: {UserId}", id);
        return rows > 0;
    }

    private static UserResponse MapToResponse(dynamic row) =>
        new((int)row.user_id, (string)row.user_name, (string)row.user_email,
            DateTime.TryParse((string)row.created_date, out var d) ? d : DateTime.MinValue,
            UserStatusExtensions.ToDisplay((string)row.status_code));
}
