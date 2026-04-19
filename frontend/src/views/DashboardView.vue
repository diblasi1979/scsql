<template>
  <section class="view-stack">
    <header class="page-header">
      <div>
        <p class="eyebrow">Resumen operativo</p>
        <h2>Dashboard principal</h2>
        <p class="section-copy">Una vista rápida del pulso del sistema para operar sin navegar de más.</p>
      </div>
      <div class="page-actions">
        <button class="ghost-button" @click="logout">Cerrar sesión</button>
        <button @click="loadData">Actualizar</button>
      </div>
    </header>

    <section class="hero-panel hero-panel--dashboard">
      <div class="hero-copy">
        <p class="eyebrow">Control room</p>
        <h3>Todo el stack en una sola pantalla</h3>
        <p>
          Seguimiento de tareas, errores recientes y próximas ejecuciones con una lectura más social,
          más directa y con mejor contraste visual.
        </p>
        <div class="hero-tags">
          <span class="hero-tag">Jobs live</span>
          <span class="hero-tag">Retry ready</span>
          <span class="hero-tag">Error trace</span>
        </div>
      </div>
      <div class="hero-sidekick">
        <div class="mini-stat-strip">
          <span>Activas</span>
          <strong>{{ dashboard?.activeTasks ?? 0 }}</strong>
        </div>
        <div class="mini-stat-strip mini-stat-strip--alt">
          <span>Recent fails</span>
          <strong>{{ dashboard?.failedExecutionsLast7Days ?? 0 }}</strong>
        </div>
      </div>
    </section>

    <div class="stat-grid">
      <StatCard label="Tareas activas" :value="dashboard?.activeTasks ?? 0" detail="Tareas habilitadas en el scheduler" />
      <StatCard label="Conexiones" :value="dashboard?.totalConnections ?? 0" detail="Destinos registrados" />
      <StatCard label="Fallos 7 días" :value="dashboard?.failedExecutionsLast7Days ?? 0" detail="Ejecuciones con error reciente" />
    </div>

    <div class="two-column-grid">
      <section class="panel-card panel-card--dense">
        <div class="panel-heading">
          <div>
            <p class="panel-kicker">Queue</p>
            <h3>Próximas tareas</h3>
          </div>
          <span class="count-pill">{{ dashboard?.upcomingTasks?.length ?? 0 }}</span>
        </div>
        <table class="data-table">
          <thead>
            <tr>
              <th>Nombre</th>
              <th>Modo</th>
              <th>Motor</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="task in dashboard?.upcomingTasks ?? []" :key="task.id">
              <td>
                <div class="data-lead">
                  <span class="cell-icon">JOB</span>
                  <div>
                    <strong>{{ task.name }}</strong>
                    <small>{{ task.sourceKind === 'sqlFile' ? 'Archivo SQL' : 'Stored procedure' }}</small>
                  </div>
                </div>
              </td>
              <td><span class="badge badge--soft">{{ task.automatic ? 'Automática' : 'Manual' }}</span></td>
              <td>{{ normalizeEngine(task.engine) }}</td>
            </tr>
          </tbody>
        </table>
      </section>

      <section class="panel-card panel-card--dense">
        <div class="panel-heading">
          <div>
            <p class="panel-kicker">Feed</p>
            <h3>Ejecuciones recientes</h3>
          </div>
          <span class="count-pill count-pill--warm">{{ dashboard?.recentExecutions?.length ?? 0 }}</span>
        </div>
        <table class="data-table">
          <thead>
            <tr>
              <th>Tarea</th>
              <th>Estado</th>
              <th>Inicio</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="execution in dashboard?.recentExecutions ?? []" :key="execution.id">
              <td>
                <div class="data-lead">
                  <span class="cell-icon cell-icon--alt">RUN</span>
                  <div>
                    <strong>{{ execution.taskName }}</strong>
                    <small>{{ execution.manualTrigger ? 'Lanzada manualmente' : 'Disparo automático' }}</small>
                  </div>
                </div>
              </td>
              <td><span :class="['badge', execution.status]">{{ execution.status }}</span></td>
              <td>{{ formatDate(execution.startedAtUtc) }}</td>
            </tr>
          </tbody>
        </table>
      </section>
    </div>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api, type Dashboard } from '@/api'
import StatCard from '@/components/StatCard.vue'
import { useAuthStore } from '@/stores/auth'

const dashboard = ref<Dashboard | null>(null)
const auth = useAuthStore()

function normalizeEngine(engine: string) {
  return engine === 'mySql' ? 'MySQL' : 'SQL Server'
}

function formatDate(value: string) {
  return new Date(value).toLocaleString('es-AR')
}

async function loadData() {
  const response = await api.get<Dashboard>('/dashboard')
  dashboard.value = response.data
}

async function logout() {
  await auth.logout()
}

onMounted(loadData)
</script>