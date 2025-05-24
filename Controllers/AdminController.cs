using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
            var tournament = await _context.Tournaments.FindAsync(model.TournamentId);
            if (tournament == null)
            {
                ModelState.AddModelError("", "Torneio não encontrado.");
                return View(model);
            }

            var players = ParseCsv(model.CsvFile, model.TournamentId); // Remove TournamentId aqui também
            var games = new List<Game>();

            foreach (var player in players)
            {
                // Verifica se já existe um Player com o mesmo RatingsCentralId
                var existingPlayer = await _context.Players
                    .FirstOrDefaultAsync(p => p.RatingsCentralId == player.RatingsCentralId);

                if (existingPlayer == null)
                {
                    _context.Players.Add(player);
                    await _context.SaveChangesAsync(); // Salva para ter o Id gerado

                    // Vincula ao torneio
                    _context.Add(new TournamentPlayer
                    {
                        PlayerId = player.Id,
                        TournamentId = tournament.Id
                    });
                }
                else
                {
                    // Atualiza dados do jogador
                    existingPlayer.Name = player.Name;
                    existingPlayer.Group = player.Group;
                    existingPlayer.Rating = player.Rating;
                    existingPlayer.StDev = player.StDev;

                    // Verifica se já está vinculado ao torneio
                    bool isLinked = await _context.TournamentPlayers
                        .AnyAsync(tp => tp.PlayerId == existingPlayer.Id && tp.TournamentId == tournament.Id);

                    if (!isLinked)
                    {
                        _context.Add(new TournamentPlayer
                        {
                            PlayerId = existingPlayer.Id,
                            TournamentId = tournament.Id
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();

            // Obtém os jogadores vinculados a esse torneio (com grupo)
            var savedPlayers = await _context.TournamentPlayers
                .Include(tp => tp.Player)
                .Where(tp => tp.TournamentId == model.TournamentId)
                .Select(tp => tp.Player)
                .ToListAsync();

            // Agrupa por grupo e gera os confrontos
            var groups = savedPlayers.GroupBy(p => p!.Group);

            foreach (var group in groups)
            {
                var groupPlayers = group.ToList();
                for (int i = 0; i < groupPlayers.Count; i++)
                {
                    for (int j = i + 1; j < groupPlayers.Count; j++)
                    {
                        games.Add(new Game
                        {
                            Player1Id = groupPlayers[i]!.Id,
                            Player2Id = groupPlayers[j]!.Id,
                            TournamentId = tournament.Id,
                            Group = group.Key,
                            Date = DateTime.Now
                        });
                    }
                }
            }

            await _context.Games.AddRangeAsync(games);
            await _context.SaveChangesAsync();

            model.SuccessMessage = "Jogadores importados e confrontos criados com sucesso!";
            return RedirectToAction("ManageResults", new { tournamentId = model.TournamentId });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", $"Erro ao processar o arquivo CSV: {ex.Message}");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateGames(int tournamentId)
    {
        var players = await _context.TournamentPlayers
            .Where(tp => tp.TournamentId == tournamentId)
            .Select(tp => tp.Player)
            .ToListAsync();

        var groups = players.GroupBy(p => p!.Group);
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
                        Player1Id = groupPlayers[i]!.Id,
                        Player2Id = groupPlayers[j]!.Id,
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

        return RedirectToAction("ManageResults", new { tournamentId = tournamentId });
    }
    private List<Player> ParseCsv(IFormFile csvFile, int tournamentId)
    {
        var players = new List<Player>();
        var random = new Random();

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

                // Usa Regex para dividir CSV com campos entre aspas
                var values = Regex.Matches(line, @"(?:^|,)(?:""(?<val>[^""]*)""|(?<val>[^,]*))")
                                .Cast<Match>()
                                .Select(m => m.Groups["val"].Value.Trim())
                                .ToArray();

                if (values.Length < 8)
                {
                    Console.WriteLine($"Linha {lineNumber} ignorada: número insuficiente de colunas.");
                    continue;
                }

                if (!int.TryParse(values[1], out int ratingsCentralId))
                {
                    Console.WriteLine($"Linha {lineNumber} ignorada: RatingsCentralId inválido.");
                    continue;
                }

                // Rating
                int rating = 0;
                if (!string.Equals(values[4], "NA", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(values[4], out rating);

                // StDev
                int stDev = 0;
                if (!string.Equals(values[5], "NA", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(values[5], out stDev);

                var userName = $"player{ratingsCentralId}";
                var plainPassword = $"senha{random.Next(1000, 9999)}";
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);

                var user = new User
                {
                    UserName = userName,
                    PasswordHash = passwordHash,
                    Role = "Player"
                };

                var player = new Player
                {
                    Name = values[3], // Nome completo (ex: "Hemen Estefaie")
                    RatingsCentralId = ratingsCentralId,
                    Rating = rating,
                    StDev = stDev,
                    Group = values[7],
                    User = user
                };

                Console.WriteLine($"Adicionando jogador: {player.Name}, RCID: {ratingsCentralId}, Grupo: {player.Group}");

                players.Add(player);
            }
        }

        return players;
    }

    [HttpGet]
    public IActionResult ManageResults(int tournamentId)
    {
        try
        {
            var games = _context.Games
                .Where(g => g.TournamentId == tournamentId)
                .Include(g => g.Player1)
                .Include(g => g.Player2)
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
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return View("Error");
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

    [HttpGet]
    public IActionResult TournamentDetails(int id)
    {
        var tournament = _context.Tournaments
            .Include(t => t.TournamentPlayers)
                .ThenInclude(tp => tp.Player)      // Inclui os jogadores dentro da tabela associativa
            .Include(t => t.Games).ThenInclude(g => g.Player1)
            .Include(t => t.Games).ThenInclude(g => g.Player2)
            .FirstOrDefault(t => t.Id == id);

        if (tournament == null)
            return NotFound();

        return View("TournamentDetails", tournament);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGameResults(List<GameResultViewModel> gameResults)
    {
        foreach (var result in gameResults)
        {
            if (result.ScorePlayer1 < 0 || result.ScorePlayer2 < 0)
                continue; // Ignora pontuações inválidas

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

        [HttpGet]
    public IActionResult EditTournament(int tournamentId)
    {
        var tournament = _context.Tournaments
            .Include(t => t.TournamentPlayers)
                .ThenInclude(tp => tp.Player)
            .Include(t => t.Games)
            .FirstOrDefault(t => t.Id == tournamentId);

        if (tournament == null || tournament.TournamentPlayers == null || tournament.Games == null)
            return NotFound();

        var model = new TournamentViewModel
        {
            TournamentId = tournament.Id,
            TournamentName = tournament.Name!,
            Players = tournament.TournamentPlayers
                .Where(tp => tp.Player != null)
                .Select(tp => tp.Player!)
                .OrderBy(p => p.Name)
                .ToList(),
            Games = tournament.Games.ToList()
        };

        return View(model);
    }
    
        [HttpPost]
        [ValidateAntiForgeryToken]
    public IActionResult EditTournament(IFormCollection form)
    {
        if (!int.TryParse(form["TournamentId"], out int tournamentId))
            return BadRequest();

        var gamesInDb = _context.Games.Where(g => g.TournamentId == tournamentId).ToList();

        foreach (var key in form.Keys)
        {
            if (key.StartsWith("Games[") && key.EndsWith("].Player1Id"))
            {
                var idPart = key.Substring(6, key.Length - 6 - 12); // extrai 1_2 de Games[1_2].Player1Id

                var player1Id = int.Parse(form[$"Games[{idPart}].Player1Id"]);
                var player2Id = int.Parse(form[$"Games[{idPart}].Player2Id"]);
                var score1 = int.Parse(form[$"Games[{idPart}].ScorePlayer1"]);
                var score2 = int.Parse(form[$"Games[{idPart}].ScorePlayer2"]);

                var game = gamesInDb.FirstOrDefault(g =>
                    (g.Player1Id == player1Id && g.Player2Id == player2Id) ||
                    (g.Player1Id == player2Id && g.Player2Id == player1Id));

                if (game != null)
                {
                    game.Player1Id = player1Id;
                    game.Player2Id = player2Id;
                    game.ScorePlayer1 = score1;
                    game.ScorePlayer2 = score2;
                    game.Date = DateTime.Now;
                }
            }
        }

        _context.SaveChanges();
        TempData["SuccessMessage"] = "Partidas atualizadas com sucesso!";
        return RedirectToAction("TournamentDetails", new { id = tournamentId });
    }

}