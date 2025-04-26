namespace TorneioTenisMesa.Models.ViewModels
{
    public class TournamentViewModel
    {
        public int Id { get; set; } // ID do torneio
        public string Name { get; set; } = ""; // Nome do torneio
        public string AdminName { get; set; } = ""; // Nome do Admin
        public DateTime StartDate { get; set; } // Data de Início
        public DateTime EndDate { get; set; } // Data de Término
    }
}
