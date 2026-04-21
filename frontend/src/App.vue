<template>
  <div class="app-shell">
    <aside v-if="auth.isAuthenticated" class="sidebar">
      <div class="sidebar-top">
        <div class="brand-lockup">
          <p class="eyebrow">SQL crew control</p>
          <h1>SCSQL</h1>
          <p class="sidebar-tagline">Tareas, scripts y ejecuciones en un panel con ritmo propio.</p>
        </div>
      </div>
      <nav>
        <RouterLink to="/">Dashboard</RouterLink>
        <RouterLink to="/connections">Conexiones</RouterLink>
        <RouterLink to="/scripts">Scripts</RouterLink>
        <RouterLink to="/tasks">Tareas</RouterLink>
        <RouterLink to="/executions">Historial</RouterLink>
      </nav>
      <div class="sidebar-footer">
        <div v-if="license.status" class="license-badge" :class="`license-badge--${license.status.state}`">
          <p class="license-badge__label">Licencia {{ license.status.planLabel }}</p>
          <strong>{{ license.status.customerName || 'Sin asignar' }}</strong>
          <span>{{ licenseDetail }}</span>
        </div>
        <p class="sidebar-note">Modo admin activo</p>
        <button class="ghost-button icon-button icon-button--wide" type="button" data-tooltip="Cerrar sesión" aria-label="Cerrar sesión" @click="logout">
          <AppIcon class="icon-svg" name="logout" />
          <span class="icon-label">Salir</span>
        </button>
      </div>
    </aside>
    <main :class="auth.isAuthenticated ? 'content content--decorated' : 'content content--authless'">
      <RouterView />
    </main>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { RouterLink, RouterView } from 'vue-router'
import AppIcon from '@/components/AppIcon.vue'
import { useAuthStore } from '@/stores/auth'
import { useLicenseStore } from '@/stores/license'

const auth = useAuthStore()
const license = useLicenseStore()

const licenseDetail = computed(() => {
  if (!license.status) {
    return ''
  }

  if (license.status.expiresAtUtc) {
    return `Vence ${new Date(license.status.expiresAtUtc).toLocaleDateString('es-AR')}`
  }

  return license.status.message
})

onMounted(async () => {
  if (!license.status && !license.loading) {
    await license.refreshStatus()
  }
})

async function logout() {
  await auth.logout()
}
</script>