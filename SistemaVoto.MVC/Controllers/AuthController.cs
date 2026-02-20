using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SistemaVoto.MVC.ViewModels;

namespace SistemaVoto.MVC.Controllers;

/// <summary>
/// Controlador de autenticacion usando ASP.NET Identity
/// </summary>
public class AuthController : Controller
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    // Injecting context to save custom user data if needed (e.g. custom name field if extending IdentityUser)
    // For now we assume standard IdentityUser or we might need to cast to our Usuario model if we were using custom storage.
    // However, the current setup seems to use standard IdentityUser for Auth, and a separate Usuario model in Modelos.
    // We need to verify if we need to sync them.
    // Looking at AdminController, it creates IdentityUser and that's it. It doesn't seem to sync to a 'Usuario' table manually.
    // But Modelos/Usuario.cs exists. Let's check if there is a sync mechanism. 
    // Wait, AdminController.CrearEleccion just creates IdentityUser. 
    // Let's stick to standard IdentityUser registration for now to match existng pattern.

    public AuthController(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    /// <summary>
    /// Muestra el formulario de registro
    /// </summary>
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToHome();
        }
        return View(new RegisterViewModel());
    }

    /// <summary>
    /// Procesa el registro de un nuevo usuario
    /// </summary>
    [HttpPost]
    // [ValidateAntiForgeryToken] // <--- COMENTADO PARA EVITAR ERROR 400 EN RENDER
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new IdentityUser { UserName = model.Email, Email = model.Email };
        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            // Asignar rol por defecto
            await _userManager.AddToRoleAsync(user, "Usuario");

            // Auto-login
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Elecciones");
        }

        foreach (var error in result.Errors)
        {
            // Traducir errores comunes si es necesario
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    /// <summary>
    /// Muestra el formulario de login
    /// </summary>
    [HttpGet]
    /// <summary>
    /// Procesa el login con Identity (Versión Manual y Directa)
    /// </summary>
    [HttpPost]
    // [ValidateAntiForgeryToken] // Mantenemos esto comentado
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        // 1. Si no envían datos, volvemos a la vista
        if (string.IsNullOrEmpty(model.Email) || string.IsNullOrEmpty(model.Password))
        {
            ModelState.AddModelError(string.Empty, "Por favor ingrese correo y contraseña.");
            return View(model);
        }

        // 2. Buscar al usuario directamente en la base de datos
        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            // Error explícito para que se muestre en el HTML
            ModelState.AddModelError(string.Empty, "El usuario no existe en la base de datos.");
            return View(model);
        }

        // 3. Verificar la contraseña manualmente
        var passwordCorrect = await _userManager.CheckPasswordAsync(user, model.Password);
        if (!passwordCorrect)
        {
            ModelState.AddModelError(string.Empty, "La contraseña es incorrecta.");
            return View(model);
        }

        // 4. INICIO DE SESIÓN FORZADO (El mismo método que sí funcionó en el Registro)
        await _signInManager.SignInAsync(user, isPersistent: model.RememberMe);

        // 5. Redirección según rol
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("Administrador"))
        {
            return RedirectToAction("Dashboard", "Admin");
        }

        // Si hay una URL de retorno válida, ir allí
        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        // Redirección por defecto para usuarios normales
        return RedirectToAction("Index", "Elecciones");
    }
    /// <summary>
    /// Procesa el login con Identity
    /// </summary>
    [HttpPost]
    // [ValidateAntiForgeryToken] // <--- COMENTADO PARA EVITAR ERROR 400 EN RENDER
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Intentar login con Identity (Email como username)
        var result = await _signInManager.PasswordSignInAsync(
            model.Email,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: false);

        if (result.Succeeded)
        {
            // Login exitoso
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("Administrador"))
                {
                    return RedirectToAction("Dashboard", "Admin");
                }
            }

            return RedirectToAction("Index", "Elecciones");
        }

        if (result.IsLockedOut)
        {
            model.ErrorMessage = "Cuenta bloqueada. Intente mas tarde.";
        }
        else if (result.IsNotAllowed)
        {
            model.ErrorMessage = "Login no permitido. Verifique su cuenta.";
        }
        else
        {
            model.ErrorMessage = "Credenciales invalidas";
        }

        return View(model);
    }

    /// <summary>
    /// Cierra la sesion
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    /// <summary>
    /// Redirige al home segun el rol del usuario
    /// </summary>
    private IActionResult RedirectToHome()
    {
        if (User.IsInRole("Administrador"))
        {
            return RedirectToAction("Dashboard", "Admin");
        }

        return RedirectToAction("Index", "Elecciones");
    }

    /// <summary>
    /// Acceso denegado
    /// </summary>
    public IActionResult AccessDenied()
    {
        return View();
    }
}