using System;
using System.ComponentModel.DataAnnotations;

namespace TorneioTenisMesa.Models.ViewModels
{
    public class GameResultViewModel
    {
        public int GameId { get; set; }

        public string Player1Name { get; set; } = string.Empty;
        public string Player2Name { get; set; } = string.Empty;

        [Range(0, int.MaxValue, ErrorMessage = "The score must be zero or greater.")]
        public int ScorePlayer1 { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "The score must be zero or greater.")]
        public int ScorePlayer2 { get; set; }

        public string Group { get; set; } = string.Empty;

        public DateTime Date { get; set; }

        // Adicione esta propriedade
        public int TournamentId { get; set; }
    }
}
