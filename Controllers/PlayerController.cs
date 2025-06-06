using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TorneioTenisMesa.Models;
using TorneioTenisMesa.Models.ViewModels;
using System.Threading.Tasks;
using System.Linq;
using TorneioTenisMesa.ViewModels;

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
            .Include(p => p.TournamentPlayers)
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
        var userName = User.Identity!.Name;

        var player = await _context.Players
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.User!.UserName == userName);

        if (player == null)
            return NotFound("Jogador não encontrado.");

        var games = await _context.Games
            .Include(g => g.Tournament)
            .Include(g => g.Player1)
            .Include(g => g.Player2)
            .Where(g =>
                (g.Player1Id == player.Id || g.Player2Id == player.Id)
                && g.ScorePlayer1 >= 0 && g.ScorePlayer2 >= 0 // apenas jogos com resultados preenchidos
            )
            .ToListAsync();

        var history = games.Select(g =>
        {
            bool isPlayer1 = g.Player1Id == player.Id;
            var opponent = isPlayer1 ? g.Player2 : g.Player1;

            return new CompletedMatchViewModel
            {
                Date = g.Date,
                OpponentName = opponent?.Name ?? "Desconhecido",
                ScorePlayer = isPlayer1 ? g.ScorePlayer1 : g.ScorePlayer2,
                ScoreOpponent = isPlayer1 ? g.ScorePlayer2 : g.ScorePlayer1,
                TournamentName = g.Tournament?.Name ?? "Torneio não identificado"
            };
        })
        .OrderByDescending(m => m.Date)
        .ToList();

        var viewModel = new PlayerProfileViewModel
        {
            Name = player.Name!,
            Rating = player.Rating,
            MatchHistory = history
        };

        return View(viewModel);
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
            .Include(p => p.TournamentPlayers)
            .FirstOrDefaultAsync(p => p.User!.UserName == userId);

        if (player == null || player.TournamentPlayers == null)
        {
            return NotFound("Jogador ou torneio não encontrado.");
        }

        var tournament = player.TournamentPlayers;
        return View(tournament);
    }
}
