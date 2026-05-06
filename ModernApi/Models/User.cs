using System.ComponentModel.DataAnnotations;

namespace ModernApi.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public required string Name { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(200)]
        public required string Email { get; set; }

        public DateTime CreatedAt { get; set; }

        public UserStatus Status { get; set; } = UserStatus.Active;
    }
    public enum UserStatus
    {
        Active = 1,
        Inactive = 2,
        Suspended = 3
    }

    public record CreateUserRequest(
        [Required][StringLength(100)] string Name,
        [Required][EmailAddress] string Email
    );

    public record UpdateUserRequest(
        [StringLength(100)] string? Name,
        [EmailAddress] string? Email,
        UserStatus? Status
    );

    public record UserResponse(
        int Id,
        string Name,
        string Email,
        DateTime CreatedAt,
        string Status
    );
}
