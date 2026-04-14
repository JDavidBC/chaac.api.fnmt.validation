using System.Security.Cryptography.X509Certificates;
using Chaac.Api.Fnmt.Validation.Models;

namespace Chaac.Api.Fnmt.Validation.Services;

/// <summary>
/// Validates FNMT (Fábrica Nacional de Moneda y Timbre) client certificates and
/// extracts the user identity information from them.
/// </summary>
public interface IFnmtCertificateValidator
{
    /// <summary>
    /// Validates an X.509 certificate against FNMT trust rules.
    /// </summary>
    /// <param name="certificate">The client certificate to validate.</param>
    /// <returns>
    /// <c>true</c> if the certificate is valid, not expired, issued by a trusted FNMT CA,
    /// and passes chain validation; otherwise <c>false</c>.
    /// </returns>
    bool Validate(X509Certificate2 certificate);

    /// <summary>
    /// Extracts identity fields (DNI/NIE, CN, serial number, issuer) from a valid certificate.
    /// </summary>
    /// <param name="certificate">A certificate that has already passed <see cref="Validate"/>.</param>
    /// <returns>A populated <see cref="CertificateIdentity"/> instance.</returns>
    CertificateIdentity ExtractIdentity(X509Certificate2 certificate);
}
