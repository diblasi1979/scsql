<template>
  <section class="view-stack">
    <header class="page-header">
      <div>
        <p class="eyebrow">Issuer studio</p>
        <h2>Emisor administrativo de licencias</h2>
        <p class="section-copy">
          Generá tokens firmados localmente en el navegador. La clave privada no se envía al backend ni se guarda.
        </p>
      </div>
    </header>

    <section class="hero-panel hero-panel--dashboard">
      <div class="hero-copy">
        <p class="eyebrow">Offline signing</p>
        <h3>Perpetua o mensual desde una pantalla única</h3>
        <p>
          Pegá una clave privada RSA PKCS#8, definí cliente, plan e instancia y obtené un token listo para pegar en producción.
        </p>
        <div class="hero-tags">
          <span class="hero-tag">RSA SHA-256</span>
          <span class="hero-tag">PKCS#8</span>
          <span class="hero-tag">Sin backend</span>
        </div>
      </div>
      <div class="hero-sidekick hero-sidekick--stacked">
        <div class="mini-stat-strip">
          <span>Plan actual</span>
          <strong>{{ form.plan === 'perpetual' ? 'Perpetua' : 'Mensual' }}</strong>
        </div>
        <div class="mini-stat-strip mini-stat-strip--alt">
          <span>Instance lock</span>
          <strong>{{ form.instanceId ? 'ON' : 'OFF' }}</strong>
        </div>
      </div>
    </section>

    <div class="two-column-grid license-studio-grid">
      <section class="panel-card panel-card--dense">
        <div class="panel-heading">
          <div>
            <p class="panel-kicker">Builder</p>
            <h3>Datos de la licencia</h3>
          </div>
          <button class="ghost-button button--compact" type="button" @click="resetForm">Limpiar</button>
        </div>

        <form class="form-grid form-grid--two form-grid--styled" @submit.prevent="generateToken">
          <label>
            <span class="field-head"><span class="field-icon">CUST</span>Cliente</span>
            <input v-model="form.customerName" type="text" placeholder="Cliente SA" />
          </label>
          <label>
            <span class="field-head"><span class="field-icon">SKU</span>Producto</span>
            <input v-model="form.productCode" type="text" placeholder="scsql" />
          </label>
          <label>
            <span class="field-head"><span class="field-icon">PLAN</span>Plan</span>
            <select v-model="form.plan">
              <option value="perpetual">Perpetua</option>
              <option value="subscription">Mensual / suscripción</option>
            </select>
          </label>
          <label>
            <span class="field-head"><span class="field-icon">NODE</span>Instance ID</span>
            <input v-model="form.instanceId" type="text" placeholder="cliente-sa-prod-01" />
          </label>
          <label>
            <span class="field-head"><span class="field-icon">DATE</span>Emitida en</span>
            <input v-model="form.issuedAtUtc" type="datetime-local" />
          </label>
          <label>
            <span class="field-head"><span class="field-icon">GO</span>Activa desde</span>
            <input v-model="form.notBeforeUtc" type="datetime-local" />
          </label>
          <label v-if="form.plan === 'subscription'">
            <span class="field-head"><span class="field-icon">EXP</span>Vence en</span>
            <input v-model="form.expiresAtUtc" type="datetime-local" />
          </label>
          <label class="license-studio__file-field">
            <span class="field-head"><span class="field-icon">PEM</span>Archivo private key</span>
            <input accept=".pem,.key,.txt" type="file" @change="loadKeyFromFile" />
          </label>
          <label class="modal-span-full license-studio__full-span">
            <span class="field-head"><span class="field-icon">KEY</span>Clave privada PKCS#8</span>
            <textarea v-model="form.privateKeyPem" rows="10" placeholder="-----BEGIN PRIVATE KEY-----"></textarea>
          </label>
          <div class="modal-actions modal-span-full license-studio__actions">
            <button class="button--compact" :disabled="generating" type="submit">{{ generating ? 'Firmando...' : 'Generar token' }}</button>
          </div>
        </form>
      </section>

      <section class="panel-card panel-card--dense">
        <div class="panel-heading">
          <div>
            <p class="panel-kicker">Output</p>
            <h3>Token y payload</h3>
          </div>
          <button class="ghost-button button--compact" :disabled="!token" type="button" @click="copyToken">Copiar token</button>
        </div>

        <div class="license-output-card">
          <p class="section-copy section-copy--compact">
            Usá una private key PKCS#8 sin passphrase. Si hoy generás `private.pem` con `openssl genrsa`, convertí antes a PKCS#8.
          </p>
          <pre class="license-output-block">{{ token || 'Todavía no hay token generado.' }}</pre>
        </div>

        <div class="license-output-card">
          <div class="panel-heading panel-heading--mini">
            <div>
              <p class="panel-kicker">Preview</p>
              <h3>Payload firmado</h3>
            </div>
          </div>
          <pre class="license-output-block">{{ payloadPreview }}</pre>
        </div>

        <div class="license-output-card license-output-card--warning">
          <p class="section-copy section-copy--compact">
            La clave privada nunca se persiste. Si recargás la página, se pierde. El token generado sí puede copiarse a `.env.production`.
          </p>
        </div>
      </section>
    </div>

    <AppToast :open="toast.open" :message="toast.message" :title="toast.title" :variant="toast.variant" @close="closeToast" />
  </section>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import AppToast from '@/components/AppToast.vue'

type LicenseForm = {
  customerName: string
  productCode: string
  plan: 'perpetual' | 'subscription'
  issuedAtUtc: string
  notBeforeUtc: string
  expiresAtUtc: string
  instanceId: string
  privateKeyPem: string
}

