using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TorneioTenisMesa.Models.ViewModels
{
    public class EditTournamentViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Please enter the name of the Tournament")]
        [Display(Name = "Tournament")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the Start Date")]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Please enter the End Date")]
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        public List<GameResultViewModel> Games { get; set; } = new();
    }
}
