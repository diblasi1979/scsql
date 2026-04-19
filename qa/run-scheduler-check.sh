#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR=$(cd "$(dirname "$0")/.." && pwd)
API_BASE_URL=${API_BASE_URL:-http://localhost:2008/api}
STATE_FILE="$ROOT_DIR/qa/results/latest-demo.env"

if [[ -f "$STATE_FILE" ]]; then
  # shellcheck disable=SC1090
  source "$STATE_FILE"
fi

TARGET_CONTAINER=${TARGET_CONTAINER:-scsql-qa-target-mysql}
TARGET_DB=${TARGET_DB:-demo}
TARGET_USER=${TARGET_USER:-root}
TARGET_PASSWORD=${TARGET_PASSWORD:-rootpass}
CONNECTION_NAME=${CONNECTION_NAME:-QA Demo Target MySQL}

TARGET_PORT=${TARGET_PORT:-$(docker inspect -f '{{range $p, $bindings := .NetworkSettings.Ports}}{{if eq $p "3306/tcp"}}{{(index $bindings 0).HostPort}}{{end}}{{end}}' "$TARGET_CONTAINER" 2>/dev/null || true)}

if [[ -z "$TARGET_PORT" ]]; then
  echo "No se pudo resolver el puerto del contenedor $TARGET_CONTAINER. Ejecuta primero qa/run-end-to-end-demo.sh"
  exit 1
fi

login() {
  curl -s -X POST "$API_BASE_URL/auth/login" \
    -H 'Content-Type: application/json' \
    -d '{"username":"admin","password":"admin123"}' | jq -r '.token'
}

TOKEN=$(login)
NOW_PLUS_ONE_MINUTE=$(date -v+1M +%H:%M)
TODAY=$(date +%w)

CONNECTION_ID=$(curl -s "$API_BASE_URL/connections" -H "Authorization: Bearer $TOKEN" | jq -r --arg connectionName "$CONNECTION_NAME" '.[] | select(.name == $connectionName) | .id' | tail -n 1)
if [[ -z "$CONNECTION_ID" ]]; then
  echo "No se encontró la conexión de QA. Ejecuta primero qa/run-end-to-end-demo.sh"
  exit 1
fi

SCRIPT_RESPONSE=$(curl -s -X POST "$API_BASE_URL/scripts" -H "Authorization: Bearer $TOKEN" -F "file=@$ROOT_DIR/qa/assets/success-demo.sql")
SCRIPT_ID=$(printf '%s' "$SCRIPT_RESPONSE" | jq -r '.id')

TASK_RESPONSE=$(curl -s -X POST "$API_BASE_URL/tasks" \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d "{
    \"name\": \"QA Automatic Scheduler Task\",
    \"connectionId\": \"$CONNECTION_ID\",
    \"engine\": 0,
    \"sourceKind\": 0,
    \"sqlScriptId\": \"$SCRIPT_ID\",
    \"parameters\": [],
    \"automatic\": true,
    \"enabled\": true,
    \"schedules\": [{ \"dayOfWeek\": $TODAY, \"time\": \"$NOW_PLUS_ONE_MINUTE\" }],
    \"retryPolicy\": { \"maxRetries\": 0, \"delayMinutes\": 1 },
    \"timeoutSeconds\": 300
  }")
TASK_ID=$(printf '%s' "$TASK_RESPONSE" | jq -r '.id')

echo "Tarea automática creada para $NOW_PLUS_ONE_MINUTE. Esperando ejecución automática..."
for _ in $(seq 1 18); do
  EXECUTIONS=$(curl -s "$API_BASE_URL/executions" -H "Authorization: Bearer $TOKEN")
  HIT=$(printf '%s' "$EXECUTIONS" | jq -r --arg taskId "$TASK_ID" '.[] | select(.taskId == $taskId and .manualTrigger == false) | .id' | head -n 1)
  if [[ -n "$HIT" ]]; then
    echo "Scheduler OK. ExecutionId=$HIT"
    exit 0
  fi
  sleep 5
done

echo "No se detectó ejecución automática dentro de la ventana esperada."
exit 1