# chaac.api.fnmt.validation

ASP.NET Core Web API (.NET 10) for authenticating Spanish citizens using FNMT (Fábrica Nacional de Moneda y Timbre) client certificates and issuing JWT tokens. Designed for IIS deployment on Windows Server 2019.

## Project Structure

```
├── Models/
│   ├── CertificateIdentity.cs      # Identity fields extracted from the certificate
│   ├── JwtSettings.cs              # JWT configuration bound from appsettings.json
│   └── LoginResponse.cs            # API response with JWT token and user info
├── Services/
│   ├── IFnmtCertificateValidator.cs  # Validator interface
│   ├── FnmtCertificateValidator.cs   # FNMT chain validation + DNI/NIE extraction
│   ├── IJwtTokenService.cs           # Token service interface
│   └── JwtTokenService.cs            # HS256 JWT generation
├── Program.cs                      # Clean startup (no HTTPS redirect, IIS-ready)
├── appsettings.json                # JWT config placeholder
```

## Authentication Flow

.

## Quick Start (Development)

```powershell
dotnet restore
dotnet run
```

## Configuration (`appsettings.json`)

```json
{
  "Jwt": {
    "Secret": "<at-least-32-char-random-secret>",
    "Issuer": "https://my-domain.com",
    "Audience": "https://my-domain.com",
    "ExpirationMinutes": 60
  }
}
```

> ⚠️ **Never** commit a real JWT secret. Use environment variables in production:  
> `Jwt__Secret=<secret>` (double underscore for nested keys).

## NGINX Deployment

Need to configure conf file in nginx for certificate authentication


