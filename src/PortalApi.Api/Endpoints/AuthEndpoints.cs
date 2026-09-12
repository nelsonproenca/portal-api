using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PortalApi.Api.Filters;
using PortalApi.Application.Auth;

namespace PortalApi.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/login", async (LoginRequest request, AdminAuthService authService, HttpContext http, CancellationToken ct) =>
        {
            var admin = await authService.VerifyLoginAsync(request.Password, ct);
            if (admin is null)
                return Results.Unauthorized();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new(ClaimTypes.Email, admin.Email),
                new(ClaimTypes.Role, "admin"),
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            });

            return Results.Ok(new { email = admin.Email });
        })
        .WithName("AdminLogin")
        .RequireRateLimiting("login")
        .AllowAnonymous();

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok();
        })
        .WithName("AdminLogout")
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization();

        group.MapGet("/me", (ClaimsPrincipal user) =>
            Results.Ok(new { email = user.FindFirstValue(ClaimTypes.Email) }))
        .WithName("AdminMe")
        .RequireAuthorization();

        group.MapPost("/change-password", async (ChangePasswordRequest request, ClaimsPrincipal user, AdminAuthService authService, CancellationToken ct) =>
        {
            var adminId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var ok = await authService.ChangePasswordAsync(adminId, request.CurrentPassword, request.NewPassword, ct);
            return ok ? Results.Ok() : Results.BadRequest(new { error = "Senha atual incorreta." });
        })
        .WithName("AdminChangePassword")
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization();
    }

    public record LoginRequest(string Password);
    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
}
