using ModernApi.Models;

namespace ModernApi.Services
{
    public class UserService(ILogger<UserService> logger)
    {
        private readonly Dictionary<int, User> _users = new()
        {
            { 1, new User { Id = 1, Name = "Alice Johnson", Email = "alice@modern.com", CreatedAt = DateTime.UtcNow.AddDays(-100), Status = UserStatus.Active } },
            { 2, new User { Id = 2, Name = "Bob Smith", Email = "bob@modern.com", CreatedAt = DateTime.UtcNow.AddDays(-80), Status = UserStatus.Active } },
            { 3, new User { Id = 3, Name = "Charlie Brown", Email = "charlie@modern.com", CreatedAt = DateTime.UtcNow.AddDays(-60), Status = UserStatus.Inactive } }
        };

        public Task<IEnumerable<UserResponse>> GetAllUsersAsync(
            UserStatus? statusFilter = null,
            int page =1,
            int pageSize =10
            )
        {
            var query = _users.Values.AsEnumerable();

            if (statusFilter.HasValue)
                query = query.Where(u => u.Status == statusFilter.Value);

            var users = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MapToResponse);

            return Task.FromResult(users);
        }

        public Task<UserResponse?> GetUserByIdAsync(int id)
        {
            _users.TryGetValue(id, out var user);
            return Task.FromResult(user != null ? MapToResponse(user) : null);
        }

        public Task<UserResponse> CreateUserAsync(CreateUserRequest request)
        {
            var newId = _users.Keys.Max() + 1;
            var user = new User
            {
                Id = newId,
                Name = request.Name,
                Email = request.Email,
                CreatedAt = DateTime.UtcNow,
                Status = UserStatus.Active
            };

            _users[newId] = user;
            logger.LogInformation("Created new user: {UserId} - {UserName}", newId, request.Name);

            return Task.FromResult(MapToResponse(user));
        }

        public Task<bool> UpdateUserAsync(int id, UpdateUserRequest request)
        {
            if (!_users.TryGetValue(id, out var user))
                return Task.FromResult(false);

            if (request.Name != null)
                user.Name = request.Name;
            if (request.Email != null)
                user.Email = request.Email;
            if (request.Status.HasValue)
                user.Status = request.Status.Value;

            logger.LogInformation("Updated user: {UserId}", id);
            return Task.FromResult(true);
        }

        public Task<bool> DeleteUserAsync(int id)
        {
            var removed = _users.Remove(id);
            if (removed)
                logger.LogInformation("Deleted user: {UserId}", id);
            return Task.FromResult(removed);
        }

        private static UserResponse MapToResponse(User user)
        {
            return new UserResponse(
                user.Id,
                user.Name,
                user.Email,
                user.CreatedAt,
                user.Status.ToString()
            );
        }
    }
}
