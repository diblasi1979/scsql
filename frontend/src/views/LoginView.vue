<template>
  <section class="login-view">
    <div class="login-card">
      <p class="eyebrow">Panel de administración</p>
      <h2>Entrá y poné a correr tus jobs</h2>
      <p class="login-copy">Controlá scripts, stored procedures y ejecución automática desde un panel más ágil y visual.</p>
      <div v-if="license.status" class="license-panel" :class="`license-panel--${license.status.state}`">
        <p class="license-panel__eyebrow">Licenciamiento</p>
        <strong>{{ license.status.customerName || 'Instancia sin asignar' }}</strong>
        <p>{{ license.status.message }}</p>
        <p v-if="license.status.expiresAtUtc" class="license-panel__meta">
          Vence el {{ new Date(license.status.expiresAtUtc).toLocaleString('es-AR') }}
        </p>
      </div>
      <form class="form-grid" @submit.prevent="submit">
        <label>
          Usuario
          <input v-model="username" type="text" placeholder="admin" />
        </label>
        <label>
          Contraseña
          <input v-model="password" type="password" placeholder="********" />
        </label>
        <button :disabled="loading || license.hasBlockingIssue" type="submit">{{ loading ? 'Ingresando...' : 'Entrar' }}</button>
      </form>
      <p v-if="errorMessage" class="error-text">{{ errorMessage }}</p>
      <p v-if="license.errorMessage" class="error-text">{{ license.errorMessage }}</p>
    </div>
  </section>
</template>

<script setup lang="ts">
import axios from 'axios'
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useLicenseStore } from '@/stores/license'

const username = ref('admin')
const password = ref('admin123')
const loading = ref(false)
const errorMessage = ref('')
const auth = useAuthStore()
const license = useLicenseStore()
const router = useRouter()

onMounted(async () => {
  if (!license.status && !license.loading) {
    await license.refreshStatus()
  }
})

async function submit() {
  if (license.hasBlockingIssue) {
    errorMessage.value = license.status?.message ?? 'La licencia actual no permite iniciar sesión.'
    return
  }

  try {
    loading.value = true
    errorMessage.value = ''
    await auth.login(username.value, password.value)
    await router.push('/')
  } catch (error) {
    if (axios.isAxiosError(error)) {
      errorMessage.value = error.response?.data?.message ?? 'Credenciales inválidas o API no disponible.'
    } else {
      errorMessage.value = 'Credenciales inválidas o API no disponible.'
    }
  } finally {
    loading.value = false
  }
}
</script>