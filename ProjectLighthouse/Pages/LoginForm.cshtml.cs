#nullable enable
using System.Threading.Tasks;
using JetBrains.Annotations;
using Kettu;
using LBPUnion.ProjectLighthouse.Helpers;
using LBPUnion.ProjectLighthouse.Logging;
using LBPUnion.ProjectLighthouse.Pages.Layouts;
using LBPUnion.ProjectLighthouse.Types;
using LBPUnion.ProjectLighthouse.Types.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace LBPUnion.ProjectLighthouse.Pages;

public class LoginForm : BaseLayout
{
    public LoginForm(Database database) : base(database)
    {}

    public string Error { get; private set; }

    [UsedImplicitly]
    public async Task<IActionResult> OnPost(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            this.Error = "The username field is required.";
            return this.Page();
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            this.Error = "The password field is required.";
            return this.Page();
        }

        if (ServerSettings.Instance.HCaptchaEnabled)
        {
            // && (!this.Request.Form.TryGetValue("h-captcha-response", out StringValues values) || !await CaptchaHelper.Verify(values[0])))
            bool gotCaptcha = this.Request.Form.TryGetValue("h-captcha-response", out StringValues values);
            string? token = gotCaptcha ? values[0] : null;

            if (token == null || !await CaptchaHelper.Verify(token))
            {
                this.Error = "You must solve the captcha correctly.";
                return this.Page();
            }
        }

        User? user = await this.Database.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            Logger.Log($"User {username} failed to login on web due to invalid username", LoggerLevelLogin.Instance);
            this.Error = "The username or password you entered is invalid.";
            return this.Page();
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.Password))
        {
            Logger.Log($"User {user.Username} (id: {user.UserId}) failed to login on web due to invalid password", LoggerLevelLogin.Instance);
            this.Error = "The username or password you entered is invalid.";
            return this.Page();
        }

        if (user.Banned)
        {
            Logger.Log($"User {user.Username} (id: {user.UserId}) failed to login on web due to being banned", LoggerLevelLogin.Instance);
            this.Error = "You have been banned. Please contact an administrator for more information.\nReason: " + user.BannedReason;
            return this.Page();
        }

        WebToken webToken = new()
        {
            UserId = user.UserId,
            UserToken = HashHelper.GenerateAuthToken(),
        };

        this.Database.WebTokens.Add(webToken);
        await this.Database.SaveChangesAsync();

        this.Response.Cookies.Append("LighthouseToken", webToken.UserToken);

        Logger.Log($"User {user.Username} (id: {user.UserId}) successfully logged in on web", LoggerLevelLogin.Instance);

        if (user.PasswordResetRequired) return this.Redirect("~/passwordResetRequired");

        return this.RedirectToPage(nameof(LandingPage));
    }

    [UsedImplicitly]
    public async Task<IActionResult> OnGet()
    {
        this.Error = string.Empty;
        return this.Page();
    }
}