using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TorneioTenisMesa.Models;
using TorneioTenisMesa.Models.ViewModels;
using System.Threading.Tasks;
using System.Linq;

[Authorize(Roles = "Player")]
public class PlayerController : Controller
{
    private readonly AppDbContext _context;

    public PlayerController(AppDbContext context)
    {
        _context = context;
    }

    // Página inicial do jogador (Index)
    public async Task<IActionResult> Index()
    {
        var userId = User.Identity!.Name;
        var player = await _context.Players
            .Include(p => p.User)
            .Include(p => p.Tournament)
            .FirstOrDefaultAsync(p => p.User!.UserName == userId);

        if (player == null)
        {
            return NotFound("Jogador não encontrado.");
        }

        // Lógica para exibir informações do jogador na página inicial
        return View(player);
    }

    // Exibe informações do jogador (Perfil)
    public async Task<IActionResult> Profile()
    {
        var userId = User.Identity!.Name;
        var player = await _context.Players
            .Include(p => p.User)
            .Include(p => p.Tournament)
            .FirstOrDefaultAsync(p => p.User!.UserName == userId);

        if (player == null)
        {
            return NotFound("Jogador não encontrado.");
        }

        return View(player);
    }

    // Exibe resultados das partidas
    public async Task<IActionResult> Results()
    {
        var userId = User.Identity!.Name;

        var player = await _context.Players
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.User!.UserName == userId);

        if (player == null)
        {
            return NotFound("Jogador não encontrado.");
        }

        var games = await _context.Games
            .Include(g => g.Player1).ThenInclude(p => p!.User)
            .Include(g => g.Player2).ThenInclude(p => p!.User)
            .Where(g => g.Player1Id == player.Id || g.Player2Id == player.Id)
            .ToListAsync();

        // Atualizando a seleção para usar o GameResultViewModel
        var results = games.Select(g => new GameResultViewModel
        {
            GameId = g.Id,
            Player1Name = g.Player1?.Name ?? "Desconhecido",
            Player2Name = g.Player2?.Name ?? "Desconhecido",
            ScorePlayer1 = g.ScorePlayer1,
            ScorePlayer2 = g.ScorePlayer2,
            Group = g.Group!,
            Date = g.Date
        }).ToList();

        return View(results);
    }


    // Exibe o ranking
    public async Task<IActionResult> Ranking()
    {
        var ranking = await _context.Players
            .Include(p => p.User)
            .OrderByDescending(p => p.Rating)
            .Select(p => new
            {
                PlayerName = p.Name,
                Username = p.User!.UserName,
                p.Rating
            })
            .ToListAsync();

        return View(ranking);
    }

    // Exibe detalhes do torneio atual em que o jogador está participando
    public async Task<IActionResult> TournamentDetails()
    {
        var userId = User.Identity!.Name;
        var player = await _context.Players
            .Include(p => p.Tournament)
            .FirstOrDefaultAsync(p => p.User!.UserName == userId);

        if (player == null || player.Tournament == null)
        {
            return NotFound("Jogador ou torneio não encontrado.");
        }

        var tournament = player.Tournament;
        return View(tournament);
    }
}
