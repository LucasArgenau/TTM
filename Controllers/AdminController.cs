using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TorneioTenisMesa.Models;
using TorneioTenisMesa.Models.ViewModels;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tournaments = await _context.Tournaments
            .OrderByDescending(t => t.StartDate)
            .ToListAsync();

        return View(tournaments);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteTournament(int id)
    {
        var tournament = await _context.Tournaments.FindAsync(id);
        if (tournament != null)
        {
            _context.Tournaments.Remove(tournament);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Index");
    }

    [HttpGet]
    public IActionResult ImportCsv(int tournamentId)
    {
        var model = new ImportCsvViewModel
        {
            TournamentId = tournamentId,
            CsvFile = null!
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportCsv(ImportCsvViewModel model)
    {
        if (model.CsvFile == null || model.CsvFile.Length == 0)
        {
            ModelState.AddModelError("", "Por favor, envie um arquivo CSV.");
            return View(model);
        }

        try
        {
            // Lê os jogadores a partir do arquivo CSV
            var players = ParseCsv(model.CsvFile, model.TournamentId);

            // Verifica e adiciona os jogadores ou atualiza os existentes
            foreach (var player in players)
            {
                var existingPlayer = await _context.Players
                    .FirstOrDefaultAsync(p => p.RatingsCentralId == player.RatingsCentralId && p.TournamentId == model.TournamentId);

                if (existingPlayer == null)
                {
                    _context.Players.Add(player); // Adiciona novo jogador
                }
                else
                {
                    existingPlayer.Name = player.Name;
                    existingPlayer.Group = player.Group;
                    existingPlayer.Rating = player.Rating;
                    existingPlayer.StDev = player.StDev; // Atualiza as informações do jogador
                }
            }

            // Salva as alterações dos jogadores (adicionados ou atualizados)
            await _context.SaveChangesAsync();

            // Atualiza a lista de jogadores para o torneio
            var savedPlayers = await _context.Players
                .Where(p => p.TournamentId == model.TournamentId)
                .ToListAsync();

            // Gera os jogos automaticamente
            var games = new List<Game>();
            var groups = savedPlayers.GroupBy(p => p.Group);

            foreach (var group in groups)
            {
                var groupPlayers = group.ToList();

                for (int i = 0; i < groupPlayers.Count; i++)
                {
                    for (int j = i + 1; j < groupPlayers.Count; j++)
                    {
                        games.Add(new Game
                        {
                            Player1Id = groupPlayers[i].Id,
                            Player2Id = groupPlayers[j].Id,
                            TournamentId = model.TournamentId,
                            Group = group.Key,
                            Date = DateTime.Now
                        });
                    }
                }
            }

            // Adiciona os jogos ao banco de dados
            await _context.Games.AddRangeAsync(games);

            // Salva as alterações dos jogos
            await _context.SaveChangesAsync();

            // Mensagem de sucesso (opcional)
            model.SuccessMessage = "Jogadores importados e jogos gerados com sucesso!";

            // Redireciona para a tela de gerenciamento de resultados
            return RedirectToAction("ManagerResults", new { tournamentId = model.TournamentId });
        }
        catch (Exception ex)
        {
            // Em caso de erro, adiciona uma mensagem de erro
            ModelState.AddModelError("", $"Erro ao processar o arquivo CSV: {ex.Message}");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveResults(IFormCollection form)
    {
        // Obtém os IDs dos jogos do formulário e os resultados
        var gameIds = form.Keys.Where(k => k.StartsWith("ScorePlayer1_")).ToList();
        var games = await _context.Games.Where(g => gameIds.Contains($"ScorePlayer1_{g.Id}")).ToListAsync();

        foreach (var game in games)
        {
            // Obtém os resultados enviados pelo formulário
            int scorePlayer1 = 0;
            if (!StringValues.IsNullOrEmpty(form[$"ScorePlayer1_{game.Id}"]))

            {
                int.TryParse(form[$"ScorePlayer1_{game.Id}"], out scorePlayer1);
            }

            int scorePlayer2 = 0;
            if (!StringValues.IsNullOrEmpty(form[$"ScorePlayer2_{game.Id}"]))

            {
                int.TryParse(form[$"ScorePlayer2_{game.Id}"], out scorePlayer2);
            }

            // Atualiza os scores dos jogadores
            game.ScorePlayer1 = scorePlayer1;
            game.ScorePlayer2 = scorePlayer2;
        }

        // Salva as mudanças no banco de dados
        await _context.SaveChangesAsync();

        // Redireciona para a página de gerenciamento de resultados ou onde for necessário
        return RedirectToAction("ManagerResults", new { tournamentId = games.First().TournamentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateGames(int tournamentId)
    {
        var players = await _context.Players
            .Where(p => p.TournamentId == tournamentId)
            .ToListAsync();

        var groups = players.GroupBy(p => p.Group);
        var games = new List<Game>();

        foreach (var group in groups)
        {
            var groupPlayers = group.ToList();

            for (int i = 0; i < groupPlayers.Count; i++)
            {
                for (int j = i + 1; j < groupPlayers.Count; j++)
                {
                    var game = new Game
                    {
                        Player1Id = groupPlayers[i].Id,
                        Player2Id = groupPlayers[j].Id,
                        TournamentId = tournamentId,
                        Group = group.Key,
                        Date = DateTime.Now
                    };

                    games.Add(game);
                }
            }
        }

        await _context.Games.AddRangeAsync(games);
        await _context.SaveChangesAsync();

        return RedirectToAction("ManagerResults", new { tournamentId = tournamentId });
    }
    private List<Player> ParseCsv(IFormFile csvFile, int tournamentId)
    {
        var players = new List<Player>();

        using (var reader = new StreamReader(csvFile.OpenReadStream()))
        {
            reader.ReadLine(); // Pula o cabeçalho

            int lineNumber = 1;

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var values = line.Split(',');

                // Validação mínima de colunas (esperado: pelo menos 8 colunas)
                if (values.Length < 8)
                {
                    Console.WriteLine($"Linha {lineNumber} ignorada: número insuficiente de colunas.");
                    continue;
                }

                // Conversões seguras
                if (!int.TryParse(values[1], out int ratingsCentralId) ||
                    !int.TryParse(values[4], out int rating) ||
                    !int.TryParse(values[5], out int stDev))
                {
                    Console.WriteLine($"Linha {lineNumber} ignorada: erro ao converter inteiros.");
                    continue;
                }

                var player = new Player
                {
                    Name = values[0].Trim(),
                    RatingsCentralId = ratingsCentralId,
                    Rating = rating,
                    StDev = stDev,
                    Group = values[7].Trim(),
                    TournamentId = tournamentId
                };

                players.Add(player);
            }
        }

        return players;
    }

    public IActionResult ManagerResults(int tournamentId)
    {
        try
        {
            var games = _context.Games
                .Where(g => g.TournamentId == tournamentId)
                .Include(g => g.Player1) 
                .Include(g => g.Player2) 
                .ToList();
            
            return View(games);
        }
        catch (Exception ex)
        {
            // Log do erro
            Console.WriteLine(ex.Message);
            return View("Error"); // Ou a view de erro que você preferir
        }
    }


    
    [HttpGet]
    public IActionResult ExportGames()
    {
        var tournaments = _context.Tournaments
            .Where(t => t.EndDate < DateTime.Now)
            .OrderByDescending(t => t.EndDate)
            .ToList();

        var model = new ExportGamesViewModel
        {
            Tournaments = tournaments
        };

        return View(model);
    }

    [HttpPost]
    public IActionResult ExportGamesToCsv(ExportGamesViewModel model)
    {
        var gamesQuery = _context.Games
            .Include(g => g.Player1)
            .Include(g => g.Player2)
            .Where(g => g.TournamentId == model.TournamentId);

        if (model.ExportOption == "withResults")
        {
            gamesQuery = gamesQuery.Where(g => g.ScorePlayer1 > 0 || g.ScorePlayer2 > 0);
        }

        var games = gamesQuery.ToList();

        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("Jogador1,Placar1,Jogador2,Placar2,Grupo,Data");

        foreach (var g in games)
        {
            csvBuilder.AppendLine($"{g.Player1!.Name},{g.ScorePlayer1},{g.Player2!.Name},{g.ScorePlayer2},{g.Group},{g.Date:dd/MM/yyyy}");
        }

        var bytes = Encoding.UTF8.GetBytes(csvBuilder.ToString());
        return File(bytes, "text/csv", "jogos.csv");
    }

    // Primeira tela: Preenchimento do nome, data de início e data de término do torneio
    [HttpGet]
    public IActionResult CreateTournament()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTournament(CreateTournamentViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var tournament = new Tournament
        {
            Name = model.Name,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
        };

        _context.Tournaments.Add(tournament);
        await _context.SaveChangesAsync();

        // Redireciona para a etapa de importação de jogadores com o ID do torneio
        return RedirectToAction("ImportCsv", new { tournamentId = tournament.Id });
    }

    // Detalhes de um torneio específico
    [HttpGet]
    public IActionResult TournamentDetails(int id)
    {
        var tournament = _context.Tournaments
            .Include(t => t.Players)
            .Include(t => t.Games).ThenInclude(g => g.Player1)
            .Include(t => t.Games).ThenInclude(g => g.Player2)
            .FirstOrDefault(t => t.Id == id);

        if (tournament == null)
            return NotFound();

        return View("TournamentDetails", tournament);
    }

    // GET: /Admin/ManageResults
        [HttpGet]
        public IActionResult ManageResults()
        {
            var games = _context.Games
                .Include(g => g.Player1)
                .Include(g => g.Player2)
                .Include(g => g.Tournament)
                .ToList();

            var model = games.Select(g => new GameResultViewModel
            {
                GameId = g.Id,
                Player1Name = g.Player1?.Name ?? "N/A",
                Player2Name = g.Player2?.Name ?? "N/A",
                ScorePlayer1 = g.ScorePlayer1,
                ScorePlayer2 = g.ScorePlayer2,
                Group = g.Group!,
                Date = g.Date
            }).ToList();

            return View(model);
        }

        // POST: /Admin/UpdateGameResults
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateGameResults(List<GameResultViewModel> gameResults)
        {
            foreach (var result in gameResults)
            {
                if (result.ScorePlayer1 < 0 || result.ScorePlayer2 < 0)
                    continue; // Ignora resultados inválidos

                var game = await _context.Games.FindAsync(result.GameId);
                if (game != null)
                {
                    game.ScorePlayer1 = result.ScorePlayer1;
                    game.ScorePlayer2 = result.ScorePlayer2;
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Resultados atualizados com sucesso!";
            return RedirectToAction(nameof(ManageResults));
        }
    // Geração de senha aleatória segura
    private string GenerateRandomPassword(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
        var password = new char[length];

        using var rng = RandomNumberGenerator.Create();
        for (int i = 0; i < length; i++)
        {
            byte[] randomByte = new byte[1];
            rng.GetBytes(randomByte);
            password[i] = chars[randomByte[0] % chars.Length];
        }

        return new string(password);
    }

    // Criptografia SHA256 da senha
    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
    }
}
