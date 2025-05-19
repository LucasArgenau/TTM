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
        public int? AdminUserId { get; set; }

        // Navegação para o usuário (admin) associado ao torneio
        public User? AdminUser { get; set; }

        // Navegações
        public List<TournamentPlayer> TournamentPlayers { get; set; } = new();
        public List<Game> Games { get; set; } = new();
    }
}
