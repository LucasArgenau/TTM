using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using TorneioTenisMesa.Models;
using TorneioTenisMesa.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
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
            var result = await _signInManager.PasswordSignInAsync(
                model.UserName!, model.Password!, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                _logger.LogInformation("Usuário logado com sucesso.");
                var user = await _userManager.FindByNameAsync(model.UserName!);

                if (user != null)
                {
                    if (await _userManager.IsInRoleAsync(user, "Admin"))
                        return RedirectToAction("Index", "Admin");
                    else if (await _userManager.IsInRoleAsync(user, "Player"))
                        return RedirectToAction("Index", "Player");
                }

                return RedirectToLocal(returnUrl);
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Conta bloqueada.");
                ModelState.AddModelError(string.Empty, "Sua conta está bloqueada.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Usuário ou senha inválidos.");
            ViewData["ErrorMessage"] = "Usuário ou senha inválidos.";
        }

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

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(HomeController.Index), "Home");
    }

    // GET: /Account/RegisterAdmin
    [AllowAnonymous]
    [HttpGet]
    public IActionResult RegisterAdmin()
    {
        return View(new CreateAdminViewModel());
    }

    // POST: /Account/RegisterAdmin
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterAdmin(CreateAdminViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var existingUser = await _userManager.FindByNameAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("", "Usuário já existe.");
            return View(model);
        }

        var newAdmin = new User
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(newAdmin, model.Password);

        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(newAdmin, "Admin");
            _logger.LogInformation("Admin registrado com sucesso.");
            return RedirectToAction("Login", "Account");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);

        return View(model);
    }
}
