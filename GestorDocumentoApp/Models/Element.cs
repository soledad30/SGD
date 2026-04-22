using System.ComponentModel.DataAnnotations;

namespace GestorDocumentoApp.Models
{
    public class Element
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        public DateTime CreatedDate { get; set; }

        public int? ElementTypeId { get; set; }
        public ElementType? ElementType { get; set; }


        [Required]
        public int ProjectId { get; set; }

        public Project Project { get; set; } = null!;

        public string? ExternalUrlElement { get; set; }
        public string? ExternaCodeElement { get; set; }

        public List<Version> Versions { get; set; } = new List<Version>();

        
    }
}
