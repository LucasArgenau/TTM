using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TorneioTenisMesa.Models;

namespace TorneioTenisMesa.Models.ViewModels
{
    public class ExportGamesViewModel
    {
        [Required]
        public int TournamentId { get; set; }

        public List<Tournament>? Tournaments { get; set; }

        public string ExportOption { get; set; } = "all";
    }
}
