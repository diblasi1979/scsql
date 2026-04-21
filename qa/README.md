# QA y Demo Reproducible

Este directorio deja una colección reproducible para QA funcional y demo comercial sobre el stack Docker de Novent.

## Prerrequisitos

- Docker Desktop corriendo.
- El stack principal levantado con `docker compose up --build`.
- `curl` y `jq` disponibles en el host.

## Scripts incluidos

- `qa/run-end-to-end-demo.sh`
  - levanta una MySQL destino fresca para esa corrida
  - hace login en la API
  - crea una conexión externa
  - prueba conexión
  - sube un archivo SQL válido
  - crea y ejecuta una tarea manual por archivo SQL
  - crea y ejecuta una tarea manual por stored procedure
  - sube un archivo SQL inválido
  - crea y ejecuta una tarea fallida
  - genera un reporte consolidado en `qa/results/end-to-end-demo-report.json`
  - deja el estado reutilizable de esa corrida en `qa/results/latest-demo.env`

- `qa/run-scheduler-check.sh`
  - crea una tarea automática a un minuto en el futuro
  - espera la ejecución automática
  - falla con código distinto de cero si no detecta ejecución dentro de la ventana esperada

## Uso recomendado

```bash
chmod +x qa/run-end-to-end-demo.sh qa/run-scheduler-check.sh
qa/run-end-to-end-demo.sh
qa/run-scheduler-check.sh
```

## Evidencias

- Reporte JSON consolidado: `qa/results/end-to-end-demo-report.json`
- Estado reutilizable de la última corrida: `qa/results/latest-demo.env`
- Resultado del scheduler: salida estándar del script `qa/run-scheduler-check.sh`

## Notas

- La MySQL destino de QA se crea con un nombre y puerto host libres por cada corrida para evitar contaminación entre pruebas.
- El chequeo del scheduler reutiliza automáticamente la conexión creada por la última demo leyendo `qa/results/latest-demo.env`.
- La MySQL interna del sistema corre separada dentro de `docker compose` y persiste el estado administrativo de la aplicación.