using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Chaac.Api.Fnmt.Validation.Models;

namespace Chaac.Api.Fnmt.Validation.Services;

/// <summary>
/// Production implementation of <see cref="IFnmtCertificateValidator"/>.
/// Validates client certificates against the FNMT chain of trust installed on the
/// Windows Server certificate store and extracts Spanish identity fields.
/// </summary>
public sealed class FnmtCertificateValidator : IFnmtCertificateValidator
{
    // Known fragments present in FNMT issuer Distinguished Names.
    // These are checked case-insensitively against the full issuer DN string.
    private static readonly string[] FnmtIssuerFragments =
    [
        "FNMT",
        "Fabrica Nacional de Moneda",
        "Fábrica Nacional de Moneda",
        "AC FNMT",
        "FNMT-RCM",
    ];

    // OID 2.5.4.5 is the X.500 serialNumber attribute, used by FNMT to store
    // the DNI/NIE in citizen certificates (e.g. SERIALNUMBER=12345678A).
    private const string SerialNumberOid = "2.5.4.5";

    // Regex patterns for DNI (8 digits + letter) and NIE (X/Y/Z + 7 digits + letter).
    private static readonly Regex DniPattern = new(
        @"\b\d{8}[A-Z]\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NiePattern = new(
        @"\b[XYZ]\d{7}[A-Z]\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ILogger<FnmtCertificateValidator> _logger;

    public FnmtCertificateValidator(ILogger<FnmtCertificateValidator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool Validate(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        // 1. Temporal validity
        var now = DateTimeOffset.UtcNow;
        if (now < certificate.NotBefore || now > certificate.NotAfter)
        {
            _logger.LogWarning(
                "Certificate {Serial} is outside its validity period ({NotBefore} – {NotAfter}).",
                certificate.SerialNumber, certificate.NotBefore, certificate.NotAfter);
            return false;
        }

        // 2. FNMT issuer check
        if (!IsIssuedByFnmt(certificate))
        {
            _logger.LogWarning(
                "Certificate {Serial} was not issued by a recognised FNMT CA. Issuer: {Issuer}.",
                certificate.SerialNumber, certificate.IssuerName.Name);
            return false;
        }

        // 3. Chain validation using the Windows/OS certificate store (trusts CAs
        //    installed by the administrator – FNMT root + intermediate must be present).
        if (!ValidateChain(certificate))
        {
            _logger.LogWarning(
                "Certificate {Serial} failed chain validation.",
                certificate.SerialNumber);
            return false;
        }

        return true;
    }

    /// <inheritdoc/>
    public CertificateIdentity ExtractIdentity(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        var dniNie = ExtractDniNie(certificate);
        var commonName = GetRdnValue(certificate.SubjectName, "2.5.4.3"); // OID for CN

        return new CertificateIdentity
        {
            DniNie = dniNie,
            CommonName = commonName,
            SerialNumber = certificate.SerialNumber ?? string.Empty,
            Issuer = certificate.IssuerName.Name ?? string.Empty,
            NotBefore = certificate.NotBefore,
            NotAfter = certificate.NotAfter,
        };
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static bool IsIssuedByFnmt(X509Certificate2 cert)
    {
        var issuer = cert.IssuerName.Name ?? string.Empty;
        foreach (var fragment in FnmtIssuerFragments)
        {
            if (issuer.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private bool ValidateChain(X509Certificate2 certificate)
    {
        using var chain = new X509Chain();

        // Allow the OS to use the machine store for chain building.
        // RevocationMode can be adjusted to Online/Offline depending on network access.
        chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
        chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
        chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
        chain.ChainPolicy.VerificationTime = DateTime.UtcNow;

        bool valid = chain.Build(certificate);

        if (!valid)
        {
            foreach (var element in chain.ChainElements)
            {
                foreach (var status in element.ChainElementStatus)
                {
                    _logger.LogWarning(
                        "Chain element {Subject}: {Status} – {Information}",
                        element.Certificate.Subject, status.Status, status.StatusInformation);
                }
            }
        }

        return valid;
    }

    /// <summary>
    /// Tries to extract a DNI or NIE from the certificate.
    /// Priority order:
    ///   1. X.500 serialNumber attribute (OID 2.5.4.5) in the Subject DN.
    ///   2. Regex search in the full Subject DN string.
    ///   3. Regex search in the Subject Alternative Name extension value.
    /// </summary>
    private static string ExtractDniNie(X509Certificate2 cert)
    {
        // 1. Dedicated serialNumber RDN (most FNMT certs)
        var serialRdn = GetRdnValue(cert.SubjectName, SerialNumberOid);
        if (!string.IsNullOrWhiteSpace(serialRdn))
        {
            var match = TryMatchDniOrNie(serialRdn);
            if (match is not null)
                return match.ToUpperInvariant();

            // FNMT sometimes stores the value as-is without extra text
            if (IsDniOrNie(serialRdn.Trim()))
                return serialRdn.Trim().ToUpperInvariant();
        }

        // 2. Full subject string scan
        var subjectMatch = TryMatchDniOrNie(cert.SubjectName.Name ?? string.Empty);
        if (subjectMatch is not null)
            return subjectMatch.ToUpperInvariant();

        // 3. Subject Alternative Name extension (OID 2.5.29.17)
        var san = cert.Extensions["2.5.29.17"];
        if (san is not null)
        {
            var sanMatch = TryMatchDniOrNie(san.Format(false));
            if (sanMatch is not null)
                return sanMatch.ToUpperInvariant();
        }

        return string.Empty;
    }

    private static string? TryMatchDniOrNie(string input)
    {
        var nie = NiePattern.Match(input);
        if (nie.Success) return nie.Value;

        var dni = DniPattern.Match(input);
        if (dni.Success) return dni.Value;

        return null;
    }

    private static bool IsDniOrNie(string value) =>
        DniPattern.IsMatch(value) || NiePattern.IsMatch(value);

    /// <summary>
    /// Returns the first RDN value for the given OID from an X500DistinguishedName.
    /// </summary>
    private static string GetRdnValue(X500DistinguishedName dn, string oid)
    {
        foreach (var rdn in dn.EnumerateRelativeDistinguishedNames())
        {
            if (rdn.GetSingleElementType().Value == oid)
                return rdn.GetSingleElementValue() ?? string.Empty;
        }
        return string.Empty;
    }
}
