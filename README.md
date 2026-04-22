# GestorDocumentoApp

Aplicacion web ASP.NET Core MVC para gestion de proyectos, elementos, solicitudes de cambio y versionado documental.

## Requisitos

- .NET SDK 9.x
- PostgreSQL 14+ (o compatible)

## Configuracion

Crear/editar el archivo `GestorDocumentoApp/appsettings.Development.json` con al menos:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=GestorDocumentoDb;Username=postgres;Password=tu_password"
  },
  "AdminUser": {
    "Email": "admin@example.com",
    "Password": "admin123"
  },
  "GitHub": {
    "Token": "tu_token_personal_opcional",
    "WebhookSecret": "secreto_para_validar_webhook"
  }
}
```

Notas de GitHub:
- Para validar commits/PR al vincular trazabilidad en CR, se recomienda un token con permisos de lectura del repositorio.
- Webhook disponible en `POST /api/github/webhook` (evento `pull_request`) con firma `X-Hub-Signature-256`.
- Diagnostico rapido de configuracion: `GET /api/github/config-status` (requiere usuario autenticado).

Prueba rapida webhook (PowerShell):

```powershell
$secret = "tu_webhook_secret"
$payload = '{"action":"closed","number":10,"pull_request":{"number":10,"html_url":"https://github.com/owner/repo/pull/10","merged":true,"state":"closed"},"repository":{"full_name":"owner/repo"}}'
$signature = "sha256=" + [System.BitConverter]::ToString((New-Object System.Security.Cryptography.HMACSHA256 ([Text.Encoding]::UTF8.GetBytes($secret))).ComputeHash([Text.Encoding]::UTF8.GetBytes($payload))).Replace("-", "").ToLower()
Invoke-RestMethod -Method Post -Uri "http://localhost:5050/api/github/webhook" -Headers @{ "X-GitHub-Event"="pull_request"; "X-Hub-Signature-256"=$signature; "Content-Type"="application/json" } -Body $payload
```

## Setup rapido (1-click local)

Desde la raiz del repositorio, puedes levantar el flujo tecnico completo con un solo comando:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-preflight.ps1
```

Este comando ejecuta en orden:

1. `dotnet restore`
2. `dotnet format --verify-no-changes` (lint)
3. `dotnet build` con analizadores estaticos habilitados y warnings como error
4. `dotnet test`
5. `dotnet publish`

Luego aplica migraciones y ejecuta la app:

```powershell
dotnet ef database update --project .\GestorDocumentoApp\GestorDocumentoApp.csproj --startup-project .\GestorDocumentoApp\GestorDocumentoApp.csproj
dotnet run --project .\GestorDocumentoApp\GestorDocumentoApp.csproj --urls "http://localhost:5050"
```

## Ejecutar migraciones

Desde la carpeta `GestorDocumentoApp`:

```bash
dotnet ef database update
```

## Ejecutar la aplicacion

Desde la carpeta `GestorDocumentoApp`:

```bash
dotnet run
```

La aplicacion queda disponible en la URL mostrada por consola (por ejemplo `http://localhost:5042`).

## Salud y operacion

- Endpoint de salud: `GET /health`
- En ambiente productivo usar siempre HTTPS y un proxy inverso (IIS/Nginx) terminando TLS.
- Las cookies de autenticacion se emiten con `HttpOnly`, `Secure` y `SameSite=Lax`.
- El pipeline registra cada request con metodo, ruta, status, tiempo y `traceId`.
- Las excepciones no controladas se registran con `traceId` para facilitar diagnostico.
- Rate limiting global habilitado (`429`) para proteger la app ante rafagas de trafico.
- Dashboard usa cache en memoria por usuario (TTL corto) para reducir carga de consultas repetidas.

## Despliegue recomendado (Fase 5)

1. Configurar `ASPNETCORE_ENVIRONMENT=Production`.
2. Definir `ConnectionStrings:DefaultConnection` en variables de entorno/secret manager.
3. Ejecutar migraciones en el servidor:

```bash
dotnet ef database update --project GestorDocumentoApp --startup-project GestorDocumentoApp
```

4. Publicar en modo release:

```bash
dotnet publish GestorDocumentoApp/GestorDocumentoApp.csproj -c Release -o ./publish
```

## Preflight de release (Windows/PowerShell)

Se incluye el script `scripts/release-preflight.ps1` para validar un release de forma repetible:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release-preflight.ps1
```

Opciones utiles:

- Omitir tests: `-SkipTests`
- Omitir lint: `-SkipLint`
- Omitir analisis estatico estricto: `-SkipStaticAnalysis`
- Omitir publish: `-SkipPublish`
- Cambiar configuracion: `-Configuration Release`

## Usuario inicial

Al iniciar la aplicacion se ejecuta un seeder que crea:

- Rol: `Admin`
- Usuario admin usando `AdminUser:Email` y `AdminUser:Password`

## Modulos principales

- Autenticacion (login/logout)
- Proyectos
- Elementos
- Tipos de elemento
- Tipos de requerimiento
- Solicitudes de cambio (CR)
- Versiones
- Dashboard

## Checklist E2E (demo/entrega)

- Autenticar con usuario `Admin`.
- Crear CR y validar auditoria inicial.
- Vincular trazabilidad Git con commit o PR valido.
- Validar `GET /api/github/config-status` (token + webhook secret).
- Simular webhook `pull_request` y confirmar auditoria "Trazabilidad Git Webhook".
- Intentar baselinar sin evidencia Git verificable (debe bloquear).
- Baselinar con commit valido o PR merged (debe permitir).
