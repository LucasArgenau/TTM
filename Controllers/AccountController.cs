using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using TorneioTenisMesa.Models;
using TorneioTenisMesa.Models.ViewModels;
using TorneioTenisMesa.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    // GET: /Account/Login
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    // POST: /Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (ModelState.IsValid)
        {
            // Tentativa de login com as credenciais fornecidas
            var result = await _signInManager.PasswordSignInAsync(
                model.UserName, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                _logger.LogInformation("Usuário logado com sucesso.");

                var user = await _userManager.FindByNameAsync(model.UserName);
                if (user != null)
                {
                    // Verifica a role do usuário e redireciona para a página adequada
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                    {
                        return RedirectToAction("Index", "Admin");  // Redireciona para o Admin
                    }
                    else if (await _userManager.IsInRoleAsync(user, "Player"))
                    {
                        return RedirectToAction("Index", "Player");  // Redireciona para o Player
                    }
                }

                // Se o usuário não tiver as roles ou outra condição, redireciona para a Home
                return RedirectToLocal(returnUrl);
            }

            // Verificando se a conta foi bloqueada
            if (result.IsLockedOut)
            {
                _logger.LogWarning("Conta bloqueada.");
                ModelState.AddModelError(string.Empty, "Sua conta está bloqueada.");
                return View(model);
            }

            // Caso o login falhe sem estar bloqueado
            ModelState.AddModelError(string.Empty, "Usuário ou senha inválidos.");
            ViewData["ErrorMessage"] = "Usuário ou senha inválidos.";  // Passando a mensagem de erro para a View
        }

        // Se o modelo não for válido, retorna a view com os erros
        return View(model);
    }

    // POST: /Account/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("Usuário deslogado.");
        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    // Redireciona para a URL local ou para a Home
    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);  // Redireciona para a URL local fornecida
        }
        return RedirectToAction(nameof(HomeController.Index), "Home");  // Caso contrário, vai para a Home
    }
}
