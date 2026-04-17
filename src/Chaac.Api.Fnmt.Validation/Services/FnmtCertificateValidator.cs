using System.Text.RegularExpressions;
using Chaac.Api.Fnmt.Validation.Models;

namespace Chaac.Api.Fnmt.Validation.Services;

public sealed class FnmtCertificateValidator : IFnmtCertificateValidator
{
    private static readonly Regex CommonNameRegex = new(@"CN=([^,]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DniNiePattern = new(@"([XYZ]?\d{7,8}[A-Z])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public CertificateIdentity ExtractIdentityFromDn(string dn)
    {
        var identity = new CertificateIdentity { RawDn = dn ?? string.Empty };
        
        if (string.IsNullOrWhiteSpace(dn)) return identity;

        // 1. Extraer Common Name (CN)
        var cnMatch = CommonNameRegex.Match(dn);
        if (!cnMatch.Success) return identity;

        var fullCn = cnMatch.Groups[1].Value.Trim(); 
        identity.NombreCompleto = fullCn;

        // 2. Extraer DNI/NIE
        var idMatch = DniNiePattern.Match(fullCn);
        if (idMatch.Success)
        {
            identity.Dni = idMatch.Value.ToUpperInvariant();
        }

        // 3. Desglosar Nombre y Apellidos (Formato FNMT: APELLIDOS NOMBRE - DNI)
        // Quitamos el DNI y el guion si existen
        var textPart = fullCn.Split(" - ")[0].Trim();
        var parts = textPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 2)
        {
            // El nombre en certificados de persona física suele ser la última palabra
            identity.Nombre = parts.Last();
            identity.Apellidos = string.Join(" ", parts.Take(parts.Length - 1));
        }
        else
        {
            identity.Nombre = textPart;
        }

        return identity;
    }
}