using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Taim.Data;

namespace Taim.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", Login)
           .AllowAnonymous()
           .WithName("Login")
           .WithTags("Auth");

        return app;
    }

    private static async Task<IResult> Login(
        [FromBody] LoginRequest req,
        TaimDbContext db,
        IConfiguration config)
    {
        // Look up user by email
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == req.Email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Results.Unauthorized();

        var secret = config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret required.");
        var issuer = config["Jwt:Issuer"] ?? "taim";
        var audience = config["Jwt:Audience"] ?? "taim";
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("tenantId", user.TenantId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(issuer, audience, claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds);

        return Results.Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt
        });
    }

    private sealed record LoginRequest(string Email, string Password);
}
