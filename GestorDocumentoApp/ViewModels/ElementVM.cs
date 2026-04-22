using GestorDocumentoApp.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace GestorDocumentoApp.ViewModels
{
    public class ElementVM
    {
        public int Id { get; set; }
        [Required(ErrorMessage ="Nombre es requerido.")]
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        [Required(ErrorMessage = "Fecha de creacion es requerido.")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public int? ElementTypeId { get; set; }


        [Required(ErrorMessage ="Proyecto es requerido.")]
        public int? ProjectId { get; set; }

        [ValidateNever]
        public IEnumerable<SelectListItem> ElementTypes { get; set; } = [];

        [ValidateNever]
        public IEnumerable<SelectListItem> Projects { get; set; } = [];

    }

    public class ElementIndexVM:PagedList<Element>
    {

        public string? ProjectName { get; set; }
        public int? ProjectId { get; set; }
        public new bool HasPrevious { get; set; }
        public new bool HasNext { get; set; }
        public IEnumerable<SelectListItem> Projects { get; set; } = [];
    }

    public class ElementTypeVM
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nombre es requerido")]
        public string Name { get;set;  } = string.Empty;

        public string? Description { get; set; }
    }
}
