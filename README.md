# chaac.api.fnmt.validation

ASP.NET Core Web API (.NET 10) for authenticating Spanish citizens using FNMT (Fábrica Nacional de Moneda y Timbre) client certificates and issuing JWT tokens. Designed for IIS deployment on Windows Server 2019.

## Project Structure

```
├── Controllers/
│   └── LoginController.cs          # POST /login – certificate auth & JWT issuance
├── Models/
│   ├── CertificateIdentity.cs      # Identity fields extracted from the certificate
│   ├── JwtSettings.cs              # JWT configuration bound from appsettings.json
│   └── LoginResponse.cs            # API response with JWT token and user info
├── Services/
│   ├── IFnmtCertificateValidator.cs  # Validator interface
│   ├── FnmtCertificateValidator.cs   # FNMT chain validation + DNI/NIE extraction
│   ├── IJwtTokenService.cs           # Token service interface
│   └── JwtTokenService.cs            # HS256 JWT generation
├── docs/
│   └── IIS-Deployment.md           # Step-by-step IIS deployment guide
├── Program.cs                      # Clean startup (no HTTPS redirect, IIS-ready)
├── appsettings.json                # JWT config placeholder
├── appsettings.Development.json    # Dev overrides
└── web.config                      # IIS / ASP.NET Core Module configuration
```

## Authentication Flow

1. User navigates to `https://my-domain.com/login`
2. IIS requests a client certificate (TLS handshake)
3. Browser presents the FNMT certificate
4. IIS validates the certificate chain and forwards it to ASP.NET Core
5. `FnmtCertificateValidator` checks expiry, FNMT issuer, and X.509 chain
6. DNI/NIE and other identity fields are extracted from the certificate subject
7. `JwtTokenService` generates a signed JWT containing `dni_nie`, `common_name`, etc.

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

## IIS Deployment

See **[docs/IIS-Deployment.md](docs/IIS-Deployment.md)** for full step-by-step instructions including:

- Installing the ASP.NET Core Hosting Bundle
- Publishing the application (`dotnet publish`)
- Installing FNMT root/intermediate CA certificates
- Configuring IIS SSL Settings and client certificate requirements
- Setting permissions and securing the JWT secret
