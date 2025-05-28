using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TorneioTenisMesa.Models.ViewModels
{
    public class EditTournamentViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nome do torneio é obrigatório")]
        [Display(Name = "Nome do Torneio")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Data de início é obrigatória")]
        [DataType(DataType.Date)]
        [Display(Name = "Data de Início")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Data de término é obrigatória")]
        [DataType(DataType.Date)]
        [Display(Name = "Data de Término")]
        public DateTime EndDate { get; set; }

        public List<GameResultViewModel> Games { get; set; } = new();
    }
}
