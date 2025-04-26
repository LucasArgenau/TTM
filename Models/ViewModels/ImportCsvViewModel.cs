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

        [Required(ErrorMessage = "Por favor, informe um nome ou descrição para a importação.")]
        [Display(Name = "Descrição da Importação")]
        public string Description { get; set; } = string.Empty;

        public string? SuccessMessage { get; set; }
        public string? ErrorMessage { get; set; }
        
        // Caso queira mostrar a lista de jogadores importados
        public List<Player>? Players { get; set; }
    }
}
