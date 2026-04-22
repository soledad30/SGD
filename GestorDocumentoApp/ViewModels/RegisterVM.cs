using System.ComponentModel.DataAnnotations;

namespace GestorDocumentoApp.ViewModels
{
    public class RegisterVM
    {
        [Required(ErrorMessage = "Email es requerido")]
        [EmailAddress(ErrorMessage = "Email no valido")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password es requerido")]
        [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$", ErrorMessage = "La contraseña debe incluir al menos una mayuscula, un numero y un simbolo")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirmar password es requerido")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Range(typeof(bool), "true", "true", ErrorMessage = "Debes aceptar los terminos y condiciones")]
        public bool AcceptTerms { get; set; }
    }
}
