# Pruebas

[← Volver al README](../README.md)

```bash
dotnet test                                   # todo (las de integración necesitan Docker)
dotnet test tests/AssetSync.Tests             # solo unitarias, en un segundo y sin Docker
dotnet test tests/AssetSync.IntegrationTests  # solo integración, contra SQL Server
ASSETSYNC_TEST_DATABASE=PostgreSql dotnet test tests/AssetSync.IntegrationTests  # contra PostgreSQL
```

**122 tests** (95 unitarias y 27 de integración) que corren en GitHub
Actions en cada push; las de integración, dos veces: contra SQL Server y
contra PostgreSQL.

## Integración: la API real contra bases de datos reales

27 tests que levantan la aplicación completa con `WebApplicationFactory`
—el mismo `Program`, middleware, políticas de autorización y mapeos de EF
Core que producción— contra una base de datos desechable que
[Testcontainers](https://testcontainers.com/) crea en Docker al empezar y
destruye al terminar: SQL Server por defecto, o PostgreSQL con
`ASSETSYNC_TEST_DATABASE=PostgreSql`. Así se prueba lo que una base en
memoria no puede ejecutar, como el claim atómico del outbox y las
migraciones de cada motor.

Solo tres cosas difieren de producción, a propósito: el cliente ERP siempre
responde bien (el simulado falla al azar), el ciclo del outbox se dispara a
mano en vez de cada 10 segundos, y el rate limiting se levanta porque todas
las peticiones de prueba salen de la misma IP.

- **Permisos**: la matriz completa endpoint × cliente (`401`, `403` o
  permitido), un token limitado a `integration.read` que no puede escribir,
  y un token alterado rechazado.
- **Flujo de punta a punta**: token → activo → tres órdenes → completarlas
  (`202`, todavía sin sincronizar) → procesar el outbox → órdenes
  sincronizadas, mensajes `Processed` y log de integración enviado; y
  completar dos veces la misma orden no la envía dos veces.
- **Claim atómico del outbox**: dos procesadores reclamando a la vez desde
  conexiones separadas nunca obtienen el mismo mensaje; y un mensaje
  atascado en `Processing` se recupera solo después de los 2 minutos,
  nunca uno que otro procesador está trabajando.
- **Paginación y validación por HTTP**: páginas estables y sin
  solapamiento, `pageSize` limitado a 100, `400` con errores por campo, y
  `/health` reportando la base real.

## Unitarias

95 tests con xUnit y Moq — sin base de datos real, sin reloj del sistema, y
sin esperar tiempo real salvo donde se prueba backoff de verdad:

- **Outbox y sincronización**: `SyncWorkOrderCommandHandler`,
  `CompleteWorkOrderCommandHandler`, `ProcessOutboxCommandHandler` y
  `OutboxMessage` (dominio puro) — éxito, duplicados, fallos, la regla de
  `Failed` tras el máximo de intentos, y que un reintento fallido pero por
  debajo del máximo vuelve a `Pending`.
- **Resiliencia**: `ResilientErpClient` reintenta ante fallos transitorios
  con el mismo código de idempotencia, y se rinde tras los intentos
  configurados.
- **Validación y errores**: `ValidationBehavior`, los validadores de
  creación (incluido el `AssetId` async) y `GlobalExceptionHandler`.
- **Acceso a datos**: la precarga del lote en una sola consulta y la
  paginación (página por defecto, última, fuera de rango, límites de
  `pageSize`, sin *change tracking*).
- **Autenticación y autorización**: `TokenService`, la verificación de
  scopes (sin coincidencias parciales) y el endpoint `/auth/token`.
- **Health checks y observabilidad**: `outbox` en `Degraded` con mensajes
  `Failed`, la validación de la cadena de Application Insights y las rutas
  que no se trazan.
