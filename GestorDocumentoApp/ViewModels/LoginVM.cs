using System.ComponentModel.DataAnnotations;

namespace GestorDocumentoApp.ViewModels
{
    public class LoginVM
    {
        [Required(ErrorMessage = "Email es requerido")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password es requerido")]
        [DataType(DataType.Password,ErrorMessage ="Contrasena no valida")]
        public string Password { get; set; } = string.Empty;
        public bool RememberMe { get; set; }
    }
}
