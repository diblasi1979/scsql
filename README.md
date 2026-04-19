# SCSQL

SCSQL es un planificador de consultas SQL y stored procedures con backend en ASP.NET Core 8 y panel administrativo en Vue 3. La implementación actual ya incluye login local, carga de archivos `.sql`, definición de conexiones MySQL y SQL Server, creación y edición de tareas manuales o automáticas, programación por día/hora, reintentos básicos e historial de ejecución con errores detallados.

## Lo que ya hace

- Login local con usuario administrador inicial.
- Panel administrativo en Vue 3.
- Registro de conexiones remotas MySQL y SQL Server.
- Edición y borrado de conexiones remotas MySQL y SQL Server desde el panel.
- Subida, edición y almacenamiento local de scripts `.sql` en el mismo servidor.
- Creación, edición y borrado de tareas contra archivo SQL o stored procedure.
- Programación automática por día de semana y horario, con múltiples horarios por día.
- Ejecución manual desde el panel.
- Historial de ejecuciones, intentos, errores y duración.
- Panel visual renovado con sidebar persistente, login centrado, modal de edición y acciones iconográficas compactas con tooltip.
- Scheduler interno que reclama ocurrencias pendientes por horario y evita depender de una coincidencia exacta de minuto.
- Scheduler con zona horaria explícita para ejecuciones automáticas consistentes dentro de Docker.
- Persistencia administrativa en MySQL interno del sistema.
- Contraseñas de conexiones externas cifradas en reposo y omitidas de las respuestas del API.
- Importación automática del estado heredado desde `app-state.json` si existe en el volumen previo.

## Arquitectura actual

- `backend/src/ScSql.Api`: API ASP.NET Core 8, autenticación JWT, scheduler y motores de ejecución.
- `mysql-internal`: base MySQL interna para usuarios, conexiones, scripts, tareas y ejecuciones.
- `frontend`: panel Vue 3 + Vite.
- `storage/`: almacenamiento local de archivos `.sql` y archivos heredados importables.
- `qa/`: scripts reproducibles para QA funcional y demo comercial.

## Limitaciones actuales

- Todavía no hay rotación automática de la clave usada para cifrar credenciales externas.
- No hay aún reemplazo masivo, versionado o papelera para conexiones y scripts eliminados.
- No hay refresh token, auditoría de cambios ni control de concurrencia distribuida.

## Ejecutar con Docker

```bash
docker compose up --build
```

Servicios:

- Frontend: `http://localhost:1979`
- API: `http://localhost:2008`
- Swagger: `http://localhost:2008/swagger`
- MySQL interna: `localhost:3308`

Credenciales por defecto:

- Usuario: `admin`
- Clave: `admin123`

## Endpoints principales

- `POST /api/auth/login`
- `GET /api/dashboard`
- `GET/POST /api/connections`
- `PUT /api/connections/{id}`
- `DELETE /api/connections/{id}`
- `POST /api/connections/{id}/test`
- `GET/POST /api/scripts`
- `PUT /api/scripts/{id}`
- `DELETE /api/scripts/{id}`
- `GET/POST /api/tasks`
- `PUT /api/tasks/{id}`
- `DELETE /api/tasks/{id}`
- `POST /api/tasks/{id}/run`
- `GET /api/executions`
- `GET /api/executions/{id}`

## Próximos pasos recomendados

1. Añadir versionado o papelera de recuperación para scripts y conexiones eliminadas.
2. Añadir rotación gestionada de la clave de cifrado para credenciales externas.
3. Añadir parámetros tipados para stored procedures y validaciones más finas.
4. Añadir health checks compuestos y métricas operativas.
5. Evaluar Quartz.NET solo si necesitas clustering o calendarios más complejos que la política actual.

## QA y demo

- Demo funcional reproducible: `qa/run-end-to-end-demo.sh`
- Validación del scheduler automático: `qa/run-scheduler-check.sh`
- Guía de uso: `qa/README.md`

## Changelog

- Historial de cambios: `CHANGELOG.md`

## Convención de versiones

SCSQL usa una convención basada en Semantic Versioning con foco práctico:

- `MAJOR`: cambios incompatibles de API, modelo de datos, contratos de integración o despliegue.
- `MINOR`: nuevas capacidades compatibles, por ejemplo nuevos motores, nuevas pantallas, edición de entidades o mejoras funcionales del scheduler.
- `PATCH`: correcciones compatibles, mejoras de QA, ajustes visuales, endurecimiento operativo o fixes de bugs.

Regla de trabajo sugerida:

1. Mientras el producto siga en maduración inicial, versiones `0.x.y` indican que todavía puede haber ajustes relevantes entre minors.
2. Usar `0.x.0` para hitos funcionales visibles o entregables comerciales claros.
3. Usar `0.x.z` para fixes y endurecimiento sin ampliar alcance funcional.
4. Pasar a `1.0.0` cuando contratos, despliegue y flujos principales estén suficientemente estabilizados para producción formal.