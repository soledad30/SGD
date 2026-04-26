using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace GestorDocumentoApp.Models
{
    public enum ProjectMemberRole
    {
        Owner = 1,
        Maintainer = 2,
        Developer = 3,
        Reviewer = 4,
        Viewer = 5
    }

    public class ProjectMember
    {
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        [Required]
        public string UserId { get; set; } = string.Empty;
        public IdentityUser User { get; set; } = null!;

        public ProjectMemberRole Role { get; set; } = ProjectMemberRole.Viewer;
        public bool CanEdit { get; set; }
        public bool CanApprove { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public bool Active { get; set; } = true;
    }
}
