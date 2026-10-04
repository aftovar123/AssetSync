# Despliegue e infraestructura (Azure + Bicep)

[← Volver al README](../README.md)

## Producción

Corre en Azure App Service (Linux, .NET 10) conectado a Azure SQL Database,
con CI/CD desde GitHub Actions: cada push a `main` corre las pruebas (flujo
`CI`) y compila, publica y despliega automáticamente (flujo de despliegue).
GitHub Actions se autentica en Azure con
**OIDC** (identidad federada), sin secretos de larga duración guardados en el
repositorio, y esa identidad solo tiene permiso sobre la web app.

Todo corre en el nivel gratuito (App Service F1 + SQL Database serverless con
la oferta gratuita), así que la primera petición tras un rato de inactividad
puede tardar unos segundos extra mientras el App Service y la base de datos
"despiertan" — comportamiento esperado de ese nivel, no un error. La
interfaz de Scalar solo está habilitada en desarrollo: no se expone
documentación interactiva de la API en un entorno público.

## Infraestructura como código (Bicep)

Toda la infraestructura de Azure está descrita en [`infra/`](../infra/) con
Bicep, en vez de depender de clics en el portal:

- App Service F1.
- Azure SQL serverless con la oferta gratuita, que se pausa en vez de cobrar
  si se agota la cuota del mes.
- Application Insights sobre un área de trabajo con límite diario de ingesta.
- La identidad administrada con la que GitHub Actions despliega por OIDC, con
  permiso solo sobre la web app.

```bash
infra/deploy.sh           # what-if: muestra qué cambiaría, sin tocar nada
infra/deploy.sh --apply   # vista previa y, tras confirmar, despliega
```

- **Sin secretos en el repo.** Las claves (JWT, secretos de clientes, cadena
  de SQL, RabbitMQ) se pasan como parámetros `@secure()`: el script las toma
  de variables de entorno o, si no están, reutiliza las que ya tiene la web
  app, así que redesplegar no las cambia.
- **Configuración derivada, no pegada a mano.** La cadena de conexión de
  Application Insights sale del propio recurso. Pegarla a mano mal fue la
  causa de una caída real (ver [Observabilidad](observability.md)).
- **Validado en CI:** cada push compila las plantillas Bicep.
