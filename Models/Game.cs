using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TorneioTenisMesa.Models
{
    public class Game
    {
        public int Id { get; set; }

        [ForeignKey("Player1")]
        public int Player1Id { get; set; }

        [ForeignKey("Player2")]
        public int Player2Id { get; set; }

        public int ScorePlayer1 { get; set; }

        public int ScorePlayer2 { get; set; }

        [ForeignKey("Tournament")]
        public int TournamentId { get; set; }

        public string? Group { get; set; }

        public DateTime Date { get; set; }

        // Navegações
        public Player? Player1 { get; set; }
        public Player? Player2 { get; set; }
        public Tournament? Tournament { get; set; }
    }
}
