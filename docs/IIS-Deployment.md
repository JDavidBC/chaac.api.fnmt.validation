# IIS Deployment Guide – Chaac FNMT Certificate Authentication API

## Prerequisites

| Component | Version |
|-----------|---------|
| Windows Server | 2019 or later |
| IIS | 10.0 (included with Windows Server 2019) |
| ASP.NET Core Hosting Bundle | .NET 10.x |
| FNMT Root + Intermediate CAs | Installed in the **Local Machine** certificate store |

---

## 1. Install the ASP.NET Core Hosting Bundle

Download from <https://dotnet.microsoft.com/en-us/download/dotnet/10.0> and run the installer.  
This installs the **ASP.NET Core Module v2 (ANCM)** for IIS.

After installation, restart IIS:

```powershell
iisreset /restart
```

---

## 2. Publish the Application

Run the following command from the project root:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o C:\inetpub\wwwroot\chaac-fnmt
```

Verify that `Chaac.Api.Fnmt.Validation.dll` and `web.config` are present in the output folder.

---

## 3. Install FNMT Certificates on the Server

FNMT certificates must be trusted by the OS certificate chain validator.

1. Download the FNMT root and intermediate certificates from  
   <https://www.sede.fnmt.gob.es/descargas/certificados-raiz-de-la-fnmt>
2. Import them into the **Local Machine → Trusted Root Certification Authorities** store  
   (and **Intermediate Certification Authorities** for intermediate CAs):

```powershell
# Example – run as Administrator
Import-Certificate -FilePath "FNMT_RCM_SHA2.cer" `
    -CertStoreLocation Cert:\LocalMachine\Root

Import-Certificate -FilePath "FNMT_AC_Ciudadanía.cer" `
    -CertStoreLocation Cert:\LocalMachine\CA
```

---

## 4. Create an IIS Site

```powershell
# Create the application pool (no managed code – ASP.NET Core is self-hosted)
New-WebAppPool -Name "ChaacFnmtPool"
Set-ItemProperty IIS:\AppPools\ChaacFnmtPool managedRuntimeVersion ""

# Create the site
New-Website -Name "ChaacFnmt" `
            -PhysicalPath "C:\inetpub\wwwroot\chaac-fnmt" `
            -ApplicationPool "ChaacFnmtPool" `
            -Port 80 `
            -Force

# Add HTTPS binding (certificate must already be in the Personal store)
$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -like "*my-domain.com*" } | Select-Object -First 1
New-WebBinding -Name "ChaacFnmt" -Protocol https -Port 443 -IPAddress "*" -SslFlags 1
(Get-WebBinding -Name "ChaacFnmt" -Protocol https).AddSslCertificate($cert.Thumbprint, "My")
```

---

## 5. Enable SSL and Client Certificates

### Via IIS Manager (GUI)

1. Open **IIS Manager**.
2. Select the **ChaacFnmt** site.
3. Double-click **SSL Settings**.
4. Check **Require SSL**.
5. Under **Client certificates**, select **Require** (mTLS – certificate always requested)  
   or **Accept** (optional – certificate presented only if available).
6. Click **Apply**.

### Via `appcmd.exe` (automated)

```cmd
# Require SSL and require client certificate
%windir%\system32\inetsrv\appcmd.exe set config "ChaacFnmt" ^
    /section:access /sslFlags:"Ssl,SslNegotiateCert,SslRequireCert"
```

Use `SslNegotiateCert` alone if you want **Accept** (optional) instead of **Require**.

---

## 6. Configure the JWT Secret

Never store the JWT secret in `appsettings.json` in production.  
Use one of the following approaches:

### Option A – Environment variable (recommended)

In the IIS application pool advanced settings set:

```
Name:  Jwt__Secret
Value: <your-secure-random-32+-character-secret>
```

Or via PowerShell:

```powershell
[System.Environment]::SetEnvironmentVariable(
    "Jwt__Secret",
    "<your-secret>",
    [System.EnvironmentVariableTarget]::Machine)
```

### Option B – Windows DPAPI / Azure Key Vault

For high-security environments, load the secret from an external secret manager and
inject it during startup using a custom `IConfigurationProvider`.

---

## 7. Set Folder Permissions

Grant the application pool identity read/execute access to the publish folder:

```powershell
$acl = Get-Acl "C:\inetpub\wwwroot\chaac-fnmt"
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS AppPool\ChaacFnmtPool", "ReadAndExecute", "ContainerInherit,ObjectInherit",
    "None", "Allow")
$acl.SetAccessRule($rule)
Set-Acl "C:\inetpub\wwwroot\chaac-fnmt" $acl
```

If `stdoutLogEnabled` is set to `true` in `web.config`, also grant write access to the `logs` subfolder.

---

## 8. Verify the Deployment

```powershell
# Should return HTTP 401 (no certificate) or 200 (valid FNMT certificate)
Invoke-WebRequest -Uri "https://my-domain.com/login" -Method POST
```

Expected responses:

| Scenario | HTTP Status | Body |
|----------|-------------|------|
| No certificate | `401 Unauthorized` | `{"message":"Client certificate is required."}` |
| Invalid / untrusted cert | `401 Unauthorized` | `{"message":"Certificate validation failed."}` |
| Valid FNMT cert | `200 OK` | `{"token":"eyJ...","tokenType":"Bearer","expiresIn":3600,...}` |

---

## 9. Security Considerations for Production

| Topic | Recommendation |
|-------|---------------|
| **JWT secret** | Use a cryptographically random secret of ≥ 64 bytes. Rotate periodically. |
| **JWT expiration** | Keep short (15–60 min). Implement refresh-token rotation for long sessions. |
| **Certificate revocation** | `X509RevocationMode.Online` is enabled by default. Ensure CRL/OCSP URLs in the FNMT certs are reachable from the server. For air-gapped environments, use `Offline` and schedule CRL downloads. |
| **TLS version** | Configure IIS to accept TLS 1.2 and TLS 1.3 only. Disable older protocol versions via IIS Crypto or the Windows registry. |
| **HTTPS only** | IIS handles TLS. Do not enable `UseHttpsRedirection()` in ASP.NET Core when behind IIS with SSL configured at the IIS layer. |
| **Logging** | Avoid logging full certificate subjects or DNI/NIE values. Log only serial numbers and outcome. |
| **Access control** | Restrict the `/login` endpoint to TLS-only traffic at the IIS level. |
| **Secret storage** | Never commit `appsettings.json` with a real JWT secret. Use environment variables or a secrets manager. |
| **Chain validation** | Regularly update FNMT CA certificates on the server as FNMT publishes new intermediates. |
