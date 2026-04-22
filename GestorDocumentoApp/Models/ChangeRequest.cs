using System.ComponentModel.DataAnnotations;

namespace GestorDocumentoApp.Models
{
    public class ChangeRequest
    {
        public int Id { get; set; }
        public ClasificationTypeCR ClasificationType { get; set; }
        public string? Description { get; set; }
        public PriorityCR Priority { get; set; }
        public StatusCR Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Code { get; set; } = string.Empty;

        public string? Remarks { get; set; }
        public ActionCR? Action { get; set; }
        public ApprovalStatus? ApprovalStatus { get; set; }
        public string? ApprovalAssigneeUserId { get; set; }
        public DateTime? ApprovalRequestedAt { get; set; }
        public DateTime? ApprovalDueAt { get; set; }
        public DateTime? ApprovalDecidedAt { get; set; }
        public int ElementId { get; set; }
        public Element Element { get; set; } = null!;
        public List<ChangeRequestAudit> Audits { get; set; } = [];
        public List<GitTraceLink> GitTraceLinks { get; set; } = new List<GitTraceLink>();

    }

    public class ChangeRequestAudit
    {
        public int Id { get; set; }
        public int ChangeRequestId { get; set; }
        public ChangeRequest ChangeRequest { get; set; } = null!;
        public DateTime ChangedAt { get; set; }
        public string ChangedByUserId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }

    public class GitTraceLink
    {
        public int Id { get; set; }
        public int ChangeRequestId { get; set; }
        public ChangeRequest ChangeRequest { get; set; } = null!;
        public int? VersionId { get; set; }
        public Version? Version { get; set; }
        public string Repository { get; set; } = string.Empty;
        public string? CommitSha { get; set; }
        public string? PullRequestUrl { get; set; }
        public int? PullRequestNumber { get; set; }
        public string LinkedByUserId { get; set; } = string.Empty;
        public DateTime LinkedAt { get; set; }
    }

    public enum ClasificationTypeCR
    {
        [Display(Name ="Incremento")]
        Enhancement = 1,

        [Display(Name = "Bug")]
        BugFixing = 2,

        [Display(Name = "Otro")]
        Other = 3,
    }

    public enum PriorityCR
    {
        [Display(Name = "Inmediato")]
        Immediate = 1,

        [Display(Name = "Urgente")]
        Urgent = 2,

        [Display(Name = "Lo mas pronto posible")]
        AsSoonAsPossible = 3,

        [Display(Name = "Deseable")]
        Desirable = 4,
    }
    public enum StatusCR
    {
        [Display(Name = "Iniciado")]
        Initiated = 1,

        [Display(Name = "Recibido")]
        Received = 2,

        [Display(Name = "Analizado")]
        Analyzed =3,

        [Display(Name = "Accion")]
        Action = 4,

        [Display(Name = "Asignado")]
        Assigned = 5,

        [Display(Name = "Checkout")]
        Checkout = 6,

        [Display(Name = "Modificado y testeado")]
        ModifiedAndTested = 7,

        [Display(Name = "Revisado")]
        Reviewed = 8,

        [Display(Name = "Aprobado")]
        Approved = 9,

        [Display(Name = "Checking")]
        Checkin = 10,

        [Display(Name = "En Linea base")]
        Baselined = 11,
    }
    public enum ActionCR
    {
        [Display(Name = "Aprobado")]
        Approved = 1,

        [Display(Name = "Rechazado")]
        Rejected = 2,

        [Display(Name = "Postergado")]    
        Deferred = 3,

        [Display(Name = "En espera")]
        InWait = 4,
    }

    public enum ApprovalStatus
    {
        [Display(Name = "Pendiente")]
        Pending = 1,

        [Display(Name = "Aprobado")]
        Approved = 2,

        [Display(Name = "Rechazado")]
        Rejected = 3,

        [Display(Name = "Expirado")]
        Expired = 4
    }

}
