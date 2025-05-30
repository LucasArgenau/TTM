using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // For potential future validation attributes

namespace TorneioTenisMesa.Models.ViewModels
{
    public class ManageResultsViewModel
    {
        public int TournamentId { get; set; }
        public string? TournamentName { get; set; }
        public List<GameResultViewModel> Games { get; set; } = new List<GameResultViewModel>();

        // Filter Selections
        [Display(Name = "Filtrar por Grupo")]
        public int? SelectedGroupId { get; set; } // Nullable for "All Groups"

        [Display(Name = "Filtrar por Status do Jogo")]
        public string SelectedGameStatus { get; set; } = "All"; // Default to "All"

        // Data sources for filters
        public List<SelectListItem> GroupFilterItems { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> GameStatusFilterItems { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Player1FilterItems { get; set; } = new List<SelectListItem>();

        // Side Panel Score Entry Data
        [Display(Name = "Jogador 1")]
        public int? SelectedPlayer1Id { get; set; }

        [Range(0, 100, ErrorMessage = "Placar deve ser entre 0 e 100")] // Example validation
        public int? Player1Score { get; set; }

        [Display(Name = "Jogador 2")]
        public int? SelectedPlayer2Id { get; set; }

        [Range(0, 100, ErrorMessage = "Placar deve ser entre 0 e 100")] // Example validation
        public int? Player2Score { get; set; }
    }
}
