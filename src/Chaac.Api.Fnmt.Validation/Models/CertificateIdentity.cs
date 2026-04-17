namespace Chaac.Api.Fnmt.Validation.Models;

public class CertificateIdentity
{
    public string Dni { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string RawDn { get; set; } = string.Empty;
}