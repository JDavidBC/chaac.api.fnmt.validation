namespace Chaac.Api.Fnmt.Validation.Models;

/// <summary>
/// Configuration values for JWT generation, bound from appsettings.json section "Jwt".
/// </summary>
public sealed class JwtSettings
{
    /// <summary>Symmetric signing secret (minimum 32 characters).</summary>
    public string Secret { get; init; } = string.Empty;

    /// <summary>Token issuer claim value.</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Token audience claim value.</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>Token lifetime in minutes.</summary>
    public int ExpirationMinutes { get; init; } = 60;
}
