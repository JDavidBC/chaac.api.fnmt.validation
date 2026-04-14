using Chaac.Api.Fnmt.Validation.Models;
using Chaac.Api.Fnmt.Validation.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── IIS integration ───────────────────────────────────────────────────────────
// When hosted behind IIS (in-process or out-of-process), use the IIS integration
// layer so that client certificates forwarded by the ASP.NET Core Module are
// available on HttpContext.Connection.ClientCertificate.
builder.Services.Configure<IISServerOptions>(options =>
{
    options.AutomaticAuthentication = false;
});

// ── Configuration ─────────────────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtSettings>(jwtSection);

var jwtSettings = jwtSection.Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT configuration is missing from appsettings.json.");

if (string.IsNullOrWhiteSpace(jwtSettings.Secret) || jwtSettings.Secret.Length < 32)
    throw new InvalidOperationException("Jwt:Secret must be at least 32 characters.");

// ── Authentication / Authorization ────────────────────────────────────────────
var keyBytes = Encoding.UTF8.GetBytes(jwtSettings.Secret);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IFnmtCertificateValidator, FnmtCertificateValidator>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

// ── MVC / OpenAPI ─────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ── Build pipeline ────────────────────────────────────────────────────────────
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// NOTE: UseHttpsRedirection() is intentionally omitted.
//       TLS termination and client certificate negotiation are handled by IIS.

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

