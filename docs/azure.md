# Infraestructura en Azure

## 1. Objetivo

Documentar los recursos de Azure donde se despliega la API del backend y cómo se configuraron, siguiendo una estrategia de costo cero.

Este documento no contiene credenciales. Las cadenas de conexión, claves y perfiles de publicación se guardan solo en Azure y en los secrets de GitHub.

## 2. Suscripción y grupo de recursos

- Suscripción: Azure for Students.
- Grupo de recursos: `rg-ics2026`.
- Región: Central US.

| Recurso | Nombre | Plan |
|---|---|---|
| Servidor SQL | `ics2026-sqlserver` | — |
| Base de datos | `dsw2025tpi` | Free – General Purpose – Serverless |
| App Service Plan | `ASP-rgics2026-a0a6` | F1 (Free), Linux |
| Web App | `ics2026-api` | Contenedor Linux |

## 3. Azure SQL Database (TP3-06)

### 3.1. Servidor y base

- Servidor lógico `ics2026-sqlserver.database.windows.net` con autenticación SQL.
- Base `dsw2025tpi` con la oferta gratuita de Azure SQL: Serverless Gen5, 2 vCores y hasta 32 GB.
- La oferta incluye 100.000 vCore-segundos por mes. Si se agotan, la base se pausa hasta el mes siguiente (comportamiento `AutoPause`) en lugar de generar costos.
- La base se pausa sola después de 60 minutos sin uso y se reanuda con la primera conexión. Ese arranque tarda unos segundos.

### 3.2. Red y firewall

- TLS mínimo 1.2.
- Excepción "Allow Azure services and resources to access this server" activada. Es la que permite que el App Service se conecte a la base.
- Una regla con la IP del equipo, para administrar la base desde el portal o una PC. Se puede quitar al terminar el TP.

### 3.3. Esquema

La base no se crea a mano. Al iniciar, la API ejecuta `Database.Migrate()` sobre `AuthenticateContext` y `Dsw2025TpiContext`, y luego carga los roles y el usuario administrador definidos en `Dsw2025Tpi.Api/admins.json`.

Las tablas de Identity están renombradas: `Usuarios`, `Roles`, `UsuariosRoles`, etc. Para verificar desde el Query editor:

```sql
SELECT UserName FROM Usuarios;
```

### 3.4. Cadena de conexión

Se obtiene en el portal, en la base > Connection strings > ADO.NET. Nunca se escribe en `appsettings.json` ni en ningún archivo del repositorio: se carga como variable de entorno del App Service (sección 4.2).

## 4. Azure App Service (TP3-07)

### 4.1. Web App

- Web App `ics2026-api`, publicada como contenedor Linux.
- Plan `ASP-rgics2026-a0a6` en F1 (Free). Se creó en B1 y se bajó a F1 para cumplir el costo cero.
- El plan gratuito no tiene Always On: la app se duerme cuando no se usa y la primera petición tarda unos 30 segundos.
- URL pública: https://ics2026-api-emahcag0cvgvajaj.centralus-01.azurewebsites.net/swagger

### 4.2. Variables de entorno

Se configuran en Settings > Environment variables > App settings. Acá se listan solo los nombres.

| Variable | Uso |
|---|---|
| `WEBSITES_PORT` | Puerto del contenedor: `8080` |
| `ASPNETCORE_ENVIRONMENT` | Entorno de ejecución |
| `ConnectionStrings__Dsw2025TpiEntities` | Cadena de conexión a Azure SQL. Reemplaza a `ConnectionStrings:Dsw2025TpiEntities` de `appsettings.json` |
| `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience` | Configuración de JWT, para no usar la clave publicada en `appsettings.json` |

### 4.3. Publicación y logs

- SCM Basic Auth Publishing Credentials activado: lo necesita el publish profile que usa GitHub Actions.
- FTP Basic Auth desactivado, porque no se usa.
- Logs del contenedor en el sistema de archivos, visibles desde Log stream.

## 5. Integración con GitHub

En el repositorio `Grupo-ICSoft/ICS2026-backend`, Settings > Secrets and variables > Actions:

| Tipo | Nombre | Contenido |
|---|---|---|
| Secret | `AZURE_WEBAPP_PUBLISH_PROFILE` | Publish profile descargado de la Web App |
| Variable | `AZURE_WEBAPP_NAME` | `ics2026-api` |

Con estos datos, un workflow de GitHub Actions puede desplegar una imagen en la Web App sin guardar credenciales en el repositorio.

## 6. Verificación

1. Abrir la URL de Swagger. La primera carga puede tardar por el arranque en frío.
2. Ejecutar `POST /api/auth/login` con el usuario administrador de `admins.json`. Debe devolver un token.
3. Con el botón Authorize, cargar `Bearer <token>` y probar un endpoint protegido.

## 7. Problemas conocidos

- Si el contenedor arranca mientras la base está pausada, la migración inicial puede fallar y la API queda sin tablas: el login devuelve 500 con "Invalid object name 'Usuarios'". Se soluciona reiniciando la Web App con la base ya activa. Para evitarlo, se puede agregar `EnableRetryOnFailure()` a la conexión con SQL Server, así EF Core reintenta mientras la base se reanuda.
