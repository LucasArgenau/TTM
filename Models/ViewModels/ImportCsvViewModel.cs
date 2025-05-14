using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace TorneioTenisMesa.Models.ViewModels
{
    public class ImportCsvViewModel
    {
        [Required(ErrorMessage = "Por favor, selecione um arquivo CSV.")]
        [DataType(DataType.Upload)]
        public required IFormFile CsvFile { get; set; }

        [Required]
        public int TournamentId { get; set; }  // ID do torneio vinculado à importação

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }

        public List<Player>? Players { get; set; }  // Opcional para mostrar lista importada
    }
}
