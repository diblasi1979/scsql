# Activación comercial de SCSQL

Este documento baja a tierra cómo pasar de la implementación técnica a una operación comercial real usando licencias firmadas.

## Qué resuelve este esquema

- Licencia perpetua: vendés una vez y no vence.
- Licencia mensual: emitís un token con fecha de vencimiento y renovás por período.
- Protección razonable: el producto valida con clave pública; la privada queda solo del lado del emisor.
- Bloqueo real: si la licencia requerida no es válida, no deja loguear ni ejecutar tareas automáticas/manuales.

## Archivos involucrados

- Backend: `backend/src/ScSql.Api/Licensing.cs`
- Configuración base: `backend/src/ScSql.Api/appsettings.json`
- Despliegue Docker: `docker-compose.yml`
- Script de emisión: `qa/generate-license-token.sh`
- Plantilla de variables: `licensing.env.example`

## Paso 1. Generar las claves RSA

Hacelo fuera del servidor del cliente y fuera del contenedor de la aplicación.

```bash
mkdir -p ~/scsql-license-issuer
cd ~/scsql-license-issuer
openssl genrsa -out private.pem 2048
openssl rsa -in private.pem -pubout -out public.pem
```

Resultado:

- `private.pem`: queda solo en tu entorno de emisión.
- `public.pem`: se copia al despliegue del cliente.

## Paso 2. Definir el identificador de instancia

Si querés que la licencia quede atada a un cliente o instalación concreta, definí un `instanceId` estable, por ejemplo:

- `cliente-sa-prod-01`
- `empresa-x-sucursal-central`
- `acme-argentina-main`

Ese mismo valor debe existir en:

- la licencia emitida
- `Licensing__InstanceId` en el despliegue

## Paso 3. Emitir una licencia

### Perpetua

```bash
cd /Users/pdiblasi/Desarrollos/scsql
SCSQL_LICENSE_PRIVATE_KEY=~/scsql-license-issuer/private.pem ./qa/generate-license-token.sh \
  --customer "Cliente SA" \
  --plan perpetual \
  --issued-at 2026-04-21T00:00:00Z \
  --instance-id cliente-sa-prod-01
```

### Mensual

```bash
cd /Users/pdiblasi/Desarrollos/scsql
SCSQL_LICENSE_PRIVATE_KEY=~/scsql-license-issuer/private.pem ./qa/generate-license-token.sh \
  --customer "Cliente SA" \
  --plan subscription \
  --issued-at 2026-04-21T00:00:00Z \
  --expires-at 2026-05-21T00:00:00Z \
  --instance-id cliente-sa-prod-01
```

El resultado es un token largo con formato:

```text
base64url(payload).base64url(signature)
```

Guardalo como secreto comercial. No hace falta que el cliente vea el contenido interno.

## Paso 4. Preparar el despliegue Docker

Copiá la plantilla y completala:

```bash
cd /Users/pdiblasi/Desarrollos/scsql
cp licensing.env.example licensing.env
```

Editar `licensing.env` con:

- `LICENSING_REQUIRE_VALID_LICENSE=true`
- `LICENSING_PRODUCT_CODE=scsql`
- `LICENSING_INSTANCE_ID=cliente-sa-prod-01`
- `LICENSING_CURRENT_LICENSE_KEY=<token emitido>`
- `LICENSING_PUBLIC_KEY_PEM=<contenido de public.pem>`

## Paso 5. Cargar la configuración en docker compose

Hay dos formas prácticas.

### Opción A. Variables exportadas en shell

```bash
export LICENSING_REQUIRE_VALID_LICENSE=true
export LICENSING_PRODUCT_CODE=scsql
export LICENSING_INSTANCE_ID=cliente-sa-prod-01
export LICENSING_CURRENT_LICENSE_KEY='PEGAR_TOKEN_AQUI'
export LICENSING_PUBLIC_KEY_PEM="$(cat ~/scsql-license-issuer/public.pem)"
docker compose up -d --build
```

### Opción B. Archivo `licensing.env`

Si usás un archivo, cargalo antes de levantar:

```bash
set -a
source ./licensing.env
set +a
docker compose up -d --build
```

## Paso 6. Ajustar `docker-compose.yml`

El compose actual ya está preparado para recibir estas variables. El servicio `api` usa:

- `Licensing__RequireValidLicense`
- `Licensing__ProductCode`
- `Licensing__PublicKeyPem`
- `Licensing__CurrentLicenseKey`
- `Licensing__InstanceId`

En producción conviene dejar valores reales vía variables de entorno y no hardcodearlos en el YAML.

## Paso 7. Verificar el estado de la licencia

Con el stack levantado:

```bash
curl http://localhost:2008/api/license/status
```

Respuesta esperada cuando está activa:

```json
{
  "state": "active",
  "enforcementEnabled": true,
  "isValid": true,
  "shouldBlock": false,
  "message": "Licencia por suscripción válida.",
  "productCode": "scsql",
  "customerName": "Cliente SA",
  "instanceId": "cliente-sa-prod-01",
  "plan": "subscription",
  "planLabel": "Mensual"
}
```

Si algo falla, los estados más comunes son:

- `missing`: falta token o clave pública.
- `invalid`: firma incorrecta, producto incorrecto o `instanceId` distinto.
- `expired`: venció la licencia.
- `notYetActive`: la licencia todavía no entra en vigencia.

## Paso 8. Renovación mensual

Para una venta mensual no cambiás la clave pública. Solo emitís un token nuevo con nueva fecha de vencimiento y reemplazás:

- `LICENSING_CURRENT_LICENSE_KEY`

Luego reiniciás el `api`:

```bash
docker compose up -d --force-recreate api
```

## Paso 9. Recomendación comercial

Modelo sugerido:

- Perpetua: `plan=perpetual`, sin `expiresAtUtc`.
- Mensual: `plan=subscription`, con vencimiento a 30 días.
- Anual: mismo `plan=subscription`, con vencimiento a 365 días.

La diferencia comercial no la define el código sino los datos de la licencia emitida.

## Paso 10. Buenas prácticas

- Nunca distribuyas `private.pem`.
- No la subas al repo.
- Guardala en un equipo o vault solo de emisión.
- Emití una licencia distinta por cliente.
- Si un cliente cambia de servidor y usás `instanceId`, reemití la licencia.

## Paso 11. Qué pasa si un cliente intenta alterar el token

No debería poder generar una firma válida sin la clave privada. Puede editar el string, pero el backend lo marcará como `invalid` y bloqueará la operación si el enforcement está activo.

## Paso 12. Límite realista

Ningún esquema local es imposible de crackear si alguien modifica binarios y tiene tiempo suficiente. Este enfoque sube mucho la vara técnica y es razonable para comercialización B2B. Si querés endurecerlo más, el siguiente nivel es validación contra un servidor de licencias tuyo.