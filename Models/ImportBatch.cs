using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TorneioTenisMesa.Models
{
    public class ImportBatch
    {
        public int Id { get; set; }

        // Data da importação, com valor padrão para a data e hora atual
        public DateTime ImportedAt { get; set; } = DateTime.Now; 

        // Nome do arquivo importado (obrigatório)
        [Required]
        public string? FileName { get; set; }

        // Relacionamento com os jogadores importados
        public ICollection<Player> Players { get; set; } = new List<Player>(); 
    }
}
