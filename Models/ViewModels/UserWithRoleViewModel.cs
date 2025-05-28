namespace TorneioTenisMesa.Models.ViewModels
{
    public class UserWithRoleViewModel
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // ESSA LINHA É ESSENCIAL
    }
}
