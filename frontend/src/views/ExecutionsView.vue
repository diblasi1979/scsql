<template>
  <section class="view-stack view-stack--executions">
    <header class="page-header">
      <div>
        <p class="eyebrow">Trazabilidad</p>
        <h2>Historial de ejecuciones</h2>
        <p class="section-copy">Leé estados, duración e incidentes con una visualización más fuerte y una jerarquía de feed.</p>
      </div>
      <div class="page-actions">
        <button
          class="ghost-button button--compact"
          type="button"
          :disabled="deletableSelectedCount === 0 || deleting"
          @click="openDeleteDialog"
        >
          {{ deleting ? 'Eliminando...' : `Eliminar seleccionadas (${deletableSelectedCount})` }}
        </button>
        <button class="icon-button" type="button" data-tooltip="Actualizar historial" aria-label="Actualizar historial" @click="loadExecutions">
          <AppIcon class="icon-svg" name="refresh" />
        </button>
      </div>
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
      <div class="panel-heading panel-heading--mini">
        <div>
          <p class="panel-kicker">Filtros</p>
          <h3>Buscá por tarea, estado o rango horario</h3>
        </div>
        <span class="count-pill">{{ executions.length }} registros</span>
      </div>

      <form class="form-grid execution-filters" @submit.prevent="loadExecutions">
        <label>
          Tarea
          <input v-model="filters.taskName" type="text" placeholder="Nombre de tarea" />
        </label>

        <label>
          Estado
          <select v-model="filters.status">
            <option value="">Todos</option>
            <option v-for="status in statuses" :key="status" :value="status">{{ status }}</option>
          </select>
        </label>

        <label>
          Desde
          <input v-model="filters.startedFromLocal" type="datetime-local" />
        </label>

        <label>
          Hasta
          <input v-model="filters.startedToLocal" type="datetime-local" />
        </label>

        <div class="execution-filters__actions">
          <button :disabled="loading" type="submit">{{ loading ? 'Buscando...' : 'Aplicar filtros' }}</button>
          <button class="ghost-button button--compact" type="button" :disabled="loading" @click="resetFilters">Limpiar</button>
        </div>
      </form>
    </section>

    <section class="panel-card panel-card--dense">
      <div class="panel-heading">
        <div>
          <p class="panel-kicker">Timeline</p>
          <h3>Eventos recientes</h3>
        </div>
        <span class="count-pill count-pill--warm">{{ selectedCount }} seleccionadas</span>
      </div>
      <table class="data-table">
        <thead>
          <tr>
            <th class="execution-history__select">
              <input
                type="checkbox"
                :checked="allVisibleSelected"
                :disabled="selectableExecutionIds.length === 0"
                @change="toggleAllVisible($event)"
              />
            </th>
            <th>Tarea</th>
            <th>Estado</th>
            <th>Intentos</th>
            <th>Inicio</th>
            <th>Duración</th>
            <th>Resultado</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="execution in executions" :key="execution.id">
            <td class="execution-history__select">
              <input
                type="checkbox"
                :checked="selectedIds.has(execution.id)"
                :disabled="!isDeletable(execution)"
                @change="toggleSelection(execution.id, $event)"
              />
            </td>
            <td>
              <div class="data-lead">
                <span class="cell-icon cell-icon--alt">LOG</span>
                <div>
                  <strong>{{ execution.taskName }}</strong>
                  <small>
                    {{ execution.manualTrigger ? 'Manual' : 'Automática' }}
                    <template v-if="!isDeletable(execution)"> · protegida</template>
                  </small>
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
              <details v-else-if="execution.successSummary" class="details-card details-card--success">
                <summary>{{ execution.successSummary }}</summary>
                <pre>{{ execution.successDetail }}</pre>
              </details>
              <span v-else>-</span>
            </td>
          </tr>
          <tr v-if="executions.length === 0">
            <td colspan="7">
              <p class="section-copy section-copy--compact">No hay ejecuciones para los filtros aplicados.</p>
            </td>
          </tr>
        </tbody>
      </table>
    </section>

    <ConfirmDialog
      :open="deleteDialogOpen"
      title="Eliminar ejecuciones del historial"
      message="Se eliminarán definitivamente los eventos seleccionados del historial."
      :detail="`${deletableSelectedCount} ejecución(es) serán removidas. Las pendientes o en curso no se pueden borrar.`"
      confirm-label="Eliminar seleccionadas"
      @confirm="deleteSelectedExecutions"
      @cancel="deleteDialogOpen = false"
    />

    <AppToast
      :open="toast.open"
      :title="toast.title"
      :message="toast.message"
      :variant="toast.variant"
      @close="closeToast"
    />
  </section>
</template>

<script setup lang="ts">
import axios from 'axios'
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import { api, type ExecutionRecord } from '@/api'
import AppIcon from '@/components/AppIcon.vue'
import AppToast from '@/components/AppToast.vue'
import ConfirmDialog from '@/components/ConfirmDialog.vue'

