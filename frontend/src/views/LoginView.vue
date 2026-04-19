<template>
  <section class="login-view">
    <div class="login-card">
      <p class="eyebrow">Panel de administración</p>
      <h2>Entrá y poné a correr tus jobs</h2>
      <p class="login-copy">Controlá scripts, stored procedures y ejecución automática desde un panel más ágil y visual.</p>
      <form class="form-grid" @submit.prevent="submit">
        <label>
          Usuario
          <input v-model="username" type="text" placeholder="admin" />
        </label>
        <label>
          Contraseña
          <input v-model="password" type="password" placeholder="********" />
        </label>
        <button :disabled="loading" type="submit">{{ loading ? 'Ingresando...' : 'Entrar' }}</button>
      </form>
      <p v-if="errorMessage" class="error-text">{{ errorMessage }}</p>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const username = ref('admin')
const password = ref('admin123')
const loading = ref(false)
const errorMessage = ref('')
const auth = useAuthStore()
const router = useRouter()

async function submit() {
  try {
    loading.value = true
    errorMessage.value = ''
    await auth.login(username.value, password.value)
    await router.push('/')
  } catch {
    errorMessage.value = 'Credenciales inválidas o API no disponible.'
  } finally {
    loading.value = false
  }
}
</script>