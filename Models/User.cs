using System.ComponentModel.DataAnnotations;

namespace TorneioTenisMesa.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public string? UserName { get; set; }

        [Required]
        public string? PasswordHash { get; set; }

        // Propriedade opcional para indicar tipo de usuário (Admin ou Player)
        public string Role { get; set; } = "Player";
    }
}
