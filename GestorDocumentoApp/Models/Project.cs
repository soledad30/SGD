using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
namespace GestorDocumentoApp.Models
{
    public class Project
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreationDate { get; set; }
        public string? GitHubTokenCipherText { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;
        public IdentityUser User { get; set; } = null!;

        public List<Element> Elements { get; set; } = [];
        public List<ProjectMember> Members { get; set; } = [];

    }
}
