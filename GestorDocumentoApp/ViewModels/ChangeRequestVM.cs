using GestorDocumentoApp.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace GestorDocumentoApp.ViewModels
{
    public class ChangeRequestVM
    {
        public int Id { get; set; }
        public string? Description { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string Clasification { get; set; } = string.Empty;
        public StatusCR Status { get; set; }
        public ActionCR Action { get; set; }
        public string Code { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }


    }

    public class ChangeRequestAuditVM
    {
        public DateTime ChangedAt { get; set; }
        public string ChangedByUserId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }

    public class ChangeRequestDetailsVM
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Priority { get; set; } = string.Empty;
        public string Clasification { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string ApprovalStatus { get; set; } = "No solicitado";
        public string? ApprovalAssigneeUserId { get; set; }
        public DateTime? ApprovalRequestedAt { get; set; }
        public DateTime? ApprovalDueAt { get; set; }
        public DateTime? ApprovalDecidedAt { get; set; }
        public bool IsPendingApproval { get; set; }
        public bool IsCurrentUserApprover { get; set; }
        public bool IsSlaExpired { get; set; }
        public IEnumerable<ChangeRequestVersionTraceVM> Versions { get; set; } = [];
        public IEnumerable<GitTraceLinkVM> GitTraceLinks { get; set; } = [];
        public IEnumerable<ChangeRequestAuditVM> Audits { get; set; } = [];
        public IEnumerable<ChangeRequestAuditVM> StateHistory { get; set; } = [];
    }

    public class ChangeRequestVersionTraceVM
    {
        public int VersionId { get; set; }
        public string VersionCode { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public int Phase { get; set; }
        public int Iteration { get; set; }
    }

    public class GitTraceLinkVM
    {
        public int Id { get; set; }
        public string Repository { get; set; } = string.Empty;
        public string? CommitSha { get; set; }
        public string? PullRequestUrl { get; set; }
        public int? PullRequestNumber { get; set; }
        public string LinkedByUserId { get; set; } = string.Empty;
        public DateTime LinkedAt { get; set; }
        public int? VersionId { get; set; }
    }

    public class ChangeRequestIndexVM:PagedList<ChangeRequestVM>
    {
       
        public string? ElementName { get; set; }
        public int? ElementId { get; set; }
        public int? ProjectId { get; set; }
        public string? Search { get; set; }
        public PriorityCR? PriorityFilter { get; set; }
        public StatusCR? StatusFilter { get; set; }
        public ActionCR? ActionFilter { get; set; }
        public int? PhaseFilter { get; set; }
        public int? IterationFilter { get; set; }
        public string SortBy { get; set; } = "createdAt";
        public string SortDir { get; set; } = "desc";
        public new bool HasPrevious { get; set; }
        public new bool HasNext { get; set; }
        public IEnumerable<SelectListItem> Projects { get; set; } = [];
        public IEnumerable<SelectListItem> Elements { get; set; } = [];
        public IEnumerable<SelectListItem> PriorityOptions { get; set; } = [];
        public IEnumerable<SelectListItem> StatusOptions { get; set; } = [];
        public IEnumerable<SelectListItem> ActionOptions { get; set; } = [];
        public IEnumerable<SelectListItem> PhaseOptions { get; set; } = [];
    }
    public class ChangeRequestCreateByElementVM
    {

        public int ElementId { get; set; }
        public string? ElementName { get; set; } 

        [Required]
        public ClasificationTypeCR ClasificationType { get; set; }

        public string? Description { get; set; }

        [Required]
        public PriorityCR Priority { get; set; }

        [Required]
        public StatusCR Status { get; set; }

        public string? Remarks { get; set; }

        public string Code { get; set; } = string.Empty;

        public ActionCR? Action { get; set; }

        [ValidateNever]
        public IEnumerable<SelectListItem>? ClasificationOptions { get; set; }

        [ValidateNever]
        public IEnumerable<SelectListItem>? PriorityOptions { get; set; }

        [ValidateNever]
        public IEnumerable<SelectListItem>? StatusOptions { get; set; }

        [ValidateNever]
        public IEnumerable<SelectListItem>? ActionOptions { get; set; }


    }
    public class ChangeRequestCreateVM
    {
        [Required(ErrorMessage="Clasificacion es requerido.")]
        public ClasificationTypeCR? ClasificationType { get; set; }

        public string? Description { get; set; }

        [Required(ErrorMessage = "Prioridad es requerido.")]
        public PriorityCR? Priority { get; set; }

        [Required(ErrorMessage = "Proceso es requerido.")]
        public StatusCR? Status { get; set; }

        public string? Remarks { get; set; }

        [Required(ErrorMessage = "Codigo es requerido.")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Estado es requerido.")]
        public ActionCR? Action { get; set; }

        [Required(ErrorMessage ="Elemento es requerido.")]
        public int? ElementId { get; set; }

        [ValidateNever]
        public IEnumerable<SelectListItem>? ClasificationOptions { get; set; }

        [ValidateNever]
        public IEnumerable<SelectListItem>? PriorityOptions { get; set; }

        [ValidateNever]
        public IEnumerable<SelectListItem>? StatusOptions { get; set; }

        [ValidateNever]
        public IEnumerable<SelectListItem>? ActionOptions { get; set; }

        [ValidateNever]
        public IEnumerable<SelectListItem> Elements { get; set; } = [];

    }
}
