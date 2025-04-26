using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult TournamentDetails()
    {
    var tournaments = _context.Tournaments
        .Include(t => t.Players)
        .Include(t => t.Games)
        .ToList();

    if (tournaments == null || !tournaments.Any())
    {
        ViewBag.Message = "Nenhum torneio cadastrado.";
        return View(); // mostra a view vazia com mensagem
    }

    return View(tournaments);
}

    // Tela de importação de jogadores via CSV
    [HttpGet]
    public IActionResult ImportCsv()
    {
        return View(new ImportCsvViewModel { CsvFile = null! });
    }

    [HttpPost]
    public async Task<IActionResult> ImportCsv(ImportCsvViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var csvFile = model.CsvFile;

        if (csvFile == null || csvFile.Length == 0 || 
            (csvFile.ContentType != "text/csv" && !csvFile.FileName.EndsWith(".csv")))
        {
            model.ErrorMessage = "Arquivo CSV inválido.";
            return View(model);
        }

        try
        {
            var novosUsuarios = new List<(string UserName, string Password)>();

            var importBatch = new ImportBatch
            {
                FileName = "Importação_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
            };

            _context.ImportBatches.Add(importBatch);
            await _context.SaveChangesAsync();

            using var stream = new StreamReader(csvFile.OpenReadStream());
            var headerLine = await stream.ReadLineAsync();
            if (string.IsNullOrEmpty(headerLine))
            {
                model.ErrorMessage = "Cabeçalho do arquivo CSV está ausente.";
                return View(model);
            }

            while (!stream.EndOfStream)
            {
                var line = (await stream.ReadLineAsync())?.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var columns = line.Split(',');
                if (columns.Length < 8) continue;

                // Limpeza e validação dos dados
                string rawRatingsId = columns[1].Trim().Replace("\"", "");
                string rawRating = columns[4].Trim().Replace("\"", "");
                string rawStDev = columns[5].Trim().Replace("\"", "");

                if (!int.TryParse(rawRatingsId, out int ratingsCentralId) ||
                    !int.TryParse(rawRating, out int rating) ||
                    !int.TryParse(rawStDev, out int stDev))
                {
                    // Se qualquer um dos dados essenciais for inválido, pula a linha
                    continue;
                }

                string name = columns[3].Trim().Replace("\"", "");
                string group = columns[7].Trim().Replace("\"", "");

                var existingPlayer = await _context.Players
                    .Include(p => p.User)
                    .FirstOrDefaultAsync(p => p.RatingsCentralId == ratingsCentralId);

                if (existingPlayer != null)
                {
                    existingPlayer.Name = name;
                    existingPlayer.Rating = rating;
                    existingPlayer.StDev = stDev;
                    existingPlayer.Group = group;
                    existingPlayer.ImportBatchId = importBatch.Id;
                }
                else
                {
                    string userName = "player" + ratingsCentralId;
                    string plainPassword = GenerateRandomPassword(8);
                    string passwordHash = HashPassword(plainPassword);

                    var user = new User
                    {
                        UserName = userName,
                        PasswordHash = passwordHash,
                        Role = "Player"
                    };

                    var player = new Player
                    {
                        Name = name,
                        RatingsCentralId = ratingsCentralId,
                        Rating = rating,
                        StDev = stDev,
                        Group = group,
                        User = user,
                        ImportBatchId = importBatch.Id,
                        ImportBatch = importBatch
                    };

                    _context.Players.Add(player);
                    novosUsuarios.Add((userName, plainPassword));
                }
            }

            await _context.SaveChangesAsync();

            model.SuccessMessage = $"Importação concluída com sucesso! Descrição: {model.Description}";
            TempData["NovosUsuarios"] = System.Text.Json.JsonSerializer.Serialize(novosUsuarios);
        }
        catch (Exception ex)
        {
            model.ErrorMessage = $"Erro ao processar o arquivo CSV: {ex.Message}";
        }

        return View(model);
    }
    // Gerenciar jogadores
    [HttpGet]
    public IActionResult ManagePlayers()
    {
        var players = _context.Players.ToList();
        return View(players);
    }

    // Exporta os usuários recém-criados como CSV
    [HttpGet]
    public IActionResult ExportarUsuariosCsv()
    {
        if (!TempData.ContainsKey("NovosUsuarios"))
        {
            TempData["Error"] = "Nenhum usuário novo para exportar.";
            return RedirectToAction("ImportCsv");
        }

        var json = TempData["NovosUsuarios"] as string;
        var novosUsuarios = System.Text.Json.JsonSerializer.Deserialize<List<(string UserName, string Password)>>(json!);

        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("UserName,Password");

        foreach (var usuario in novosUsuarios!)
            csvBuilder.AppendLine($"{usuario.UserName},{usuario.Password}");

        var bytes = Encoding.UTF8.GetBytes(csvBuilder.ToString());
        return File(bytes, "text/csv", "novos_usuarios.csv");
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

    // Gerenciar torneios
    [HttpGet]
    public IActionResult ManageTournaments()
    {
        var tournaments = _context.Tournaments
            .Include(t => t.Players)
            .ToList();
        return View(tournaments);
    }

    // Criar torneio
    [HttpGet]
    public IActionResult CreateTournament()
    {
        var viewModel = new CreateTournamentViewModel
        {
            ImportBatchOptions = _context.ImportBatches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = $"Importação #{b.Id} - {b.ImportedAt.ToShortDateString()}"
                })
                .ToList()
        };

        return View(viewModel);
    }

    // Escolher Torneio
    [HttpGet]
    public IActionResult ChooseTournament()
    {
        // Obtém o ID do admin logado (convertido para int)
        var adminIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(adminIdString, out var adminId))
        {
            // Filtra os torneios criados pelo admin logado e que tenham jogadores associados
            var tournaments = _context.Tournaments
                .Include(t => t.ImportBatch) // Inclui o ImportBatch associado ao torneio
                .Where(t => t.AdminUserId == adminId && t.Players.Any()) // Filtra pelos torneios do admin logado
                .ToList();

            return View(tournaments);
        }
        
        // Se a conversão falhar, redireciona para uma página de erro ou outro tratamento de erro
        return RedirectToAction("Error", "Home"); // Exemplo de redirecionamento para uma página de erro
    }

    [HttpPost]
    public IActionResult SaveResults(List<GameResultViewModel> results)
    {
        if (results == null || results.Count == 0)
        {
            return BadRequest("Nenhum resultado foi fornecido.");
        }

        foreach (var result in results)
        {
            var game = _context.Games.FirstOrDefault(g => g.Id == result.GameId);
            if (game != null)
            {
                game.ScorePlayer1 = result.ScorePlayer1;
                game.ScorePlayer2 = result.ScorePlayer2;
                _context.SaveChanges();
            }
        }

        return RedirectToAction("ChooseTournament");
    }

    [HttpPost]
    public async Task<IActionResult> CreateTournament(Tournament tournament)
    {
        if (ModelState.IsValid)
        {
            tournament.StartDate = DateTime.Now;
            tournament.EndDate = DateTime.Now.AddDays(7);

            _context.Tournaments.Add(tournament);
            await _context.SaveChangesAsync();

            await CriarJogosParaTorneio(tournament.Id);
            return RedirectToAction(nameof(ManageTournaments));
        }

        return View(tournament);
    }

    // Criar confrontos automaticamente por grupo
    private async Task CriarJogosParaTorneio(int tournamentId)
    {
        var players = await _context.Players
            .Where(p => p.TournamentId == tournamentId)
            .ToListAsync();

        var groups = players.GroupBy(p => p.Group);

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

                    _context.Games.Add(game);
                }
            }
        }

        await _context.SaveChangesAsync();
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

    // Gerenciar resultados das partidas
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


    [HttpPost]
    public async Task<IActionResult> UpdateGameResults(List<GameResultViewModel> gameResults)
    {
        foreach (var result in gameResults)
        {
            var game = await _context.Games.FindAsync(result.GameId);
            if (game != null)
            {
                game.ScorePlayer1 = result.ScorePlayer1;
                game.ScorePlayer2 = result.ScorePlayer2;
            }
        }

        await _context.SaveChangesAsync();
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
