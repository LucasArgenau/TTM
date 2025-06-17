using System.ComponentModel.DataAnnotations;

namespace TorneioTenisMesa.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "The username is required.")]
        [Display(Name = "User")]
        public string? UserName { get; set; }  // Não nullable, removido o "?"

        [Required(ErrorMessage = "The password is required.")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string? Password { get; set; }  // Não nullable, removido o "?"

        [Display(Name = "Remember-me")]
        public bool RememberMe { get; set; }
    }
}
