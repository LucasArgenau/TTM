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
                var playersToLinkToTournament = new List<Player>();

                foreach (var playerFromCsv in playersFromCsv)
                {
                    var existingDbPlayer = await _context.Players
                                                     .Include(p => p.User)
                                                     .FirstOrDefaultAsync(p => p.RatingsCentralId == playerFromCsv.RatingsCentralId);

                    if (existingDbPlayer != null)
                    {
                        // Update existing player's details
                        existingDbPlayer.Name = playerFromCsv.Name;
                        existingDbPlayer.Group = playerFromCsv.Group;
                        existingDbPlayer.Rating = playerFromCsv.Rating;
                        existingDbPlayer.StDev = playerFromCsv.StDev;
                        // User association: existingDbPlayer.User is already loaded. 
                        // playerFromCsv.User was determined by ParseCsv. If existingDbPlayer.User.UserName 
                        // is different from playerFromCsv.User.UserName, it implies a potential change 
                        // in user association, which is complex and not explicitly handled here.
                        // For now, we assume the RCID is the primary key for a player, and its user association is stable.
                        playersToLinkToTournament.Add(existingDbPlayer);
                    }
                    else
                    {
                        // playerFromCsv is a new player. Its User property is already set by ParseCsv.
                        // If playerFromCsv.User is new (not tracked by UserManager yet, or tracked but new to DB context), 
                        // EF Core will also add it due to the relationship when _context.Players.AddAsync(playerFromCsv) is called.
                        // If playerFromCsv.User is an existing user (tracked by UserManager and possibly by DB context),
                        // EF Core will correctly associate it.
                        await _context.Players.AddAsync(playerFromCsv); // EF Core tracks playerFromCsv and its related User.
                        playersToLinkToTournament.Add(playerFromCsv);
                    }
                }

                // --- TournamentPlayer linking logic ---
                var tournamentPlayersToAdd = new List<TournamentPlayer>();
                var currentTournamentPlayerLinks = await _context.TournamentPlayers
                    .Where(tp => tp.TournamentId == tournament.Id)
                    .ToListAsync();

                foreach (var playerToLink in playersToLinkToTournament)
                {
                    bool isAlreadyLinked;
                    if (playerToLink.UserId == 0) // New player, not yet saved to DB
                    {
                        isAlreadyLinked = false;
                    }
                    else // Existing player, check if linked by Id
                    {
                        isAlreadyLinked = currentTournamentPlayerLinks.Any(tp => tp.PlayerId == playerToLink.UserId);
                    }

                    if (!isAlreadyLinked)
                    {
                        tournamentPlayersToAdd.Add(new TournamentPlayer
                        {
                            Player = playerToLink, // Link by object reference, EF handles ID.
                            TournamentId = tournament.Id
                        });
                    }
                }

                if (tournamentPlayersToAdd.Any())
                {
                    await _context.TournamentPlayers.AddRangeAsync(tournamentPlayersToAdd);
                }

                // --- Game generation logic ---
                // 1. Clear existing games for the tournament.
                var oldGames = await _context.Games.Where(g => g.TournamentId == tournament.Id).ToListAsync();
                if (oldGames.Any())
                {
                    _context.Games.RemoveRange(oldGames);
                }

                // 2. Construct the list of all players for game generation.
                // This includes players processed from CSV AND players already in the tournament but not in this CSV.
                var allPlayersForGames = new List<Player>();

                // Add players processed from CSV (they are now tracked by EF context, new ones will get Ids on SaveChanges)
                // Ensure they are distinct by RatingsCentralId as playerToLink could have duplicates if logic error elsewhere.
                foreach (var pLinked in playersToLinkToTournament.Where(p => p != null).DistinctBy(p => p.RatingsCentralId))
                {
                    allPlayersForGames.Add(pLinked);
                }

                // Add players already in the tournament (via TournamentPlayers) but NOT in the current CSV batch.
                var playerIdsFromCsvBeingProcessed = playersToLinkToTournament
                    .Where(p => p.UserId != 0) // Only consider players that have an ID
                    .Select(p => p.UserId)
                    .ToList();

                var existingPlayersInDbForTournament = await _context.TournamentPlayers
                    .Include(tp => tp.Player) // Ensure Player is included before Select
                    .Where(tp => tp.TournamentId == tournament.Id && !playerIdsFromCsvBeingProcessed.Contains(tp.PlayerId))
                    .Select(tp => tp.Player)
                    .ToListAsync();

                foreach (var existingPlayerInTournament in existingPlayersInDbForTournament)
                {
                    if (existingPlayerInTournament != null && !allPlayersForGames.Any(p => p.RatingsCentralId == existingPlayerInTournament.RatingsCentralId))
                    {
                        allPlayersForGames.Add(existingPlayerInTournament);
                    }
                }

                // 3. Generate new games
                var newGames = new List<Game>();
                var groups = allPlayersForGames.Where(p => p != null).GroupBy(p => p.Group);

                foreach (var group in groups)
                {
                    var groupPlayers = group.ToList();
                    for (int i = 0; i < groupPlayers.Count; i++)
                    {
                        for (int j = i + 1; j < groupPlayers.Count; j++)
                        {
                            // Ensure Player1 and Player2 are tracked entities if they are new.
                            // Linking by object (Player1 = groupPlayers[i]) is correct.
                            newGames.Add(new Game
                            {
                                Player1 = groupPlayers[i],
                                Player2 = groupPlayers[j],
                                TournamentId = tournament.Id,
                                Group = group.Key,
                                Date = DateTime.Now // Or tournament.StartDate
                            });
                        }
                    }
                }

                if (newGames.Any())
                {
                    await _context.Games.AddRangeAsync(newGames);
                }

                await _context.SaveChangesAsync(); // Single save for all changes (Users, Players, TournamentPlayers, Games)
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
                        Player1Id = groupPlayers[i]!.UserId,
                        Player2Id = groupPlayers[j]!.UserId,
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

        if (TempData.ContainsKey("NewUserCredentials"))
        {
            TempData.Keep("NewUserCredentials"); // Garante que o TempData ainda estará disponível após esse redirect
        }

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

                var userEmailForLookup = $"player{ratingsCentralId}@example.com"; // UserName will be this email

                User user = await _userManager.FindByNameAsync(userEmailForLookup); // Find by UserName
                bool userExisted = user != null;

                if (!userExisted)
                {
                    var plainPassword = GenerateRandomPassword(8);
                    user = new User
                    {
                        UserName = userEmailForLookup, // UserName is the email
                        Email = userEmailForLookup,    // Email is also the email
                        EmailConfirmed = true
                    };

                    var createUserResult = await _userManager.CreateAsync(user, plainPassword);

                    if (createUserResult.Succeeded)
                    {
                        var addToRoleResult = await _userManager.AddToRoleAsync(user, "Player");
                        if (!addToRoleResult.Succeeded)
                        {
                            importErrors.Add($"Linha {lineNumber}: Erro ao atribuir role 'Player' ao novo usuário {userEmailForLookup} (RCID: {ratingsCentralId}): {string.Join(", ", addToRoleResult.Errors.Select(e => e.Description))}");
                            // Optional: await _userManager.DeleteAsync(user);
                            continue; // Skip this player
                        }
                        newUserCredentials.Add(userEmailForLookup, plainPassword); // Key is userEmailForLookup (the UserName)
                    }
                    else
                    {
                        importErrors.Add($"Linha {lineNumber}: Erro ao criar usuário {userEmailForLookup} (RCID: {ratingsCentralId}): {string.Join(", ", createUserResult.Errors.Select(e => e.Description))}");
                        continue; // Skip this player
                    }
                }
                // If user existed or was successfully created and role assigned:
                var player = new Player
                {
                    Name = values[3], // Assuming column index 3 is Name
                    RatingsCentralId = ratingsCentralId,
                    Rating = rating,
                    StDev = stDev,
                    Group = values[7], // Assuming column index 7 is Group
                    User = user
                };
                players.Add(player);
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
        csvBuilder.AppendLine("GameDate,Division,Round,GameID,GameNO,Player 1,Player 1 RC ID,Player 2,Player 2 RC ID,Sets");

        foreach (var g in games)
        {
            csvBuilder.AppendLine($"{g.Date:dd/MM/yyyy}," +
                                  $"{g.Group}," +
                                  $"{g.Round + 1}," +
                                  $"{g.Id}," +
                                  $"{g.GameNo + 1}," +
                                  $"{g.Player1?.Name}," +
                                  $"{g.Player1?.RatingsCentralId}," +
                                  $"{g.Player2?.Name}," +
                                  $"{g.Player2?.RatingsCentralId}," +
                                  $"{g.ScorePlayer1}-{g.ScorePlayer2}");
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
                Date = g.Date,
                TournamentId = tournamentId
            }).ToList();

            ViewBag.NewUserCredentials = TempData["NewUserCredentials"] as Dictionary<string, string>;
            ViewBag.TournamentId = tournamentId;

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
    public async Task<IActionResult> SaveResults(List<GameResultViewModel> results, string action)
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

        int tournamentId = results.First().TournamentId;

        if (action == "finalizar")
        {
            // Aqui você pode executar alguma lógica extra de finalização, se quiser
            return RedirectToAction("Index"); // Vai para a tela principal do Admin
        }

        // Caso contrário, volta para a tela de gerenciamento de resultados
        return RedirectToAction("ManageResults", new { tournamentId });
    }

    private static char GetRandomChar(string charSet, RandomNumberGenerator rng)
    {
        if (string.IsNullOrEmpty(charSet))
            throw new ArgumentException("Character set cannot be null or empty", nameof(charSet));

        var byteBuffer = new byte[sizeof(uint)];
        rng.GetBytes(byteBuffer);
        uint num = BitConverter.ToUInt32(byteBuffer, 0);
        return charSet[(int)(num % (uint)charSet.Length)];
    }

    private static void Shuffle(List<char> list, RandomNumberGenerator rng)
    {
        int n = list.Count;
        while (n > 1)
        {
            var box = new byte[sizeof(uint)]; // Changed to sizeof(uint) for consistency with GetRandomChar
            rng.GetBytes(box);
            int k = (int)(BitConverter.ToUInt32(box, 0) % n--);
            (list[n], list[k]) = (list[k], list[n]); // Swap
        }
    }

    private string GenerateRandomPassword(int length)
    {
        if (length < 4) // Minimum length to include one of each of the four required types
            throw new ArgumentOutOfRangeException(nameof(length), "Password length must be at least 4 to meet complexity requirements (lower, upper, digit, special).");

        const string lowerChars = "abcdefghijklmnopqrstuvwxyz";
        const string upperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string digitChars = "0123456789";
        const string specialChars = "!@#$%^&*()_-+={[}]|:;<,>.?";
        const string allValidChars = lowerChars + upperChars + digitChars + specialChars;

        var passwordChars = new List<char>(length);

        using (var rng = RandomNumberGenerator.Create())
        {
            // 1. Add one of each required character type
            passwordChars.Add(GetRandomChar(lowerChars, rng));
            passwordChars.Add(GetRandomChar(upperChars, rng));
            passwordChars.Add(GetRandomChar(digitChars, rng));
            passwordChars.Add(GetRandomChar(specialChars, rng)); // Ensure a special character

            // 2. Add remaining characters from the full set of valid characters
            // If length is exactly 4, this loop won't run, which is correct.
            for (int i = 4; i < length; i++)
            {
                passwordChars.Add(GetRandomChar(allValidChars, rng));
            }

            // 3. Shuffle the password to ensure randomness in character positions
            Shuffle(passwordChars, rng);
        }
        return new string(passwordChars.ToArray());
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

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null) return NotFound();

        return View(user);
    }

    // EDITAR USUÁRIO - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(int id, [Bind("Id,UserName")] User userFromForm)
    {
        if (id != userFromForm.Id)
        {
            return NotFound();
        }

        var existingUser = await _userManager.FindByIdAsync(id.ToString());
        if (existingUser == null)
        {
            return NotFound();
        }

        string newProposedEmail = userFromForm.UserName;

        if (string.IsNullOrWhiteSpace(newProposedEmail))
        {
            ModelState.AddModelError("UserName", "Username/Email cannot be empty.");
            return View(existingUser);
        }

        // Check if the new email/username is different and if it's already taken
        bool emailChanged = !string.Equals(existingUser.Email, newProposedEmail, StringComparison.OrdinalIgnoreCase);
        bool userNameChanged = !string.Equals(existingUser.UserName, newProposedEmail, StringComparison.OrdinalIgnoreCase);

        if (emailChanged)
        {
            var userByNewEmail = await _userManager.FindByEmailAsync(newProposedEmail);
            if (userByNewEmail != null && userByNewEmail.Id != existingUser.Id)
            {
                ModelState.AddModelError("UserName", "This email is already in use by another account.");
                return View(existingUser);
            }
        }
        if (userNameChanged) // This check might be redundant if UserName is always Email, but good for robustness
        {
            var userByNewUserName = await _userManager.FindByNameAsync(newProposedEmail);
            if (userByNewUserName != null && userByNewUserName.Id != existingUser.Id)
            {
                ModelState.AddModelError("UserName", "This username is already in use by another account.");
                return View(existingUser);
            }
        }

        if (emailChanged)
        {
            var setEmailResult = await _userManager.SetEmailAsync(existingUser, newProposedEmail);
            if (!setEmailResult.Succeeded)
            {
                foreach (var error in setEmailResult.Errors) { ModelState.AddModelError(string.Empty, error.Description); }
                return View(existingUser);
            }
            existingUser.EmailConfirmed = true; // Admin change implies confirmation
        }

        if (userNameChanged)
        {
            var setUserNameResult = await _userManager.SetUserNameAsync(existingUser, newProposedEmail);
            if (!setUserNameResult.Succeeded)
            {
                // Potentially attempt to revert email change if critical, or ensure transactional behavior.
                // For now, add errors and return.
                foreach (var error in setUserNameResult.Errors) { ModelState.AddModelError(string.Empty, error.Description); }
                return View(existingUser);
            }
        }

        // Only call UpdateAsync if properties were actually changed by UserManager methods
        // or if other properties like EmailConfirmed were modified.
        if (emailChanged || userNameChanged)
        {
            var updateResult = await _userManager.UpdateAsync(existingUser);
            if (updateResult.Succeeded)
            {
                TempData["SuccessMessage"] = "User updated successfully.";
                return RedirectToAction(nameof(Users));
            }
            else
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(existingUser);
            }
        }
        else
        {
            // No actual changes to UserName or Email, so just redirect or inform user.
            // Or, if other properties could be changed on this form in future, this logic would differ.
            TempData["SuccessMessage"] = "No changes detected for user."; // Or "SuccessMessage" if preferred
            return RedirectToAction(nameof(Users));
        }
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
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return NotFound();
        }

        // Check if the user is a player in any tournament
        // Assumes Player.UserId links to User.Id and Player has TournamentPlayers collection
        var isPlayerInTournament = await _context.Players
            .AnyAsync(p => p.UserId == user.Id && p.TournamentPlayers.Any());

        if (isPlayerInTournament)
        {
            TempData["ErrorMessage"] = "You cannot delete a user who is currently part of a tournament.";
            return RedirectToAction(nameof(Users));
        }

        // If not in a tournament, proceed with deletion
        // 1. Delete associated Player record first
        var playerRecord = await _context.Players.FirstOrDefaultAsync(p => p.UserId == user.Id);
        if (playerRecord != null)
        {
            _context.Players.Remove(playerRecord);
            // It's generally better to ensure this succeeds before trying to delete the user.
            // A transaction spanning both operations would be ideal for atomicity.
            // For now, saving changes for player deletion separately.
            await _context.SaveChangesAsync();
        }

        // 2. Delete User via UserManager
        var deleteUserResult = await _userManager.DeleteAsync(user);
        if (deleteUserResult.Succeeded)
        {
            TempData["SuccessMessage"] = "User and any associated player data deleted successfully.";
        }
        else
        {
            var errorMessages = string.Join(", ", deleteUserResult.Errors.Select(e => e.Description));
            TempData["ErrorMessage"] = $"Error deleting user: {errorMessages}";
        }

        return RedirectToAction(nameof(Users));
    }
}