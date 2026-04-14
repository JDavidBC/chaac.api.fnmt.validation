namespace Chaac.Api.Fnmt.Validation.Models;

/// <summary>
/// Holds the identity information extracted from an FNMT client certificate.
/// </summary>
public sealed class CertificateIdentity
{
    /// <summary>DNI or NIE extracted from the certificate subject or extensions.</summary>
    public string DniNie { get; init; } = string.Empty;

    /// <summary>Common Name (CN) from the certificate subject.</summary>
    public string CommonName { get; init; } = string.Empty;

    /// <summary>Serial number of the certificate (hex string).</summary>
    public string SerialNumber { get; init; } = string.Empty;

    /// <summary>Distinguished name of the certificate issuer.</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Certificate validity start date (UTC).</summary>
    public DateTimeOffset NotBefore { get; init; }

    /// <summary>Certificate validity end date (UTC).</summary>
    public DateTimeOffset NotAfter { get; init; }
}
