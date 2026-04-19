<template>
  <section class="view-stack">
    <header class="page-header">
      <div>
        <p class="eyebrow">Trazabilidad</p>
        <h2>Historial de ejecuciones</h2>
        <p class="section-copy">Leé estados, duración e incidentes con una visualización más fuerte y una jerarquía de feed.</p>
      </div>
      <button @click="loadExecutions">Actualizar</button>
    </header>

    <section class="hero-panel hero-panel--executions">
      <div class="hero-copy">
        <p class="eyebrow">Trace feed</p>
        <h3>El timeline operativo de tus ejecuciones</h3>
      </div>
      <div class="hero-sidekick hero-sidekick--stacked">
        <div class="mini-stat-strip">
          <span>Total visible</span>
          <strong>{{ executions.length }}</strong>
        </div>
        <div class="mini-stat-strip mini-stat-strip--alt">
          <span>Errores</span>
          <strong>{{ failedCount }}</strong>
        </div>
      </div>
    </section>

    <section class="panel-card panel-card--dense">
      <div class="panel-heading">
        <div>
          <p class="panel-kicker">Timeline</p>
          <h3>Eventos recientes</h3>
        </div>
        <span class="count-pill count-pill--warm">{{ executions.length }}</span>
      </div>
      <table class="data-table">
        <thead>
          <tr>
            <th>Tarea</th>
            <th>Estado</th>
            <th>Intentos</th>
            <th>Inicio</th>
            <th>Duración</th>
            <th>Error</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="execution in executions" :key="execution.id">
            <td>
              <div class="data-lead">
                <span class="cell-icon cell-icon--alt">LOG</span>
                <div>
                  <strong>{{ execution.taskName }}</strong>
                  <small>{{ execution.manualTrigger ? 'Manual' : 'Automática' }}</small>
                </div>
              </div>
            </td>
            <td><span :class="['badge', execution.status]">{{ execution.status }}</span></td>
            <td>{{ execution.attempts }}</td>
            <td>{{ formatDate(execution.startedAtUtc) }}</td>
            <td>{{ execution.durationMs ? `${execution.durationMs} ms` : '-' }}</td>
            <td>
              <details v-if="execution.errorSummary" class="details-card">
                <summary>{{ execution.errorSummary }}</summary>
                <pre>{{ execution.errorDetail }}</pre>
              </details>
              <span v-else>-</span>
            </td>
          </tr>
        </tbody>
      </table>
    </section>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { api, type ExecutionRecord } from '@/api'

const executions = ref<ExecutionRecord[]>([])
const failedCount = computed(() => executions.value.filter((entry) => entry.status === 'failed').length)

function formatDate(value: string) {
  return new Date(value).toLocaleString('es-AR')
}

async function loadExecutions() {
  const response = await api.get<ExecutionRecord[]>('/executions')
  executions.value = response.data
}

onMounted(loadExecutions)
</script>