using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TorneioTenisMesa.Models;
using TorneioTenisMesa.Models.ViewModels;

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

            var playersFromCsv = ParseCsv(model.CsvFile);

            // Carregar Players existentes do torneio em memória para evitar várias consultas
            var existingPlayers = await _context.Players
                .Include(p => p.User)
                .ToListAsync();

            var tournamentPlayers = await _context.TournamentPlayers
                .Where(tp => tp.TournamentId == tournament.Id)
                .ToListAsync();

            var newPlayersToAdd = new List<Player>();
            var newTournamentPlayers = new List<TournamentPlayer>();

            foreach (var player in playersFromCsv)
            {
                var existingPlayer = existingPlayers.FirstOrDefault(p => p.RatingsCentralId == player.RatingsCentralId);

                if (existingPlayer == null)
                {
                    // Novo jogador + novo usuário
                    newPlayersToAdd.Add(player);
                }
                else
                {
                    // Atualizar dados do jogador existente
                    existingPlayer.Name = player.Name;
                    existingPlayer.Group = player.Group;
                    existingPlayer.Rating = player.Rating;
                    existingPlayer.StDev = player.StDev;

                    // Verifica se já está vinculado ao torneio
                    bool isLinked = tournamentPlayers.Any(tp => tp.PlayerId == existingPlayer.Id);

                    if (!isLinked)
                    {
                        newTournamentPlayers.Add(new TournamentPlayer
                        {
                            PlayerId = existingPlayer.Id,
                            TournamentId = tournament.Id
                        });
                    }
                }
            }

            // Adiciona os novos jogadores e seus usuários
            if (newPlayersToAdd.Any())
            {
                await _context.Players.AddRangeAsync(newPlayersToAdd);
                await _context.SaveChangesAsync();

                foreach (var player in newPlayersToAdd)
                {
                    newTournamentPlayers.Add(new TournamentPlayer
                    {
                        PlayerId = player.Id,
                        TournamentId = tournament.Id
                    });
                }
            }

            if (newTournamentPlayers.Any())
            {
                await _context.TournamentPlayers.AddRangeAsync(newTournamentPlayers);
                await _context.SaveChangesAsync();
            }

            // Agora, recupera os jogadores vinculados ao torneio para gerar os jogos
            var savedPlayers = await _context.TournamentPlayers
                .Include(tp => tp.Player)
                .Where(tp => tp.TournamentId == tournament.Id)
                .Select(tp => tp.Player)
                .ToListAsync();

            var games = new List<Game>();

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

            if (games.Any())
            {
                await _context.Games.AddRangeAsync(games);
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "Jogadores importados e confrontos criados com sucesso!";
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

    private List<Player> ParseCsv(IFormFile csvFile)
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

                var values = Regex.Matches(line, @"(?:^|,)(?:""(?<val>[^""]*)""|(?<val>[^,]*))")
                                .Cast<Match>()
                                .Select(m => m.Groups["val"].Value.Trim())
                                .ToArray();

                if (values.Length < 8)
                {
                    // Pode usar logs apropriados
                    // LogWarning($"Linha {lineNumber} ignorada: número insuficiente de colunas.");
                    continue;
                }

                if (!int.TryParse(values[1], out int ratingsCentralId))
                {
                    // LogWarning($"Linha {lineNumber} ignorada: RatingsCentralId inválido.");
                    continue;
                }

                int rating = 0;
                if (!string.Equals(values[4], "NA", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(values[4], out rating);

                int stDev = 0;
                if (!string.Equals(values[5], "NA", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(values[5], out stDev);

                var userName = $"player{ratingsCentralId}";
                var plainPassword = GenerateRandomPassword(8);
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);

                var user = new User
                {
                    UserName = userName,
                    PasswordHash = passwordHash,
                };

                var player = new Player
                {
                    Name = values[3],
                    RatingsCentralId = ratingsCentralId,
                    Rating = rating,
                    StDev = stDev,
                    Group = values[7],
                    User = user
                };

                players.Add(player);
            }
        }

        return players;
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