const executions = ref<ExecutionRecord[]>([])
const loading = ref(false)
const deleting = ref(false)
const deleteDialogOpen = ref(false)
const selectedIds = ref(new Set<string>())
const statuses: ExecutionRecord['status'][] = ['pending', 'running', 'success', 'failed', 'retrying']
const filters = reactive({
  taskName: '',
  status: '',
  startedFromLocal: '',
  startedToLocal: '',
})
const toast = reactive({
  open: false,
  title: 'Listo',
  message: '',
  variant: 'success' as 'success' | 'info' | 'warning' | 'error',
})

let toastTimer: ReturnType<typeof setTimeout> | undefined

const failedCount = computed(() => executions.value.filter((entry) => entry.status === 'failed').length)
const selectedCount = computed(() => selectedIds.value.size)
const selectableExecutionIds = computed(() => executions.value.filter(isDeletable).map((execution) => execution.id))
const allVisibleSelected = computed(() => selectableExecutionIds.value.length > 0 && selectableExecutionIds.value.every((id) => selectedIds.value.has(id)))
const deletableSelectedCount = computed(() => executions.value.filter((execution) => selectedIds.value.has(execution.id) && isDeletable(execution)).length)

function formatDate(value: string) {
  return new Date(value).toLocaleString('es-AR')
}

function isDeletable(execution: ExecutionRecord) {
  return !['pending', 'running', 'retrying'].includes(execution.status)
}

function toUtcIso(value: string) {
  return value ? new Date(value).toISOString() : undefined
}

function showToast(message: string, variant: 'success' | 'info' | 'warning' | 'error' = 'success', title = 'Listo') {
  toast.message = message
  toast.variant = variant
  toast.title = title
  toast.open = true

  if (toastTimer) {
    clearTimeout(toastTimer)
  }

  toastTimer = setTimeout(() => {
    toast.open = false
  }, 3200)
}

function closeToast() {
  toast.open = false

  if (toastTimer) {
    clearTimeout(toastTimer)
    toastTimer = undefined
  }
}

function syncSelectedIds() {
  const visibleIds = new Set(executions.value.map((execution) => execution.id))
  selectedIds.value = new Set([...selectedIds.value].filter((id) => visibleIds.has(id)))
}

function toggleSelection(id: string, event: Event) {
  const input = event.target as HTMLInputElement
  const next = new Set(selectedIds.value)

  if (input.checked) {
    next.add(id)
  } else {
    next.delete(id)
  }

  selectedIds.value = next
}

function toggleAllVisible(event: Event) {
  const input = event.target as HTMLInputElement
  const next = new Set(selectedIds.value)

  if (input.checked) {
    for (const id of selectableExecutionIds.value) {
      next.add(id)
    }
  } else {
    for (const id of selectableExecutionIds.value) {
      next.delete(id)
    }
  }

  selectedIds.value = next
}

function openDeleteDialog() {
  if (deletableSelectedCount.value === 0) {
    return
  }

  deleteDialogOpen.value = true
}

async function loadExecutions() {
  loading.value = true

  try {
    const response = await api.get<ExecutionRecord[]>('/executions', {
      params: {
        taskName: filters.taskName || undefined,
        status: filters.status || undefined,
        startedFromUtc: toUtcIso(filters.startedFromLocal),
        startedToUtc: toUtcIso(filters.startedToLocal),
      },
    })

    executions.value = response.data
    syncSelectedIds()
  } finally {
    loading.value = false
  }
}

async function deleteSelectedExecutions() {
  const ids = executions.value.filter((execution) => selectedIds.value.has(execution.id) && isDeletable(execution)).map((execution) => execution.id)

  if (ids.length === 0) {
    deleteDialogOpen.value = false
    return
  }

  deleting.value = true

  try {
    await api.delete('/executions', { data: { ids } })
    deleteDialogOpen.value = false
    selectedIds.value = new Set([...selectedIds.value].filter((id) => !ids.includes(id)))
    showToast(`${ids.length} ejecución(es) eliminadas del historial.`)
    await loadExecutions()
  } catch (error) {
    if (axios.isAxiosError(error)) {
      showToast(error.response?.data?.message ?? 'No fue posible eliminar las ejecuciones seleccionadas.', 'error', 'Error al eliminar')
    } else {
      showToast('No fue posible eliminar las ejecuciones seleccionadas.', 'error', 'Error al eliminar')
    }
  } finally {
    deleting.value = false
  }
}

async function resetFilters() {
  filters.taskName = ''
  filters.status = ''
  filters.startedFromLocal = ''
  filters.startedToLocal = ''
  await loadExecutions()
}

onMounted(loadExecutions)
onUnmounted(closeToast)
</script>