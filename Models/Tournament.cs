using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TorneioTenisMesa.Models
{
    public class Tournament
    {
        public int Id { get; set; }

        [Required]
        public string? Name { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        [ForeignKey("User")]
        public int AdminUserId { get; set; }

        // Navegações
        public List<Player> Players { get; set; } = new();
        public List<Game> Games { get; set; } = new();
    }
}
