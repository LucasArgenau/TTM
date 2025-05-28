using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace TorneioTenisMesa.Models
{
    public class User : IdentityUser<int>
    {
        [Required]
        public override string? UserName { get; set; }

        [Required]
        public override string? PasswordHash { get; set; }

    }
}
