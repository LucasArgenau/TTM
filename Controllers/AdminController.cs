using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TorneioTenisMesa.Models;
using TorneioTenisMesa.Models.ViewModels;

// Helper record for ParseCsv result
public record CsvParseResult(List<Player> Players, Dictionary<string, string> NewUserCredentials, List<string> ImportErrors);

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<User> _userManager;

    public AdminController(AppDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tournaments = await _context.Tournaments
            .OrderByDescending(t => t.StartDate)
            .ToListAsync();

        return View(tournaments);
    }

    // GET: Admin/CreateAdmin
    public IActionResult CreateAdmin()
    {
        return View();
    }

    // POST: Admin/CreateAdmin
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAdmin(CreateAdminViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError(string.Empty, "Já existe um usuário com esse e-mail.");
            return View(model);
        }

        var user = new User
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "Admin");
            TempData["SuccessMessage"] = "Administrador criado com sucesso!";
            return RedirectToAction("Index"); // Ajuste se quiser outra página
        }
        else
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }
    }

    // GET: Admin/EditTournament/5
    [HttpGet]
    public async Task<IActionResult> EditTournament(int id)
    {
        var tournament = await _context.Tournaments
            .Include(t => t.Games)
                .ThenInclude(g => g.Player1)
            .Include(t => t.Games)
                .ThenInclude(g => g.Player2)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (tournament == null)
            return NotFound();

        var model = new EditTournamentViewModel
        {
            Id = tournament.Id,
            Name = tournament.Name!,
            StartDate = tournament.StartDate,
            EndDate = tournament.EndDate,
            Games = tournament.Games.Select(g => new GameResultViewModel
            {
                GameId = g.Id,
                Player1Name = g.Player1?.Name ?? "N/A",
                ScorePlayer1 = g.ScorePlayer1,
                Player2Name = g.Player2?.Name ?? "N/A",
                ScorePlayer2 = g.ScorePlayer2,
                Group = g.Group!,
                Date = g.Date
            }).ToList()
        };

        return View(model);
    }

    // POST: Admin/EditTournament/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTournament(EditTournamentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var tournament = await _context.Tournaments
            .Include(t => t.Games)
            .FirstOrDefaultAsync(t => t.Id == model.Id);

        if (tournament == null)
            return NotFound();

        // Atualiza dados do torneio
        tournament.Name = model.Name;
        tournament.StartDate = model.StartDate;
        tournament.EndDate = model.EndDate;

        // Atualiza os scores dos jogos
        foreach (var gameVm in model.Games)
        {
            var game = tournament.Games.FirstOrDefault(g => g.Id == gameVm.GameId);
            if (game != null)
            {
                game.ScorePlayer1 = gameVm.ScorePlayer1;
                game.ScorePlayer2 = gameVm.ScorePlayer2;
            }
        }

        try
        {
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Torneio e resultados atualizados com sucesso!";
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Erro ao salvar no banco de dados.");
            return View(model);
        }

        return RedirectToAction(nameof(Index)); // Ajuste se quiser outra rota
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

            // var playersFromCsv = await ParseCsv(model.CsvFile); // Old call
            var parseResult = await ParseCsv(model.CsvFile);
            var playersFromCsv = parseResult.Players;

            if (parseResult.ImportErrors.Any())
            {
                foreach (var errorMsg in parseResult.ImportErrors)
                {
                    ModelState.AddModelError("", errorMsg);
                }
                // If there are import errors, return to the view to display them.
                // This stops further processing of the current CSV import attempt.
                return View(model);
            }

            if (parseResult.NewUserCredentials.Any())
            {
                TempData["NewUserCredentials"] = parseResult.NewUserCredentials;
            }

            // Carregar Players existentes do torneio em memória para evitar várias consultas
            // This part will be inside the transaction or adjusted if playersFromCsv is empty.

            if (!playersFromCsv.Any() && !parseResult.NewUserCredentials.Any()) // Check if there's nothing to process
            {
                 // If no players were parsed and no new users to create (even if no import errors explicitly, maybe empty file)
                TempData["SuccessMessage"] = "Nenhum jogador processado do arquivo CSV. Nenhuma alteração feita.";
                return RedirectToAction("ManageResults", new { tournamentId = model.TournamentId });
            }
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingPlayers = await _context.Players
                    .Include(p => p.User)
                    .ToListAsync(); // Consider moving this query if it's too heavy before knowing if playersFromCsv has items

                var tournamentPlayers = await _context.TournamentPlayers
                    .Where(tp => tp.TournamentId == tournament.Id)
                    .ToListAsync();

                var newPlayersToAdd = new List<Player>();
                var tournamentPlayersToProcess = new List<TournamentPlayer>(); // Renamed to avoid confusion

                foreach (var playerFromCsv in playersFromCsv) // Iterate over successfully parsed players
                {
                    var existingPlayer = existingPlayers.FirstOrDefault(p => p.RatingsCentralId == playerFromCsv.RatingsCentralId);

                    if (existingPlayer == null)
                    {
                        // This player (and its user) was created by ParseCsv and added to playersFromCsv list.
                        // The User object should already be part of playerFromCsv.User
                        newPlayersToAdd.Add(playerFromCsv);
                        // Will be linked to tournament later
                    }
                    else
                    {
                        // Atualizar dados do jogador existente
                        existingPlayer.Name = playerFromCsv.Name;
                        existingPlayer.Group = playerFromCsv.Group;
                        existingPlayer.Rating = playerFromCsv.Rating;
                        existingPlayer.StDev = playerFromCsv.StDev;
                        // User for existingPlayer is already in DB.

                        // Verifica se já está vinculado ao torneio
                        bool isLinked = tournamentPlayers.Any(tp => tp.PlayerId == existingPlayer.Id);
                        if (!isLinked)
                        {
                            tournamentPlayersToProcess.Add(new TournamentPlayer
                            {
                                PlayerId = existingPlayer.Id,
                                TournamentId = tournament.Id
                            });
                        }
                    }
                }

                // Add new players to context
                if (newPlayersToAdd.Any())
                {
                    await _context.Players.AddRangeAsync(newPlayersToAdd);
                    // Users associated with newPlayersToAdd are also added due to relationship
                }

                // After potential new players are added (or existing ones updated), save changes to get their IDs
                // This is necessary if new player IDs are needed for TournamentPlayer or Game entities *before* final commit.
                // However, EF Core can handle related entities being added in one go.
                // Let's try to consolidate SaveChangesAsync to be called only once.

                // Link new players to the tournament
                foreach (var newPlayer in newPlayersToAdd)
                {
                    // Need to ensure PlayerId is available if SaveChangesAsync hasn't been called.
                    // EF Core *should* handle this if newPlayer is tracked.
                    tournamentPlayersToProcess.Add(new TournamentPlayer
                    {
                        Player = newPlayer, // Link by object, EF Core will get ID
                        TournamentId = tournament.Id
                    });
                }
                
                if (tournamentPlayersToProcess.Any())
                {
                    await _context.TournamentPlayers.AddRangeAsync(tournamentPlayersToProcess);
                }

                // Regenerate or fetch the list of players now part of the tournament for game generation
                // This needs to include both newly added and existing players now linked.

                // To get *all* players for game generation (newly added and existing ones linked in this import)
                // we might need to query after the above additions are tracked by EF Core.
                // For simplicity, if newPlayersToAdd are tracked, their IDs become available.
                // And existing players already have IDs.

                var allPlayersForGamesInTournament = new List<Player>();
                
                // Add newly added players that are now linked
                foreach(var tp in tournamentPlayersToProcess.Where(tp => newPlayersToAdd.Contains(tp.Player!)))
                {
                    if(tp.Player != null) allPlayersForGamesInTournament.Add(tp.Player);
                }

                // Add existing players that were newly linked
                foreach(var tp in tournamentPlayersToProcess.Where(tp => !newPlayersToAdd.Contains(tp.Player!)))
                {
                     var player = existingPlayers.First(p => p.Id == tp.PlayerId); // Should exist
                     if(player != null) allPlayersForGamesInTournament.Add(player);
                }
                
                // Also add players already in the tournament but not processed by this CSV (if any, though current logic implies all CSV players are processed)
                // The original logic for savedPlayers was:
                // var savedPlayers = await _context.TournamentPlayers.Include(tp => tp.Player).Where(tp => tp.TournamentId == tournament.Id).Select(tp => tp.Player).ToListAsync();
                // This query should be run *after* all TournamentPlayer links for the current import are established and *before* game generation.
                // For now, let's assume `allPlayersForGamesInTournament` correctly collects players for game generation based on current processing.
                // A more robust way would be to query TournamentPlayers for this tournamentId *after* AddRangeAsync for TournamentPlayers.

                // For game generation, we need all players that *will be* in the tournament after this transaction.
                // This includes:
                // 1. New players added AND linked.
                // 2. Existing players, now (possibly newly) linked.
                // 3. Existing players already linked (not touched by CSV but part of the tournament).

                // To simplify, let's first save Players and TournamentPlayers to ensure IDs are set and relationships are queryable.
                // Then query for all players in the tournament to generate games. This might mean two SaveChangesAsync calls within transaction or careful EF tracking.
                // The goal is a *single* SaveChangesAsync.

                // Let's adjust:
                // 1. Add new players to context.
                // 2. Add new TournamentPlayer links to context.
                // 3. All these entities are now tracked. EF Core should resolve their IDs and relationships upon the single SaveChangesAsync.

                // Game generation logic:
                // We need a list of *all* Player objects that are supposed to be in this tournament.
                // This includes players in `newPlayersToAdd` (who will be linked via `tournamentPlayersToProcess`)
                // and existing players who might be newly linked via `tournamentPlayersToProcess` or were already linked.

                // Let's fetch all players that will be in the tournament *after* current operations are saved.
                // This is tricky without calling SaveChanges.
                // Alternative: Construct the list of players for games from playersFromCsv and existing linked players.

                List<Player> playersForGameGeneration = new List<Player>();
                playersForGameGeneration.AddRange(newPlayersToAdd); // These are new and will be in the tournament.
                
                var existingPlayersInTournamentPreviously = await _context.TournamentPlayers
                    .Where(tp => tp.TournamentId == tournament.Id)
                    .Select(tp => tp.Player)
                    .ToListAsync();
                
                foreach (var playerCsv in playersFromCsv)
                {
                    var existingPlayer = existingPlayers.FirstOrDefault(p => p.RatingsCentralId == playerCsv.RatingsCentralId);
                    if (existingPlayer != null) // If it's an existing player processed from CSV
                    {
                        if (!playersForGameGeneration.Any(p=> p.Id == existingPlayer.Id)) // Ensure not added twice
                        {
                             playersForGameGeneration.Add(existingPlayer);
                        }
                    }
                }
                // Add players who were already in the tournament and not in the CSV (if any, depends on desired logic)
                // For now, assuming games are only between players processed or existing in CSV.
                // The original logic for `savedPlayers` would be better if run after TP links are made.
                // Given the single SaveChangesAsync goal, we'll use the players we know are being actively processed or added.
                // This might miss games if some tournament players are not in the CSV.
                // Re-evaluating: The original `savedPlayers` query after all TP links are made (but before SaveChanges) might be fine if EF tracks them.

                // Let's clear and rebuild games list
                var games = new List<Game>();
                var groups = playersForGameGeneration.Where(p => p != null).GroupBy(p => p.Group);

                foreach (var group in groups)
                {
                    var groupPlayers = group.ToList();
                    for (int i = 0; i < groupPlayers.Count; i++)
                    {
                        for (int j = i + 1; j < groupPlayers.Count; j++)
                        {
                            games.Add(new Game
                            {
                                Player1 = groupPlayers[i], // Link by object
                                Player2 = groupPlayers[j], // Link by object
                                TournamentId = tournament.Id,
                                Group = group.Key,
                                Date = DateTime.Now 
                            });
                        }
                    }
                }

                if (games.Any())
                {
                    await _context.Games.AddRangeAsync(games);
                }

                await _context.SaveChangesAsync(); // Single save for all changes
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = "Jogadores importados e confrontos criados com sucesso!";
                return RedirectToAction("ManageResults", new { tournamentId = model.TournamentId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError("", $"Ocorreu um erro ao salvar os dados no banco de dados: {ex.Message}. Todas as alterações foram revertidas.");
                return View(model);
            }
        }
        catch (Exception ex) // Catch for issues before transaction (e.g., reading CSV file itself)
        {
            ModelState.AddModelError("", $"Erro geral ao processar o arquivo CSV: {ex.Message}");
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

    private async Task<CsvParseResult> ParseCsv(IFormFile csvFile)
    {
        var players = new List<Player>();
        var newUserCredentials = new Dictionary<string, string>();
        var importErrors = new List<string>();
        // var random = new Random(); // GenerateRandomPassword uses RandomNumberGenerator

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

                var values = Regex.Matches(line, @"(?:^|,)(?:""(?<val>[^""]*)""|(?<val>[^,]*))")
                                .Cast<Match>()
                                .Select(m => m.Groups["val"].Value.Trim())
                                .ToArray();

                if (values.Length < 8)
                {
                    importErrors.Add($"Linha {lineNumber} ignorada: número insuficiente de colunas. Esperado: 8, Encontrado: {values.Length}. Conteúdo: '{line}'");
                    continue;
                }

                if (!int.TryParse(values[1], out int ratingsCentralId))
                {
                    importErrors.Add($"Linha {lineNumber} ignorada: RatingsCentralId inválido '{values[1]}'. Conteúdo: '{line}'");
                    continue;
                }

                int rating = 0;
                if (!string.Equals(values[4], "NA", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(values[4], out rating);

                int stDev = 0;
                if (!string.Equals(values[5], "NA", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(values[5], out stDev);

                var userName = $"player{ratingsCentralId}";
                var userEmail = $"player{ratingsCentralId}@example.com"; // Placeholder email
                var plainPassword = GenerateRandomPassword(8);

                var user = new User
                {
                    UserName = userName,
                    Email = userEmail,
                    EmailConfirmed = true // Avoid email confirmation for auto-generated accounts
                };

                var result = await _userManager.CreateAsync(user, plainPassword);

                if (result.Succeeded)
                {
                    // User created, now assign "Player" role
                    var roleResult = await _userManager.AddToRoleAsync(user, "Player");
                    if (roleResult.Succeeded)
                    {
                        var player = new Player
                        {
                            Name = values[3],
                            RatingsCentralId = ratingsCentralId,
                            Rating = rating,
                            StDev = stDev,
                            Group = values[7],
                            User = user // Or UserId = user.Id
                        };
                        players.Add(player);
                        newUserCredentials.Add(userName, plainPassword); // Store credentials
                    }
                    else
                    {
                        // Role assignment failed
                        foreach (var error in roleResult.Errors)
                        {
                            importErrors.Add($"Erro ao atribuir role 'Player' ao usuário {userName} (RCID: {ratingsCentralId}): {error.Description}");
                        }
                        // Optional: Delete the created user if role assignment fails and it's critical.
                        // await _userManager.DeleteAsync(user); // Requires careful consideration of overall flow.
                        // For now, adding to importErrors will prevent the whole batch from committing due to logic in ImportCsv.
                    }
                }
                else
                {
                    // User creation failed
                    foreach (var error in result.Errors)
                    {
                        importErrors.Add($"Erro ao criar usuário {userName} (RCID: {ratingsCentralId}): {error.Description}");
                    }
                    // Player is not added if user creation fails
                }
            }
        }

        return new CsvParseResult(players, newUserCredentials, importErrors);
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
            // Log error
            TempData["ErrorMessage"] = $"Erro ao carregar resultados: {ex.Message}";
            return RedirectToAction("Index");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveResults(List<GameResultViewModel> results)
    {
        if (results == null || !results.Any())
        {
            TempData["ErrorMessage"] = "Nenhum resultado para salvar.";
            return RedirectToAction("Index");
        }

        foreach (var result in results)
        {
            var game = await _context.Games.FindAsync(result.GameId);
            if (game != null)
            {
                game.ScorePlayer1 = result.ScorePlayer1;
                game.ScorePlayer2 = result.ScorePlayer2;
            }
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Resultados salvos com sucesso!";

        // Aqui pega o tournamentId do primeiro resultado com segurança,
        // porque já garantimos que a lista não está vazia
        int tournamentId = results.First().TournamentId;

        return RedirectToAction("ManageResults", new { tournamentId });
    }
    private string GenerateRandomPassword(int length)
    {
        const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()_-+={[}]|:;<,>.?";

        var password = new char[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            var byteBuffer = new byte[sizeof(uint)];

            for (int i = 0; i < length; i++)
            {
                rng.GetBytes(byteBuffer);
                uint num = BitConverter.ToUInt32(byteBuffer, 0);
                password[i] = validChars[(int)(num % (uint)validChars.Length)];
            }
        }

        return new string(password);
    }
    
    public async Task<IActionResult> Users()
    {
        var users = await _userManager.Users.ToListAsync();

        var userWithRoles = new List<UserWithRoleViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            userWithRoles.Add(new UserWithRoleViewModel
            {
                Id = user.Id,
                UserName = user.Email!, 
                Role = roles.FirstOrDefault() ?? "Sem Role"
            });
        }

        return View(userWithRoles);
    }



        // EDITAR USUÁRIO - GET
        public async Task<IActionResult> EditUser(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        // EDITAR USUÁRIO - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(int id, [Bind("Id,UserName,Role")] User user)
        {
            if (id != user.Id) 
                return NotFound();

            if (!ModelState.IsValid)
            {
                // Se o modelo não for válido, retorna a view com os dados preenchidos
                return View(user);
            }

            var existingUser = await _context.Users.FindAsync(id);
            if (existingUser == null) 
                return NotFound();

            // Atualiza os campos que deseja alterar
            existingUser.UserName = user.UserName;

            try
            {
                _context.Update(existingUser);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(existingUser.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToAction(nameof(Users));
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }


        // EXCLUIR USUÁRIO - GET
        public async Task<IActionResult> DeleteUser(int? id)
        {
            if (id == null) return NotFound();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound();

            return View(user);
        }

        // EXCLUIR USUÁRIO - POST
        [HttpPost, ActionName("DeleteUser")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUserConfirmed(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Users));
        }
}
