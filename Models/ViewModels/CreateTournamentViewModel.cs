using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace TorneioTenisMesa.Models.ViewModels
{
    public class CreateTournamentViewModel
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Required]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        public int ImportBatchId { get; set; }

        public List<SelectListItem> ImportBatchOptions { get; set; } = new();
    }
}
