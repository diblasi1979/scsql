# Changelog

Este proyecto sigue una variante pragmática de Semantic Versioning.

## Unreleased

### Added

- Endpoint `PUT /api/tasks/{id}` para actualizar tareas existentes.
- Endpoint `DELETE /api/tasks/{id}` para eliminar tareas sin ejecuciones activas.
- Modal de edición de tareas desde el panel administrativo.
- Biblioteca local de iconos SVG reutilizables para acciones del frontend.

### Changed

- Se centralizó la validación de creación y actualización de tareas en el backend.
- Se compactaron acciones repetidas de UI con botones iconográficos y tooltips visibles al pasar el mouse o navegar con foco.
- Se redujo el peso visual de botones secundarios en formularios y modales para mejorar densidad operativa.

### Fixed

- El dashboard volvió a usar un único cierre de sesión persistente en el sidebar.
- El layout autenticado y no autenticado quedó mejor ajustado para login centrado y navegación lateral estable.

## v0.1.0 - 2026-04-19

Lanzamiento inicial público de SCSQL.

### Added

- Backend en ASP.NET Core 8 con autenticación JWT y endpoints para dashboard, conexiones, scripts, tareas y ejecuciones.
- Frontend administrativo en Vue 3 + Vite con login, dashboard, conexiones, scripts, tareas e historial.
- Soporte inicial de ejecución sobre MySQL y SQL Server.
- Persistencia administrativa en MySQL interno usando EF Core + Pomelo.
- Scheduler automático con programación por día y hora, múltiples corridas por día y ventana de catch-up.
- Ejecución manual de tareas desde UI y API.
- Carga de archivos `.sql` almacenados en el servidor.
- Historial de ejecuciones con duración, intentos, errores y detalle técnico.
- Cifrado en reposo para contraseñas de conexiones externas.
- Colección reproducible de QA/demo con scripts end-to-end y validación del scheduler.
- Despliegue completo con Docker Compose.

### Changed

- Se reemplazó la persistencia inicial basada en JSON por MySQL interno como fuente principal de verdad.
- Se reforzó el scheduler para evitar depender de la coincidencia exacta de minuto.
- Se modernizó el panel administrativo con una identidad visual más informal, colorida y orientada a un perfil de usuario joven.

### Notes

- Esta versión establece la baseline funcional inicial del producto.
- Los cambios futuros seguirán la convención de versionado documentada en el README.