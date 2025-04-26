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

        // Lista única de jogos, com a diferenciação do papel (Player1 ou Player2)
        public List<Game> Games { get; set; } = new();

        // Campo relacionado à importação de jogadores
        public int ImportBatchId { get; set; }  // Refere-se à importação específica
        public required ImportBatch ImportBatch { get; set; }  // Navegação para ImportBatch

        // Código do jogador, se necessário
        public string? PlayerCode { get; internal set; }
    }
}
