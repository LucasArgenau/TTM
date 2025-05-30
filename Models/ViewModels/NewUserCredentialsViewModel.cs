using System.Collections.Generic;

namespace TorneioTenisMesa.Models.ViewModels
{
    public class NewUserCredentialsViewModel
    {
        public List<NewUserCredentialItem> NewUsers { get; set; } = new List<NewUserCredentialItem>();
        public int TournamentId { get; set; }
        public string? TournamentName { get; set; } // Optional
    }
}
