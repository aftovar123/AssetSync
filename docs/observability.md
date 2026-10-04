# Observabilidad y logging

[← Volver al README](../README.md)

## OpenTelemetry → Application Insights

Trazas y métricas con OpenTelemetry: peticiones HTTP entrantes, llamadas
HTTP salientes, comandos a la base de datos (SQL Server o PostgreSQL),
métricas del runtime de .NET, y telemetría propia del dominio:

- **`outbox.process_batch`**: una traza por cada lote del outbox, con la
  consulta única que precarga sus órdenes y, debajo, una
  **`erp.sync_work_order`** por orden (con su código de envío y, si falla,
  la excepción).
- **`assetsync.outbox.messages`**: mensajes manejados por resultado
  (`processed`, `retry`, `failed`).
- **`assetsync.erp.sync.duration`**: duración de cada sincronización con el
  ERP, reintentos de Polly incluidos.

La instrumentación es siempre la misma; el destino depende solo de la
configuración:

| Variable | Destino |
|---|---|
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Azure Monitor / Application Insights (producción) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Cualquier backend OTLP, p. ej. el Aspire Dashboard en local |
| Ninguna | No se exporta nada (tests, ejecuciones locales simples) |

Las capas `Application` y `Domain` no dependen de OpenTelemetry: la
telemetría propia usa `ActivitySource` y `Meter` de .NET, y es la API la
que decide si alguien escucha.

**La telemetría nunca tumba la API.** Una cadena de conexión de Application
Insights mal pegada (la clave sola en vez de la cadena completa) causó una
caída real en producción: el exportador fallaba al arrancar y la app entraba
en reinicios. Ahora la cadena se valida antes de activar el exportador; si
está mal, la API arranca igual sin enviar telemetría y deja una advertencia
en el log. Además, la [infraestructura como código](infrastructure.md) genera
esa cadena desde el propio recurso en vez de pegarla a mano.

**Control de costos.** El procesador del outbox consulta cada 10 segundos
aunque no haya nada pendiente; registrado tal cual, serían unas 8.600
trazas SQL al día sin información útil. Un *sampler* descarta las llamadas
salientes sin padre (SQL o HTTP fuera de una petición o de un lote). También
se excluyen `/health`, la documentación de la API, la raíz y la sonda con la
que App Service comprueba que el contenedor arrancó (`/robots933456.txt`),
que siempre responden 404 y solo ensuciarían los gráficos de errores. Los
logs siguen en Serilog y no se exportan. En Azure, el área de trabajo tiene
un límite diario de ingesta para no salir del nivel gratuito.

Para verlo en local, con el
[Aspire Dashboard](https://learn.microsoft.com/dotnet/aspire/fundamentals/dashboard/standalone)
en Docker:

```bash
docker run -d --name aspire-dashboard -p 127.0.0.1:18888:18888 -p 127.0.0.1:4317:18889 \
  -e DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true mcr.microsoft.com/dotnet/aspire-dashboard:latest
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 dotnet run --project src/AssetSync.Api
# panel en http://localhost:18888
```

## Logging estructurado (Serilog)

Reemplaza el logger por defecto de ASP.NET Core — configurado por completo
desde `appsettings.json` (nivel mínimo, sinks, overrides por namespace), no
hardcodeado en `Program.cs`. Dos sinks activos: consola y un archivo rotado
por día (`logs/assetsync-YYYYMMDD.log`, se conservan 14 días). Todo lo que
ya usaba `ILogger<T>` — los reintentos de `ResilientErpClient`, el
`GlobalExceptionHandler`, el `OutboxProcessor` — fluye por Serilog sin tocar
esos archivos, más `UseSerilogRequestLogging()` para una línea estructurada
por request (método, ruta, código, duración):

```
[08:44:36 INF] HTTP GET /assets responded 200 in 496.8430 ms
[08:44:59 INF] Outbox processor handled 1 pending message(s).
```
