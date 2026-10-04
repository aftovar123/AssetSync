# Autenticación y autorización (JWT, OAuth2 client credentials)

[← Volver al README](../README.md)

El flujo es el estándar OAuth2 para comunicación máquina a máquina
(*client credentials*, RFC 6749 §4.4) — el mismo que usaría el ERP para
llamar a esta API:

1. El cliente pide un token a `POST /auth/token` con
   `grant_type=client_credentials`, `client_id` y `client_secret` (en el
   formulario o como HTTP Basic), y opcionalmente `scope` para pedir solo
   una parte de sus permisos.
2. La API responde un **JWT firmado con HMAC-SHA256** que expira en 60
   minutos, con el claim `scope` (lista separada por espacios, RFC 9068).
3. El cliente lo envía como `Authorization: Bearer <token>`. El middleware
   `JwtBearer` valida firma, emisor, audiencia, algoritmo y expiración, y
   la política del endpoint exige el scope de su área.

## Un scope por área

Cada cliente recibe solo lo que su función necesita — la integración del ERP
puede completar órdenes pero no crear activos:

| Scope | Permite |
|---|---|
| `assets.write` | Crear activos |
| `workorders.write` | Crear y completar órdenes de trabajo |
| `integration.read` | Ver el outbox y los logs de integración (estado interno de la sincronización) |

Los clientes se configuran con sus scopes permitidos (`Auth:Clients`). Sin
token, un endpoint protegido responde `401`; con un token válido pero sin
el scope que exige, `403`. Las lecturas de activos y órdenes, y `/health`,
siguen públicas para que el demo en vivo se pueda explorar.

```bash
curl -X POST https://localhost:<puerto>/auth/token \
  -d grant_type=client_credentials -d client_id=erp-integration -d client_secret=<secreto>
# {"access_token":"eyJhbGciOiJIUzI1NiIs...","token_type":"Bearer","expires_in":3600,
#  "scope":"workorders.write integration.read"}

curl -X POST https://localhost:<puerto>/auth/token \
  -d grant_type=client_credentials -d client_id=erp-integration -d client_secret=<secreto> \
  -d scope=assets.write
# {"error":"invalid_scope"}   ← no puede pedir un permiso que no tiene
```

La matriz completa endpoint × cliente se verifica en las
[pruebas de integración](testing.md).

## Decisiones de seguridad

- **Sin clave, no arranca.** Si `Jwt:SigningKey` falta o tiene menos de 32
  bytes, la aplicación falla al iniciar en vez de correr con las escrituras
  desprotegidas o firmadas con una clave por defecto adivinable. Lo mismo
  con un scope mal escrito en la configuración de un cliente: el error dice
  qué cliente y qué scope, en vez de dejar al cliente sin acceso sin pista.
- **Secretos fuera de git**: user-secrets en local, configuración del App
  Service en Azure (`Jwt__SigningKey`, `Auth__Clients__0__ClientSecret`, ...),
  pasados como parámetros `@secure()` en la [infraestructura](infrastructure.md).
- **Comparación en tiempo constante** de las credenciales
  (`CryptographicOperations.FixedTimeEquals` sobre hashes, contra todos los
  clientes), para no filtrar por tiempo de respuesta qué cliente existe ni
  cuántos caracteres coinciden.
- **Errores en formato OAuth2** (`invalid_client`, `invalid_scope`,
  `unsupported_grant_type`) y `Cache-Control: no-store` en la respuesta del
  token, como pide el RFC.
- El intento de fuerza bruta contra `/auth/token` queda acotado por el
  rate limiting de 60 peticiones por minuto por IP.
- Scalar declara el esquema Bearer, así que en desarrollo se puede pegar el
  token en su botón de autorización y probar los endpoints protegidos desde
  la interfaz.
