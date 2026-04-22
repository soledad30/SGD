
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace GestorDocumentoApp.Models
{
    [Index(nameof(ChangeRequestId), IsUnique = true)]
    public class Version
    {

        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public string ElementUrl { get; set; } = string.Empty;

        public DateTime UploadDate { get; set; }


        public string State { get; set; } = string.Empty;

        public string? ToolUrl { get; set; }

        public string VersionCode { get; set; } = string.Empty;

        public int Phase { get; set; } = 1;

        public int iteration { get; set; } = 1;

        public int ChangeRequestId { get; set; }

        public ChangeRequest ChangeRequest { get; set; } = null!;


        // Cada versión pertenece a un elemento
        public int ElementId { get; set; }
        public Element Element { get; set; } = null!;

        // Cada versión está asociada a un usuario (quién la subió)
        public string UserId { get; set; } = string.Empty;
        // public User User { get; set; } 
        public IdentityUser User { get; set; } = null!;
        // 🔗 Relación 1..* con RequirementType

        public int RequirementTypeId { get; set; }
        public RequirementType RequirementType { get; set; } = null!;

        // 🔗 Relación consigo mismo
        public int? ParentVersionId { get; set; }
        public Version? ParentVersion { get; set; }

    }
}
