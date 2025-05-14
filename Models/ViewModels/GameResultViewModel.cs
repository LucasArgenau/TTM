using System;
using System.ComponentModel.DataAnnotations;

namespace TorneioTenisMesa.Models.ViewModels
{
    public class GameResultViewModel
    {
        public int GameId { get; set; }

        public string Player1Name { get; set; } = string.Empty;
        public string Player2Name { get; set; } = string.Empty;

        [Range(0, int.MaxValue, ErrorMessage = "A pontuação deve ser zero ou maior.")]
        public int ScorePlayer1 { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "A pontuação deve ser zero ou maior.")]
        public int ScorePlayer2 { get; set; }

        public string Group { get; set; } = string.Empty;

        public DateTime Date { get; set; }
    }
}
