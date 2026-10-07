# Diseño: sincronización, errores y acceso a datos

[← Volver al README](../README.md)

## Outbox transaccional y reintentos

Al completar una orden de trabajo hay que avisarle a un sistema externo, y
eso puede fallar de formas muy distintas: la red se cae, el proceso se
reinicia a mitad de un reintento, o no se sabe si la solicitud llegó. Dos
capas resuelven esto:

1. **Outbox transaccional.** `CompleteWorkOrderCommand` marca el estado **y**
   encola la intención de sincronizar en la misma llamada a `SaveChanges` —
   una sola transacción. No puede pasar que uno se guarde sin el otro.
2. **Reintento con backoff, separado del negocio.** Un `BackgroundService`
   revisa el outbox cada 10s y dispara `SyncWorkOrderCommand`, que genera un
   **código de idempotencia** reutilizado en cada reintento, llama a
   `IExternalErpClient` una sola vez desde su punto de vista (quien reintenta
   de verdad es `ResilientErpClient`, un decorador con **Polly**: backoff
   exponencial + jitter), y registra cada resultado en `IntegrationLog`.

Si el mensaje sigue fallando tras 5 intentos, `OutboxMessage` se marca a sí
mismo como `Failed` — una regla del dominio, no una consulta implícita — y
deja de reintentarse para siempre.

**Claim atómico.** Tomar el mensaje pendiente y reintentarlo son dos pasos
distintos: si dos instancias de `OutboxProcessor` corrieran a la vez, un
simple `SELECT` seguido de un `UPDATE` dejaría una ventana donde ambas
podrían tomar el mismo mensaje y sincronizarlo dos veces. `ClaimPendingAsync`
lo resuelve con una sola sentencia atómica que mueve el mensaje a
`Processing` y lo devuelve a la vez:

- **SQL Server:** `UPDATE TOP (n) ... OUTPUT INSERTED.Id`.
- **PostgreSQL:** `UPDATE ... RETURNING` sobre una subconsulta con
  `FOR UPDATE SKIP LOCKED`, el patrón clásico de cola en Postgres: un proceso
  salta las filas que otro está tomando en vez de esperarlas.

Un mensaje que quedó en `Processing` más de 2 minutos (su instancia se cayó a
mitad del proceso) vuelve a estar disponible para reclamarse, en vez de
perderse para siempre.

## Eventos con RabbitMQ

