using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using GestorDocumentoApp.Models;
using System.ComponentModel.DataAnnotations;

namespace GestorDocumentoApp.ViewModels
{
    public class ProjectVM
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nombre es requerido.")]
        public string Name { get; set; } = string.Empty;
        
        public string? Description { get; set; }

        [Required(ErrorMessage = "Fecha de creacion es requerido.")]
        public DateTime CreationDate { get; set; } = DateTime.Now;

    }

    public class ProjectElementVM
    {
        public int Id { get; set; }
        public string ProjectName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nombre es requerido.")]
        public string ElementName { get; set; } = string.Empty;
        public string? Description { get; set; }

        [Required(ErrorMessage = "Fecha de creacion es requerido.")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public int? ElementTypeId { get; set; }

        public string? ExternalUrlElement { get; set; } = null;
        public string? ExternaCodeElement { get; set; } = null;


        [ValidateNever]
        public IEnumerable<SelectListItem> ElementTypes { get; set; } = [];
    }

    public class ProjectMembersVM
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string CurrentUserId { get; set; } = string.Empty;
        public IEnumerable<ProjectMemberRowVM> Members { get; set; } = [];
        public IEnumerable<string> AvailableUserEmails { get; set; } = [];
    }

    public class ProjectMemberRowVM
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public ProjectMemberRole Role { get; set; }
        public bool CanEdit { get; set; }
        public bool CanApprove { get; set; }
        public bool Active { get; set; }
        public bool IsOwner => Role == ProjectMemberRole.Owner;
    }

    public class ProjectMemberCreateVM
    {
        public int ProjectId { get; set; }

        [Required(ErrorMessage = "Email es requerido.")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public ProjectMemberRole Role { get; set; } = ProjectMemberRole.Viewer;

        public bool CanEdit { get; set; }
        public bool CanApprove { get; set; }
    }

    public class ProjectMemberUpdateVM
    {
        [Required]
        public int ProjectId { get; set; }

        [Required]
        public int MemberId { get; set; }

        [Required]
        public ProjectMemberRole Role { get; set; }

        public bool CanEdit { get; set; }
        public bool CanApprove { get; set; }
        public bool Active { get; set; } = true;
    }
}
