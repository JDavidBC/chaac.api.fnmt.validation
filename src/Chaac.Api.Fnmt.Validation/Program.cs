using System.Text.RegularExpressions;
using Chaac.Api.Fnmt.Validation.Models;
using Chaac.Api.Fnmt.Validation.Services;
using Microsoft.Extensions.Hosting.WindowsServices;


// 1. FORZAR RUTA (Ya lo tienes bien, pero aseguremos el orden)
var baseDir = AppContext.BaseDirectory;
Directory.SetCurrentDirectory(baseDir);

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = baseDir
});

// 2. FORZAR LA CARGA DEL JSON (Esto es lo que te falta para el servicio)
builder.Configuration.SetBasePath(baseDir);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// 1. CONFIGURACIÓN DEL SERVICIO
builder.Host.UseWindowsService(); // Permite correr como servicio de Windows

// 2. KESTREL (Escucha interna para Nginx)
builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(5000));

// 3. CARGA DE VARIABLES DE ENTORNO CON PREFIJO SEGURO
// Esto mapeará CHAAC_API_FNMT__JWT_SECRET a la propiedad Jwt:Secret
// builder.Configuration.AddEnvironmentVariables();

// 4. VALIDACIÓN DE SEGURIDAD (Falla rápido si no hay secreto)
var jwtSettingsSection = builder.Configuration.GetSection("Jwt");
var secret = jwtSettingsSection["Secret"];

if (string.IsNullOrEmpty(secret)) 
{
    throw new Exception("CRITICAL: No se encuentra la variable de entorno 'Jwt__Secret'");
}

// 3. REGISTRO DE CONFIGURACIÓN Y SERVICIOS
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddAuthorization();

var app = builder.Build();

// 4. EL ENDPOINT DE AUTENTICACIÓN (Reemplaza al Controller)
app.MapGet("/auth-fnmt", async (HttpContext context, IJwtTokenService jwtService) =>
{
    try 
    {
        // 1. Cabeceras
        string certDn = context.Request.Headers["X-Client-Cert-DN"].ToString();
        string certVerify = context.Request.Headers["X-Client-Verify"].ToString();
        
        var xForwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        string userIp = !string.IsNullOrWhiteSpace(xForwardedFor) 
                        ? xForwardedFor.Split(',')[0].Trim() 
                        : context.Connection.RemoteIpAddress?.ToString() ?? "8.8.8.8";

        // 2. Validación
        if (!certVerify.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
            return Results.Json(new { message = "Error cert" }, statusCode: 403);

        var identity = ParseFnmtDn(certDn);

        // 3. GEO con PROTECCIÓN TOTAL (Para evitar el 500)
        string ciudad = "Desconocida";
        string pais = "Desconocido";
        
        try {
            using var client = new HttpClient();
            // Ponemos un timeout corto para que no se quede colgado el servicio
            client.Timeout = TimeSpan.FromSeconds(2); 
            var geo = await client.GetFromJsonAsync<dynamic>($"http://ip-api.com/json/{userIp}?fields=status,country,city");
            if (geo?.GetProperty("status").GetString() == "success") {
                ciudad = geo.GetProperty("city").GetString();
                pais = geo.GetProperty("country").GetString();
            }
        } catch { 
            // Si esto falla, NO lanzamos error 500, seguimos adelante
        }

        // 4. Token
        var authResponse = jwtService.GenerateToken(identity, userIp, ciudad, pais);
        return Results.Ok(authResponse);
    }
    catch (Exception ex)
    {
        // Esto te dirá en el log qué está pasando realmente
        return Results.Json(new { error = ex.Message, stack = ex.StackTrace }, statusCode: 500);
    }
});

app.Run();

// --- LÓGICA DE EXTRACCIÓN (Mantenemos coherencia con el modelo) ---
static CertificateIdentity ParseFnmtDn(string dn)
{
    var identity = new CertificateIdentity { RawDn = dn ?? "" };
    if (string.IsNullOrEmpty(dn)) return identity;

    // 1. APELLIDOS (SN): Captura todo hasta la siguiente coma o final de cadena
    var snMatch = Regex.Match(dn, @"SN=([^,]+)", RegexOptions.IgnoreCase);
    if (snMatch.Success) identity.Apellidos = snMatch.Groups[1].Value.Trim();

    // 2. NOMBRE (GN): Captura nombres compuestos sin importar cuántos sean
    var gnMatch = Regex.Match(dn, @"GN=([^,]+)", RegexOptions.IgnoreCase);
    if (gnMatch.Success) identity.Nombre = gnMatch.Groups[1].Value.Trim();

    // 3. IDENTIFICADOR (NIE/DNI): 
    // Buscamos el serialNumber que es donde viene el ID oficial "limpio"
    // Patrón: Una o más letras/números (ej: 12345678Z, X1234567L, etc.)
    var idMatch = Regex.Match(dn, @"serialNumber=IDCES-([^,]+)", RegexOptions.IgnoreCase);
    if (idMatch.Success) 
    {
        identity.Dni = idMatch.Groups[1].Value.Trim().ToUpper();
    }
    else 
    {
        // Fallback: si no viene en serialNumber, buscamos el patrón de NIE/DNI en todo el texto
        var fallbackMatch = Regex.Match(dn, @"([A-Z]?\d+[A-Z]?)", RegexOptions.IgnoreCase);
        if (fallbackMatch.Success) identity.Dni = fallbackMatch.Groups[1].Value.ToUpper();
    }

    identity.NombreCompleto = $"{identity.Nombre} {identity.Apellidos}".Trim();

    return identity;
}