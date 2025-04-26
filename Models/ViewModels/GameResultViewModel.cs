namespace TorneioTenisMesa.Models.ViewModels
{
    public class GameResultViewModel
    {
        public int GameId { get; set; }

        public string Player1Name { get; set; } = "";
        public string Player2Name { get; set; } = "";

        public int ScorePlayer1 { get; set; }
        public int ScorePlayer2 { get; set; }

        public string Group { get; set; } = "";
        public DateTime Date { get; set; }
    }
}
