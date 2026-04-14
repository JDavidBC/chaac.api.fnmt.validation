using Chaac.Api.Fnmt.Validation.Services;
using Microsoft.AspNetCore.Mvc;

namespace Chaac.Api.Fnmt.Validation.Controllers;

/// <summary>
/// Handles certificate-based authentication and JWT token issuance.
/// </summary>
[ApiController]
[Route("[controller]")]
public sealed class LoginController : ControllerBase
{
    private readonly IFnmtCertificateValidator _certificateValidator;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<LoginController> _logger;

    public LoginController(
        IFnmtCertificateValidator certificateValidator,
        IJwtTokenService jwtTokenService,
        ILogger<LoginController> logger)
    {
        _certificateValidator = certificateValidator;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user using the FNMT client certificate provided by IIS.
    /// </summary>
    /// <remarks>
    /// IIS must be configured to require or accept a client certificate on this endpoint.
    /// The browser presents the certificate during the TLS handshake; IIS forwards it to
    /// ASP.NET Core via the server variable <c>CERT_CLIENTCERT</c> (or the connection
    /// property when using the ASP.NET Core Hosting Module with client certificate
    /// forwarding enabled).
    /// </remarks>
    /// <returns>
    /// 200 OK with a <c>LoginResponse</c> (JWT token + user info) on success.
    /// 401 Unauthorized if no certificate is present or it fails validation.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Post()
    {
        var certificate = HttpContext.Connection.ClientCertificate;

        if (certificate is null)
        {
            _logger.LogWarning(
                "Login attempt from {RemoteIp} with no client certificate.",
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "Client certificate is required." });
        }

        _logger.LogInformation(
            "Received client certificate: Subject={Subject}, Issuer={Issuer}, Serial={Serial}.",
            certificate.Subject, certificate.Issuer, certificate.SerialNumber);

        if (!_certificateValidator.Validate(certificate))
        {
            _logger.LogWarning(
                "Certificate validation failed for serial {Serial}.",
                certificate.SerialNumber);
            return Unauthorized(new { message = "Certificate validation failed." });
        }

        var identity = _certificateValidator.ExtractIdentity(certificate);

        if (string.IsNullOrWhiteSpace(identity.DniNie))
        {
            _logger.LogWarning(
                "Could not extract DNI/NIE from certificate serial {Serial}.",
                certificate.SerialNumber);
            return Unauthorized(new { message = "Could not extract identity from certificate." });
        }

        _logger.LogInformation(
            "Certificate authentication successful for DNI/NIE {DniNie}.",
            identity.DniNie);

        var response = _jwtTokenService.GenerateToken(identity);
        return Ok(response);
    }
}
