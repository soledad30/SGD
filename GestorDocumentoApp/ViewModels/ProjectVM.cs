using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
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
}
