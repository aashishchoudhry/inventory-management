using InventoryErp.Infrastructure.Identity;
using InventoryErp.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InventoryErp.Web.Controllers;

/// <summary>
/// Login and logout only. Registration, password reset and user management are deliberately
/// out of scope — the only account is the seeded administrator.
/// </summary>
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_signInManager.IsSignedIn(User))
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);

        // Every failure below returns the same message. Distinguishing "no such account" from
        // "wrong password" would let an anonymous caller enumerate valid email addresses.
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Failed login for {Email}: unknown or inactive account.", model.Email);
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            model.Password,
            isPersistent: model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} signed in.", model.Email);
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Login blocked for {Email}: account locked out.", model.Email);
            ModelState.AddModelError(string.Empty, "This account is locked. Try again later.");
            return View(model);
        }

        _logger.LogWarning("Failed login for {Email}: incorrect password.", model.Email);
        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    /// <summary>POST only — a GET logout can be triggered by any link or image tag.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    /// <summary>
    /// Guards against open redirects: an attacker-supplied absolute URL in <c>returnUrl</c> would
    /// otherwise bounce the user off-site after a genuine sign-in.
    /// </summary>
    private IActionResult RedirectToLocal(string? returnUrl)
        => Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl!)
            : RedirectToAction("Index", "Home");
}
