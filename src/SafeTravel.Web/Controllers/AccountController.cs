using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SafeTravel.Core.Data;
using SafeTravel.Core.Entities;
using SafeTravel.Core.Services;
using SafeTravel.Web.Api;

namespace SafeTravel.Web.Controllers;

public class LoginForm
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
    public string? Code { get; set; }
    public string? ReturnUrl { get; set; }
    public bool NeedsCode { get; set; }
    public string? Error { get; set; }
}

/// <summary>Sign-in for the browser portals. One account works on the web and in the apps.</summary>
[AllowAnonymous, Route("account")]
public class AccountController(SignInChecker checker, SignInManager<AppUser> signIn, UserManager<AppUser> users, AuditService audit, AppDbContext db) : Controller
{
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null) => View(new LoginForm { ReturnUrl = returnUrl });

    [HttpPost("login"), ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginForm form, CancellationToken ct)
    {
        form.Error = null;
        if (!ModelState.IsValid) { form.Error = "Enter your email and password."; return View(form); }

        var (outcome, user) = await checker.CheckAsync(form.Email, form.Password, form.Code);
        switch (outcome)
        {
            case SignInChecker.Outcome.Locked: form.Error = "Too many wrong attempts. Try again in 15 minutes."; return View(form);
            case SignInChecker.Outcome.NeedsCode: form.NeedsCode = true; return View(form);
            case SignInChecker.Outcome.Wrong: form.NeedsCode = !string.IsNullOrEmpty(form.Code); form.Error = form.NeedsCode ? "That code is not right." : "The email or password is not right."; return View(form);
        }

        await signIn.SignInAsync(user!, isPersistent: false);
        var roles = await users.GetRolesAsync(user!);
        if (roles.Any(Roles.Staff.Contains)) { audit.Add("Signed in to the Admin Portal", nameof(AppUser), user!.Id, user.FullName); await db.SaveChangesAsync(ct); }

        if (!string.IsNullOrEmpty(form.ReturnUrl) && Url.IsLocalUrl(form.ReturnUrl)) return LocalRedirect(form.ReturnUrl);
        return LocalRedirect(roles.Any(Roles.Staff.Contains) ? "/admin" : roles.Contains(Roles.Supplier) ? "/supplier" : "/");
    }

    [HttpPost("logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout() { await signIn.SignOutAsync(); return LocalRedirect("/"); }

    [HttpGet("denied")]
    public IActionResult Denied() => View();
}
