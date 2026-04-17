using System.Security.Cryptography.X509Certificates;
using Chaac.Api.Fnmt.Validation.Models;

namespace Chaac.Api.Fnmt.Validation.Services;

public interface IFnmtCertificateValidator
{
    /// <summary>
    /// Procesa el DN crudo de Nginx y devuelve el objeto identidad con Nombre, Apellidos y DNI desglosados.
    /// </summary>
    CertificateIdentity ExtractIdentityFromDn(string dn);
}