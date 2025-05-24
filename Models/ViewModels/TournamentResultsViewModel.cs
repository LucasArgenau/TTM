using System.Collections.Generic;

public class TournamentResultsViewModel
{
    public int TournamentId { get; set; }
    public Dictionary<string, int> Results { get; set; } = new();
}