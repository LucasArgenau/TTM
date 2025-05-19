using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TorneioTenisMesa.Models
{
    public class Player
    {
        public int Id { get; set; }

        [Required]
        public string? Name { get; set; }

        public int RatingsCentralId { get; set; }

        public int Rating { get; set; }

        public int StDev { get; set; }

        public string? Group { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        public User? User { get; set; }
        // Relacionamento muitos-para-muitos com torneios
        public List<TournamentPlayer> TournamentPlayers { get; set; } = new();

        // Lista única de jogos, com a diferenciação do papel (Player1 ou Player2)
        public List<Game> Games { get; set; } = new();

        // Código do jogador, se necessário
        public string? PlayerCode { get; internal set; }
    }
}
