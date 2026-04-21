<template>
  <section class="login-view">
    <div class="login-stage">
      <article class="login-showcase">
        <p class="eyebrow">Novent orchestration</p>
        <h1>Automatizá la operación con una cabina más clara y más elegante.</h1>
        <p class="login-copy login-copy--hero">
          Centralizá conexiones, scripts, tareas programadas y monitoreo desde un panel pensado para operación diaria.
        </p>
        <div class="login-showcase__chips">
          <span class="hero-tag hero-tag--soft">Jobs programados</span>
          <span class="hero-tag hero-tag--soft">Licencias firmadas</span>
          <span class="hero-tag hero-tag--soft">Trazabilidad</span>
        </div>
        <div class="login-metrics">
          <div class="login-metric">
            <strong>24/7</strong>
            <span>Ejecución y seguimiento continuo</span>
          </div>
          <div class="login-metric">
            <strong>1 panel</strong>
            <span>Conexiones, scripts y tareas en un solo flujo</span>
          </div>
          <div class="login-metric">
            <strong>RSA</strong>
            <span>Control comercial con licencias firmadas</span>
          </div>
        </div>
      </article>

      <div class="login-card login-card--auth">
        <div class="login-card__header">
          <p class="eyebrow">Panel de administración</p>
          <h2>Entrá y poné a correr tus jobs</h2>
          <p class="login-copy">Accedé al panel operativo para gestionar automatizaciones, revisar ejecuciones y mantener el entorno bajo control.</p>
        </div>

        <div v-if="license.status" class="license-panel license-panel--login" :class="`license-panel--${license.status.state}`">
          <p class="license-panel__eyebrow">Licenciamiento</p>
          <strong>{{ license.status.customerName || 'Instancia sin asignar' }}</strong>
          <p>{{ license.status.message }}</p>
          <p v-if="license.status.expiresAtUtc" class="license-panel__meta">
            Vence el {{ new Date(license.status.expiresAtUtc).toLocaleString('es-AR') }}
          </p>
        </div>

        <form class="form-grid login-form" @submit.prevent="submit">
          <label class="login-field">
            <span class="login-field__label">Usuario</span>
            <input v-model="username" type="text" placeholder="admin" />
          </label>
          <label class="login-field">
            <span class="login-field__label">Contraseña</span>
            <input v-model="password" type="password" placeholder="********" />
          </label>
          <button :disabled="loading || license.hasBlockingIssue" type="submit">{{ loading ? 'Ingresando...' : 'Entrar al panel' }}</button>
        </form>

        <p v-if="errorMessage" class="error-text">{{ errorMessage }}</p>
        <p v-if="license.errorMessage" class="error-text">{{ license.errorMessage }}</p>

        <div class="login-card__footer">
          <p class="login-card__hint">Si necesitás emitir o renovar una licencia, usá el estudio administrativo.</p>
          <RouterLink class="secondary-link" to="/license-studio">Abrir emisor administrativo de licencias</RouterLink>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import axios from 'axios'
import { onMounted, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
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