const generating = ref(false)
const token = ref('')
const payloadJson = ref('')
const toast = reactive({
  open: false,
  title: 'Listo',
  message: '',
  variant: 'success' as 'success' | 'info' | 'warning' | 'error',
})

const form = reactive(createInitialForm())

const payloadPreview = computed(() => payloadJson.value || '{\n  "productCode": "scsql"\n}')

function createInitialForm(): LicenseForm {
  const now = new Date()
  const inThirtyDays = new Date(now.getTime() + 30 * 24 * 60 * 60 * 1000)

  return {
    customerName: '',
    productCode: 'scsql',
    plan: 'subscription',
    issuedAtUtc: toDateTimeLocalValue(now),
    notBeforeUtc: toDateTimeLocalValue(now),
    expiresAtUtc: toDateTimeLocalValue(inThirtyDays),
    instanceId: '',
    privateKeyPem: '',
  }
}

function resetForm() {
  Object.assign(form, createInitialForm())
  token.value = ''
  payloadJson.value = ''
}

async function loadKeyFromFile(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) {
    return
  }

  form.privateKeyPem = await file.text()
  showToast('Clave privada cargada en memoria del navegador.', 'success', 'Clave cargada')
}

async function generateToken() {
  try {
    generating.value = true
    const payload = buildPayload()
    const privateKey = await importPrivateKey(form.privateKeyPem)
    const payloadBytes = new TextEncoder().encode(JSON.stringify(payload))
    const signature = await crypto.subtle.sign('RSASSA-PKCS1-v1_5', privateKey, payloadBytes)

    payloadJson.value = JSON.stringify(payload, null, 2)
    token.value = `${toBase64Url(payloadBytes)}.${toBase64Url(new Uint8Array(signature))}`
    showToast('Token generado y listo para copiar a producción.', 'success', 'Licencia emitida')
  } catch (error) {
    showToast(resolveErrorMessage(error), 'error', 'No se pudo firmar')
  } finally {
    generating.value = false
  }
}

async function copyToken() {
  if (!token.value) {
    return
  }

  await navigator.clipboard.writeText(token.value)
  showToast('Token copiado al portapapeles.', 'info', 'Copiado')
}

function buildPayload() {
  const customerName = form.customerName.trim()
  const productCode = form.productCode.trim()
  const privateKeyPem = form.privateKeyPem.trim()
  const issuedAtUtc = toIsoString(form.issuedAtUtc)

  if (!customerName) {
    throw new Error('Debés indicar el cliente licenciado.')
  }

  if (!productCode) {
    throw new Error('Debés indicar el código de producto.')
  }

  if (!privateKeyPem) {
    throw new Error('Debés pegar una clave privada PKCS#8.')
  }

  if (!issuedAtUtc) {
    throw new Error('La fecha de emisión no es válida.')
  }

  const payload: Record<string, string> = {
    productCode,
    customerName,
    plan: form.plan,
    issuedAtUtc,
  }

  const notBeforeUtc = toIsoString(form.notBeforeUtc)
  if (notBeforeUtc) {
    payload.notBeforeUtc = notBeforeUtc
  }

  if (form.plan === 'subscription') {
    const expiresAtUtc = toIsoString(form.expiresAtUtc)
    if (!expiresAtUtc) {
      throw new Error('Las licencias mensuales requieren una fecha de vencimiento válida.')
    }

    payload.expiresAtUtc = expiresAtUtc
  }

  const instanceId = form.instanceId.trim()
  if (instanceId) {
    payload.instanceId = instanceId
  }

  return payload
}

async function importPrivateKey(pem: string) {
  const normalizedPem = pem.trim().replace(/\r/g, '')
  const content = normalizedPem
    .replace('-----BEGIN PRIVATE KEY-----', '')
    .replace('-----END PRIVATE KEY-----', '')
    .replace(/\s+/g, '')

  if (!normalizedPem.includes('BEGIN PRIVATE KEY')) {
    throw new Error('La clave debe estar en formato PKCS#8 PEM, con encabezado BEGIN PRIVATE KEY.')
  }

  const keyBytes = base64ToBytes(content)
  return crypto.subtle.importKey(
    'pkcs8',
    keyBytes,
    {
      name: 'RSASSA-PKCS1-v1_5',
      hash: 'SHA-256',
    },
    false,
    ['sign'],
  )
}

function base64ToBytes(value: string) {
  const binary = atob(value)
  const bytes = new Uint8Array(binary.length)
  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index)
  }

  return bytes
}

function toBase64Url(value: Uint8Array) {
  let binary = ''
  const chunkSize = 0x8000
  for (let index = 0; index < value.length; index += chunkSize) {
    binary += String.fromCharCode(...value.subarray(index, index + chunkSize))
  }

  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '')
}

function toDateTimeLocalValue(date: Date) {
  const year = date.getFullYear()
  const month = `${date.getMonth() + 1}`.padStart(2, '0')
  const day = `${date.getDate()}`.padStart(2, '0')
  const hours = `${date.getHours()}`.padStart(2, '0')
  const minutes = `${date.getMinutes()}`.padStart(2, '0')
  return `${year}-${month}-${day}T${hours}:${minutes}`
}

function toIsoString(value: string) {
  if (!value) {
    return ''
  }

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return ''
  }

  return date.toISOString()
}

function resolveErrorMessage(error: unknown) {
  if (error instanceof Error) {
    if (error.message.includes('importKey')) {
      return 'No se pudo importar la clave. Usá un PEM PKCS#8 sin passphrase.'
    }

    return error.message
  }

  return 'Ocurrió un error inesperado al firmar la licencia.'
}

function showToast(message: string, variant: 'success' | 'info' | 'warning' | 'error', title = 'Listo') {
  toast.message = message
  toast.variant = variant
  toast.title = title
  toast.open = true
}

function closeToast() {
  toast.open = false
}
</script>