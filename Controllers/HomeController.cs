using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TorneioTenisMesa.Models;

namespace TorneioTenisMesa.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    // Página inicial (Home)
    public IActionResult Index()
    {
        // Verifica se o usuário está autenticado
        if (User.Identity!.IsAuthenticated)
        {
            // Se estiver logado, verifica o papel do usuário
            if (User.IsInRole("Admin"))
            {
                // Redireciona para o painel do Admin
                return RedirectToAction("Index", "Admin");
            }
            else if (User.IsInRole("Player"))
            {
                // Redireciona para o painel do Jogador
                return RedirectToAction("Index", "Player");
            }
        }

        // Se não estiver logado, retorna para a tela de login
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
