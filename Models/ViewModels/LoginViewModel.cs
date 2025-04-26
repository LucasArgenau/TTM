using System.ComponentModel.DataAnnotations;

namespace TorneioTenisMesa.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "O nome de usuário é obrigatório.")]
        [Display(Name = "Usuário")]
        public string UserName { get; set; }  // Não nullable, removido o "?"

        [Required(ErrorMessage = "A senha é obrigatória.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Password { get; set; }  // Não nullable, removido o "?"

        [Display(Name = "Lembrar-me")]
        public bool RememberMe { get; set; }
    }
}
