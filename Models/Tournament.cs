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

        // Navegação para o usuário (admin) associado ao torneio
        public User? AdminUser { get; set; }

        // Relacionamento com o ImportBatch (para vincular a importação de jogadores)
        [ForeignKey("ImportBatch")]
        public int ImportBatchId { get; set; }
        public required ImportBatch ImportBatch { get; set; }  // Navegação para ImportBatch

        // Navegações
        public List<Player> Players { get; set; } = new();
        public List<Game> Games { get; set; } = new();
    }
}
