#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Uso:
  SCSQL_LICENSE_PRIVATE_KEY=/ruta/private.pem qa/generate-license-token.sh \
    --customer "Cliente SA" \
    --plan perpetual|subscription \
    --issued-at 2026-04-21T00:00:00Z \
    [--expires-at 2026-05-21T00:00:00Z] \
    [--instance-id cliente-prod-01] \
    [--product-code scsql]

Genera un token firmado con formato:
  base64url(payloadJson).base64url(signature)

La clave privada nunca debe distribuirse dentro del producto ni del repositorio entregado al cliente.
EOF
}

if [[ -z "${SCSQL_LICENSE_PRIVATE_KEY:-}" ]]; then
  echo "Debe definir SCSQL_LICENSE_PRIVATE_KEY con la ruta a la clave privada RSA." >&2
  usage
  exit 1
fi

customer=""
plan=""
issued_at=""
expires_at=""
instance_id=""
product_code="scsql"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --customer)
      customer="$2"
      shift 2
      ;;
    --plan)
      plan="$2"
      shift 2
      ;;
    --issued-at)
      issued_at="$2"
      shift 2
      ;;
    --expires-at)
      expires_at="$2"
      shift 2
      ;;
    --instance-id)
      instance_id="$2"
      shift 2
      ;;
    --product-code)
      product_code="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Argumento no reconocido: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ -z "$customer" || -z "$plan" || -z "$issued_at" ]]; then
  echo "Faltan argumentos obligatorios." >&2
  usage
  exit 1
fi

if [[ "$plan" != "perpetual" && "$plan" != "subscription" ]]; then
  echo "El plan debe ser perpetual o subscription." >&2
  exit 1
fi

if [[ "$plan" == "subscription" && -z "$expires_at" ]]; then
  echo "Las licencias subscription requieren --expires-at." >&2
  exit 1
fi

tmp_payload="$(mktemp)"
tmp_signature="$(mktemp)"
cleanup() {
  rm -f "$tmp_payload" "$tmp_signature"
}
trap cleanup EXIT

python3 - <<'PY' "$tmp_payload" "$product_code" "$customer" "$plan" "$issued_at" "$expires_at" "$instance_id"
import json
import sys

output_path, product_code, customer, plan, issued_at, expires_at, instance_id = sys.argv[1:8]
payload = {
    "productCode": product_code,
    "customerName": customer,
    "plan": plan,
    "issuedAtUtc": issued_at,
}
if expires_at:
    payload["expiresAtUtc"] = expires_at
if instance_id:
    payload["instanceId"] = instance_id

with open(output_path, "w", encoding="utf-8") as handle:
    json.dump(payload, handle, separators=(",", ":"))
PY

payload_b64="$(openssl base64 -A -in "$tmp_payload" | tr '+/' '-_' | tr -d '=')"
openssl dgst -sha256 -sign "$SCSQL_LICENSE_PRIVATE_KEY" -out "$tmp_signature" "$tmp_payload"
signature_b64="$(openssl base64 -A -in "$tmp_signature" | tr '+/' '-_' | tr -d '=')"

printf '%s.%s\n' "$payload_b64" "$signature_b64"