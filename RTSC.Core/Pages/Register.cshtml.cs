using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RTSC.Core.Data;
using RTSC.Core.Domain;

namespace RTSC.Core.Pages;

public sealed class RegisterModel(AppDbContext db, IPasswordHasher<User> hasher) : PageModel
{
    [BindProperty] public string DisplayName { get; set; } = string.Empty;
    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    public string? Error { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        var email = Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(email) || Password.Length < 8)
        {
            Error = "Заполните все поля. Пароль должен содержать минимум 8 символов.";
            return Page();
        }

        if (await db.Users.AnyAsync(x => x.Email == email))
        {
            Error = "Аккаунт с таким email уже существует.";
            return Page();
        }

        var user = new User { DisplayName = DisplayName.Trim(), Email = email };
        user.PasswordHash = hasher.HashPassword(user, Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return RedirectToPage("/Login");
    }
}