Cuando `SyncWorkOrderCommandHandler` sincroniza una orden de trabajo con
éxito, publica un `WorkOrderSyncedEvent` a una cola de RabbitMQ (probado
contra una instancia gratuita de [CloudAMQP](https://www.cloudamqp.com/),
sobre LavinMQ — mismo protocolo AMQP 0.9.1, mismo cliente .NET) — separado
tanto de `IExternalErpClient` (que habla con el sistema externo) como de
`INotificationService` (que avisa a una persona). Otros sistemas
(reportes, analítica, servicios corriente abajo) pueden suscribirse sin
tener que sondear la base de datos ni el ERP.

Es **best-effort a propósito**: si el broker falla o no está configurado,
`RabbitMqEventPublisher` registra una advertencia y continúa — nunca hace que
se reporte como fallida una sincronización con el ERP que en realidad sí tuvo
éxito. Por la misma razón, el health check de `messaging` responde `Degraded`
(no `Unhealthy`) cuando el broker no está disponible, para no tumbar todo
`/health` a `503` por un problema en una función complementaria.

`WorkOrderSyncedConsumer` (un `BackgroundService` más, mismo patrón que
`OutboxProcessor`) se suscribe a esa misma cola dentro del propio proceso —
arquitectónicamente idéntico a un servicio separado — y confirma que el
evento efectivamente se recibió.

## Validación y manejo de errores

`POST /assets` y `POST /work-orders` pasan por comandos MediatR
(`CreateAssetCommand`, `CreateWorkOrderCommand`), igual que el resto de
`Application`. Un `ValidationBehavior<TRequest, TResponse>` — un pipeline
behavior de MediatR registrado una sola vez — corre todos los
`IValidator<TRequest>` de FluentValidation **antes** de cualquier handler:
si algo falla, lanza `ValidationException` y el handler nunca se ejecuta. La
regla de `AssetId` en `CreateWorkOrderCommandValidator` es un `MustAsync` que
consulta `IAssetRepository` de verdad — no solo "¿es mayor que cero?", sino
"¿ese activo existe?".

Un `GlobalExceptionHandler` (`IExceptionHandler`) traduce eso a respuestas
HTTP: `ValidationException` → `400` con el detalle por campo,
`NotFoundException` → `404`, una petición que ASP.NET Core no pudo leer (JSON
mal formado, un texto donde va un número) → `400`, y cualquier otra
excepción → `500` genérico sin filtrar detalles internos:

```bash
$ curl -X POST http://localhost:5188/work-orders -d '{"assetId":9999,"description":""}'
{"errors":{"AssetId":["Asset 9999 does not exist."],"Description":["'Description' no debería estar vacío."]}}
# HTTP 400

$ curl -X POST http://localhost:5188/work-orders/99999/complete
{"title":"Work order 99999 not found.","status":404}
```

## Acceso a datos

- **Dos motores.** `Database:Provider` elige SQL Server (por defecto, como en
  producción) o PostgreSQL. Cada uno tiene sus propias migraciones, porque EF
  Core las genera para un proveedor concreto: las de SQL Server viven en
  `AssetSync.Infrastructure` y las de PostgreSQL en
  `AssetSync.Migrations.PostgreSql`. Un valor desconocido hace fallar el
  arranque.
- **Reintentos ante fallos transitorios.** `EnableRetryOnFailure` reintenta
  con backoff exponencial los errores pasajeros de la base de datos — una
  transacción elegida como víctima de un deadlock, una conexión caída, o la
  base de datos serverless de Azure todavía despertando de su pausa
  automática — en vez de devolver un `500`. Como cada escritura es un único
  `SaveChanges`, no hay transacciones manuales que envolver.
- **Una consulta por lote, no una por mensaje.** El procesador del outbox
  toma hasta 10 mensajes por ciclo. Antes cada uno consultaba su orden de
  trabajo por separado (el clásico N+1); ahora `PreloadAsync` las trae todas
  en un solo `WHERE Id IN (...)` y `GetByIdAsync` las sirve desde el
  *change tracker* de EF Core sin volver a la base de datos.
- **Listados paginados.** Todos los `GET` de listas aceptan
  `?page=1&pageSize=20` (máximo 100 por página) y responden
  `{ items, page, pageSize, totalCount, totalPages }`, con un `ORDER BY`
  estable y sin *change tracking*. Los valores fuera de rango se ajustan en
  vez de rechazarse, así que ningún cliente puede pedir la tabla completa.
- **Formato consistente.** Los estados se devuelven como texto
  (`"Processed"`, `"Completed"`) y todas las fechas en UTC con la `Z` final,
  tanto al crear un registro como al leerlo de la base: SQL Server no guarda
  el tipo de fecha, así que un convertidor de EF Core las marca como UTC al
  leerlas.

## Health checks

`GET /health` no solo confirma que el proceso está vivo: incluye un check de
la base de datos (`CanConnectAsync`), el de `messaging` y uno propio del
dominio, `outbox`, que se pone en `Degraded` si algún mensaje llegó a
`Failed` (agotó sus reintentos) — algo que un simple ping a la base de datos
nunca revelaría.

```json
{"status":"Healthy","checks":[
  {"name":"database","status":"Healthy","description":"Database reachable."},
  {"name":"outbox","status":"Healthy","description":"No dead-lettered outbox messages."},
  {"name":"messaging","status":"Healthy","description":"RabbitMQ reachable."}
]}
```

## Rate limiting

Cada endpoint (salvo `/health`, que Azure y las herramientas de monitoreo
necesitan llamar sin restricción) aplica un límite de **60 peticiones por
minuto por dirección IP**, con ventana fija y sin cola: la petición 61 en
adelante recibe `429 Too Many Requests` de inmediato, sin encolarse ni
consumir hilos del plan gratuito de App Service. También acota los intentos
de fuerza bruta contra `/auth/token`.

## Reloj inyectable

`IClock`/`SystemClock` reemplaza `DateTime.UtcNow` en toda la lógica de
negocio — mismo patrón que uso en [Questlog](https://github.com/aftovar123/questlog).
Permite que los tests fijen un instante exacto en vez de asumir cuándo corrió
la prueba.
