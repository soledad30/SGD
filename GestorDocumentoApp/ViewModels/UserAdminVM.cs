using System.ComponentModel.DataAnnotations;

namespace GestorDocumentoApp.ViewModels
{
    public class UserAdminVM
    {
        public IEnumerable<UserAdminRowVM> Users { get; set; } = [];
        public IEnumerable<string> AvailableRoles { get; set; } = [];
    }

    public class UserAdminRowVM
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public IEnumerable<string> Roles { get; set; } = [];
    }

    public class UserCreateVM
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "User";
    }

    public class UserAssignRoleVM
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "User";
    }

    public class UserResetPasswordVM
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string NewPassword { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string ConfirmPassword { get; set; } = string.Empty;

        public bool ConfirmAdminReset { get; set; }
    }
}
