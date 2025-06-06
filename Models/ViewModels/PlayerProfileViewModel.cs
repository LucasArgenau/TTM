using System;

namespace TorneioTenisMesa.ViewModels
{
    public class PlayerProfileViewModel
    {
        public string Name { get; set; } = string.Empty;
        public int Rating { get; set; }
        public List<CompletedMatchViewModel> MatchHistory { get; set; } = new();
    }

    public class CompletedMatchViewModel
    {
        public DateTime Date { get; set; }
        public string OpponentName { get; set; } = string.Empty;
        public int ScorePlayer { get; set; }
        public int ScoreOpponent { get; set; }
        public string TournamentName { get; set; } = string.Empty;
    }
}
