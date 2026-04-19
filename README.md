# SCSQL

SCSQL es un planificador de consultas SQL y stored procedures con backend en ASP.NET Core 8 y panel administrativo en Vue 3. La implementación actual ya incluye login local, carga de archivos `.sql`, definición de conexiones MySQL y SQL Server, creación de tareas manuales o automáticas, programación por día/hora, reintentos básicos e historial de ejecución con errores detallados.

## Lo que ya hace

- Login local con usuario administrador inicial.
- Panel administrativo en Vue 3.
- Registro de conexiones remotas MySQL y SQL Server.
- Subida y almacenamiento local de scripts `.sql` en el mismo servidor.
- Creación de tareas contra archivo SQL o stored procedure.
- Programación automática por día de semana y horario, con múltiples horarios por día.
- Ejecución manual desde el panel.
- Historial de ejecuciones, intentos, errores y duración.
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

- Las credenciales de conexiones externas aún no están cifradas en reposo.
- No hay edición o borrado de conexiones/tareas/scripts desde UI.
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
- `POST /api/connections/{id}/test`
- `GET/POST /api/scripts`
- `GET/POST /api/tasks`
- `POST /api/tasks/{id}/run`
- `GET /api/executions`
- `GET /api/executions/{id}`

## Próximos pasos recomendados

1. Agregar edición, desactivación y borrado de entidades desde el panel.
2. Añadir rotación gestionada de la clave de cifrado para credenciales externas.
3. Añadir parámetros tipados para stored procedures y validaciones más finas.
4. Añadir health checks compuestos y métricas operativas.
5. Evaluar Quartz.NET solo si necesitas clustering o calendarios más complejos que la política actual.

## QA y demo

- Demo funcional reproducible: `qa/run-end-to-end-demo.sh`
- Validación del scheduler automático: `qa/run-scheduler-check.sh`
- Guía de uso: `qa/README.md`