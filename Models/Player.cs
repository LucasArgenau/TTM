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

        [ForeignKey("Tournament")]
        public int TournamentId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        // Navegações
        public Tournament? Tournament { get; set; }
        public User? User { get; set; }

        public List<Game> GamesAsPlayer1 { get; set; } = new();
        public List<Game> GamesAsPlayer2 { get; set; } = new();
    }
}
