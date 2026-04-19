#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR=$(cd "$(dirname "$0")/.." && pwd)
API_BASE_URL=${API_BASE_URL:-http://localhost:2008/api}
RUN_ID=${RUN_ID:-$(date +%Y%m%d%H%M%S)}
TARGET_CONTAINER=${TARGET_CONTAINER:-scsql-qa-target-mysql-$RUN_ID}
TARGET_PORT=${TARGET_PORT:-3307}
TARGET_DB=${TARGET_DB:-demo}
TARGET_USER=${TARGET_USER:-root}
TARGET_PASSWORD=${TARGET_PASSWORD:-rootpass}
CONNECTION_NAME=${CONNECTION_NAME:-QA Demo Target MySQL $RUN_ID}
RESULTS_DIR="$ROOT_DIR/qa/results"
STATE_FILE="$RESULTS_DIR/latest-demo.env"
mkdir -p "$RESULTS_DIR"

pick_available_port() {
  for candidate in "$TARGET_PORT" 3317 3327 3337 3347; do
    if ! docker ps --format '{{.Ports}}' | grep -q "0.0.0.0:${candidate}->"; then
      echo "$candidate"
      return 0
    fi
  done

  echo "No se encontró un puerto libre para la MySQL de QA" >&2
  exit 1
}

ensure_target_mysql() {
  TARGET_PORT=$(pick_available_port)
  docker rm -f "$TARGET_CONTAINER" >/dev/null 2>&1 || true
  docker run -d --name "$TARGET_CONTAINER" \
    -e MYSQL_ROOT_PASSWORD="$TARGET_PASSWORD" \
    -e MYSQL_DATABASE="$TARGET_DB" \
    -p "$TARGET_PORT:3306" \
    mysql:8.4 >/dev/null

  until docker exec "$TARGET_CONTAINER" mysql -u"$TARGET_USER" -p"$TARGET_PASSWORD" -D "$TARGET_DB" -e "SELECT 1" >/dev/null 2>&1; do
    sleep 2
  done
}

login() {
  curl -s -X POST "$API_BASE_URL/auth/login" \
    -H 'Content-Type: application/json' \
    -d '{"username":"admin","password":"admin123"}' | jq -r '.token'
}

auth_post_json() {
  local path=$1
  local payload=$2
  curl -s -X POST "$API_BASE_URL$path" \
    -H "Authorization: Bearer $TOKEN" \
    -H 'Content-Type: application/json' \
    -d "$payload"
}

auth_get() {
  local path=$1
  curl -s "$API_BASE_URL$path" -H "Authorization: Bearer $TOKEN"
}

ensure_target_mysql
TOKEN=$(login)

CONNECTION_RESPONSE=$(auth_post_json /connections "{
  \"name\": \"$CONNECTION_NAME\",
  \"engine\": 0,
  \"server\": \"host.docker.internal\",
  \"port\": $TARGET_PORT,
  \"database\": \"$TARGET_DB\",
  \"username\": \"$TARGET_USER\",
  \"password\": \"$TARGET_PASSWORD\",
  \"trustServerCertificate\": false
}")
CONNECTION_ID=$(printf '%s' "$CONNECTION_RESPONSE" | jq -r '.id')

CONNECTION_TEST=$(curl -s -X POST "$API_BASE_URL/connections/$CONNECTION_ID/test" -H "Authorization: Bearer $TOKEN")

SUCCESS_SCRIPT_RESPONSE=$(curl -s -X POST "$API_BASE_URL/scripts" -H "Authorization: Bearer $TOKEN" -F "file=@$ROOT_DIR/qa/assets/success-demo.sql")
SUCCESS_SCRIPT_ID=$(printf '%s' "$SUCCESS_SCRIPT_RESPONSE" | jq -r '.id')

SUCCESS_TASK_RESPONSE=$(auth_post_json /tasks "{
  \"name\": \"QA Success SQL Task\",
  \"connectionId\": \"$CONNECTION_ID\",
  \"engine\": 0,
  \"sourceKind\": 0,
  \"sqlScriptId\": \"$SUCCESS_SCRIPT_ID\",
  \"parameters\": [],
  \"automatic\": false,
  \"enabled\": true,
  \"schedules\": [],
  \"retryPolicy\": { \"maxRetries\": 0, \"delayMinutes\": 1 },
  \"timeoutSeconds\": 300
}")
SUCCESS_TASK_ID=$(printf '%s' "$SUCCESS_TASK_RESPONSE" | jq -r '.id')
SUCCESS_RUN_RESPONSE=$(curl -s -X POST "$API_BASE_URL/tasks/$SUCCESS_TASK_ID/run" -H "Authorization: Bearer $TOKEN")

docker exec "$TARGET_CONTAINER" mysql -u"$TARGET_USER" -p"$TARGET_PASSWORD" -D "$TARGET_DB" <<'SQL' >/dev/null
CREATE TABLE IF NOT EXISTS scheduled_demo_runs (
  id INT AUTO_INCREMENT PRIMARY KEY,
  note VARCHAR(100) NOT NULL,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
SQL

docker exec "$TARGET_CONTAINER" mysql -u"$TARGET_USER" -p"$TARGET_PASSWORD" -D "$TARGET_DB" --delimiter=// -e "DROP PROCEDURE IF EXISTS usp_insert_demo_run// CREATE PROCEDURE usp_insert_demo_run(IN p_note VARCHAR(100)) BEGIN INSERT INTO scheduled_demo_runs (note) VALUES (p_note); END //" >/dev/null

docker exec "$TARGET_CONTAINER" mysql -u"$TARGET_USER" -p"$TARGET_PASSWORD" -D "$TARGET_DB" -Nse "SELECT COUNT(*) FROM information_schema.routines WHERE routine_schema = DATABASE() AND routine_type = 'PROCEDURE' AND routine_name = 'usp_insert_demo_run'" | grep -qx '1'

SP_TASK_RESPONSE=$(auth_post_json /tasks "{
  \"name\": \"QA Stored Procedure Task\",
  \"connectionId\": \"$CONNECTION_ID\",
  \"engine\": 0,
  \"sourceKind\": 1,
  \"storedProcedureName\": \"usp_insert_demo_run\",
  \"parameters\": [{ \"name\": \"p_note\", \"value\": \"run from qa stored procedure\" }],
  \"automatic\": false,
  \"enabled\": true,
  \"schedules\": [],
  \"retryPolicy\": { \"maxRetries\": 0, \"delayMinutes\": 1 },
  \"timeoutSeconds\": 300
}")
SP_TASK_ID=$(printf '%s' "$SP_TASK_RESPONSE" | jq -r '.id')
SP_RUN_RESPONSE=$(curl -s -X POST "$API_BASE_URL/tasks/$SP_TASK_ID/run" -H "Authorization: Bearer $TOKEN")

FAIL_SCRIPT_RESPONSE=$(curl -s -X POST "$API_BASE_URL/scripts" -H "Authorization: Bearer $TOKEN" -F "file=@$ROOT_DIR/qa/assets/failing-demo.sql")
FAIL_SCRIPT_ID=$(printf '%s' "$FAIL_SCRIPT_RESPONSE" | jq -r '.id')
FAIL_TASK_RESPONSE=$(auth_post_json /tasks "{
  \"name\": \"QA Failing SQL Task\",
  \"connectionId\": \"$CONNECTION_ID\",
  \"engine\": 0,
  \"sourceKind\": 0,
  \"sqlScriptId\": \"$FAIL_SCRIPT_ID\",
  \"parameters\": [],
  \"automatic\": false,
  \"enabled\": true,
  \"schedules\": [],
  \"retryPolicy\": { \"maxRetries\": 0, \"delayMinutes\": 1 },
  \"timeoutSeconds\": 300
}")
FAIL_TASK_ID=$(printf '%s' "$FAIL_TASK_RESPONSE" | jq -r '.id')
FAIL_RUN_RESPONSE=$(curl -s -X POST "$API_BASE_URL/tasks/$FAIL_TASK_ID/run" -H "Authorization: Bearer $TOKEN")

DASHBOARD_RESPONSE=$(auth_get /dashboard)
EXECUTIONS_RESPONSE=$(auth_get /executions)
MYSQL_TABLE=$(docker exec "$TARGET_CONTAINER" mysql -u"$TARGET_USER" -p"$TARGET_PASSWORD" -D "$TARGET_DB" -e "SELECT id, note, DATE_FORMAT(created_at, '%Y-%m-%d %H:%i:%s') AS created_at FROM scheduled_demo_runs ORDER BY id" --table)

REPORT_FILE="$RESULTS_DIR/end-to-end-demo-report.json"
{
  printf 'TARGET_CONTAINER=%q\n' "$TARGET_CONTAINER"
  printf 'TARGET_PORT=%q\n' "$TARGET_PORT"
  printf 'TARGET_DB=%q\n' "$TARGET_DB"
  printf 'TARGET_USER=%q\n' "$TARGET_USER"
  printf 'TARGET_PASSWORD=%q\n' "$TARGET_PASSWORD"
  printf 'CONNECTION_NAME=%q\n' "$CONNECTION_NAME"
} > "$STATE_FILE"

jq -n \
  --argjson connection "$CONNECTION_RESPONSE" \
  --argjson connectionTest "$CONNECTION_TEST" \
  --argjson successScript "$SUCCESS_SCRIPT_RESPONSE" \
  --argjson successTask "$SUCCESS_TASK_RESPONSE" \
  --argjson successRun "$SUCCESS_RUN_RESPONSE" \
  --argjson spTask "$SP_TASK_RESPONSE" \
  --argjson spRun "$SP_RUN_RESPONSE" \
  --argjson failTask "$FAIL_TASK_RESPONSE" \
  --argjson failRun "$FAIL_RUN_RESPONSE" \
  --argjson dashboard "$DASHBOARD_RESPONSE" \
  --argjson executions "$EXECUTIONS_RESPONSE" \
  --arg mysqlTable "$MYSQL_TABLE" \
  '{connection: $connection, connectionTest: $connectionTest, successScript: $successScript, successTask: $successTask, successRun: $successRun, storedProcedureTask: $spTask, storedProcedureRun: $spRun, failingTask: $failTask, failingRun: $failRun, dashboard: $dashboard, executions: $executions, mysqlTable: $mysqlTable}' \
  > "$REPORT_FILE"

echo "Demo QA completada. Reporte: $REPORT_FILE"