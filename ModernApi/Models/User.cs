using System.ComponentModel.DataAnnotations;

namespace ModernApi.Models
{
    public enum UserStatus
    {
        Active = 1,
        Inactive = 2,
        Suspended = 3
    }

    public static class UserStatusExtensions
    {
        public static string ToCode(this UserStatus status) => status switch
        {
            UserStatus.Active => "A",
            UserStatus.Inactive => "I",
            _ => "A"
        };

        public static string ToDisplay(string code) => code switch
        {
            "A" => "Active",
            "I" => "Inactive",
            _ => code
        };
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
