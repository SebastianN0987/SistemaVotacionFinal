using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SistemaVoto.MVC.ViewModels;

namespace SistemaVoto.MVC.Controllers;

public class AuthController : Controller
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;

    public AuthController(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToHome();
        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new IdentityUser { UserName = model.Email, Email = model.Email };
        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "Usuario");
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Elecciones");
        }

        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToHome();
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    // ============================================================
    // VERSIÓN DE LOGIN ÚNICA Y CORREGIDA (SIN DUPLICADOS)
    // ============================================================
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
        {
            ModelState.AddModelError(string.Empty, "Correo y contraseña requeridos.");
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "El usuario no existe.");
            return View(model);
        }

        var passwordCorrect = await _userManager.CheckPasswordAsync(user, model.Password);
        if (!passwordCorrect)
        {
            ModelState.AddModelError(string.Empty, "Contraseña incorrecta.");
            return View(model);
        }

        // Inicio de sesión forzado (Igual al de Register que sí funciona)
        await _signInManager.SignInAsync(user, isPersistent: model.RememberMe);

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("Administrador"))
        {
            return RedirectToAction("Dashboard", "Admin");
        }

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Elecciones");
    }

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    private IActionResult RedirectToHome()
    {
        if (User.IsInRole("Administrador")) return RedirectToAction("Dashboard", "Admin");
        return RedirectToAction("Index", "Elecciones");
    }

    public IActionResult AccessDenied() => View();
}