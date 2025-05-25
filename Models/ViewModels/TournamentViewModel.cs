namespace TorneioTenisMesa.Models.ViewModels
{
    public class TournamentViewModel
    {
        public int TournamentId { get; set; }
        public string TournamentName { get; set; } = string.Empty;
        public List<Player> Players { get; set; } = new();
        public List<Game> Games { get; set; } = new();

        public Game? GetGame(int player1Id, int player2Id)
        {
            return Games.FirstOrDefault(g =>
                (g.Player1Id == player1Id && g.Player2Id == player2Id) ||
                (g.Player1Id == player2Id && g.Player2Id == player1Id));
        }
    }
}
