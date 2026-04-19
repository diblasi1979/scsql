<template>
  <section class="view-stack">
    <header class="page-header">
      <div>
        <p class="eyebrow">Orquestación</p>
        <h2>Tareas programadas</h2>
        <p class="section-copy">Diseñá flujos manuales o automáticos con un builder más claro, más visual y más rápido de leer.</p>
      </div>
      <button class="icon-button" type="button" data-tooltip="Recargar tareas" aria-label="Recargar tareas" @click="loadAll">
        <AppIcon class="icon-svg" name="refresh" />
      </button>
    </header>

    <section class="hero-panel hero-panel--tasks">
      <div class="hero-copy">
        <p class="eyebrow">Automation board</p>
        <h3>Tu scheduler con lenguaje visual de startup</h3>
      </div>
      <div class="hero-sidekick hero-sidekick--stacked">
        <div class="mini-stat-strip">
          <span>Tareas</span>
          <strong>{{ tasks.length }}</strong>
        </div>
        <div class="mini-stat-strip mini-stat-strip--alt">
          <span>Conexiones listas</span>
          <strong>{{ connections.length }}</strong>
        </div>
      </div>
    </section>

    <section class="panel-card panel-card--dense">
      <div class="panel-heading">
        <div>
          <p class="panel-kicker">Builder</p>
          <h3>Nueva tarea</h3>
        </div>
        <span class="count-pill">RUN</span>
      </div>
      <form class="form-grid form-grid--three form-grid--styled" @submit.prevent="createTask">
        <label>
          <span class="field-head"><span class="field-icon">ID</span>Nombre</span>
          <input v-model="form.name" type="text" />
        </label>
        <label>
          <span class="field-head"><span class="field-icon">DB</span>Conexión</span>
          <select v-model="form.connectionId">
            <option disabled value="">Selecciona una conexión</option>
            <option v-for="connection in connections" :key="connection.id" :value="connection.id">
              {{ connection.name }}
            </option>
          </select>
        </label>
        <label>
          <span class="field-head"><span class="field-icon">ENG</span>Motor</span>
          <select v-model.number="form.engine">
            <option value="0">MySQL</option>
            <option value="1">SQL Server</option>
          </select>
        </label>
        <label>
          <span class="field-head"><span class="field-icon">SRC</span>Origen</span>
          <select v-model.number="form.sourceKind">
            <option value="0">Archivo SQL</option>
            <option value="1">Stored Procedure</option>
          </select>
        </label>
        <label v-if="form.sourceKind === 0">
          <span class="field-head"><span class="field-icon">SQL</span>Script</span>
          <select v-model="form.sqlScriptId">
            <option disabled value="">Selecciona un script</option>
            <option v-for="script in scripts" :key="script.id" :value="script.id">
              {{ script.originalName }}
            </option>
          </select>
        </label>
        <label v-else>
          <span class="field-head"><span class="field-icon">SP</span>Stored Procedure</span>
          <input v-model="form.storedProcedureName" type="text" placeholder="usp_Procesar" />
        </label>
        <label>
          <span class="field-head"><span class="field-icon">TIME</span>Timeout (seg)</span>
          <input v-model.number="form.timeoutSeconds" type="number" min="1" />
        </label>
        <label class="checkbox-field">
          <input v-model="form.automatic" type="checkbox" />
          Ejecutar automáticamente
        </label>
        <label>
          <span class="field-head"><span class="field-icon">RTR</span>Reintentos</span>
          <input v-model.number="form.retryPolicy.maxRetries" type="number" min="0" />
        </label>
        <label>
          <span class="field-head"><span class="field-icon">WAIT</span>Espera entre reintentos (min)</span>
          <input v-model.number="form.retryPolicy.delayMinutes" type="number" min="1" />
        </label>

        <div class="schedule-builder schedule-builder--card">
          <div class="panel-heading panel-heading--mini">
            <div>
              <p class="panel-kicker">Schedule</p>
              <h3>Ventanas de ejecución</h3>
            </div>
          </div>
          <div class="schedule-row">
            <select v-model.number="schedule.dayOfWeek">
              <option :value="1">Lunes</option>
              <option :value="2">Martes</option>
              <option :value="3">Miércoles</option>
              <option :value="4">Jueves</option>
              <option :value="5">Viernes</option>
              <option :value="6">Sábado</option>
              <option :value="0">Domingo</option>
            </select>
            <input v-model="schedule.time" type="time" />
            <button class="ghost-button icon-button icon-button--soft" type="button" data-tooltip="Agregar horario" aria-label="Agregar horario" @click="addSchedule">
              <AppIcon class="icon-svg" name="plus" />
            </button>
          </div>
          <ul class="chip-list">
            <li v-for="slot in form.schedules" :key="`${slot.dayOfWeek}-${slot.time}`">
              {{ dayLabels[slot.dayOfWeek] }} {{ slot.time }}
            </li>
          </ul>
        </div>

        <button class="button--compact" type="submit">Guardar tarea</button>
      </form>
    </section>

    <section class="panel-card panel-card--dense">
      <div class="panel-heading">
        <div>
          <p class="panel-kicker">Fleet</p>
          <h3>Listado</h3>
        </div>
        <span class="count-pill count-pill--warm">{{ tasks.length }}</span>
      </div>
      <table class="data-table">
        <thead>
          <tr>
            <th>Nombre</th>
            <th>Modo</th>
            <th>Origen</th>
            <th>Reintentos</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="task in tasks" :key="task.id">
            <td>
              <div class="data-lead">
                <span class="cell-icon">JOB</span>
                <div>
                  <strong>{{ task.name }}</strong>
                  <small>{{ task.engine === 'mySql' ? 'MySQL' : 'SQL Server' }}</small>
                </div>
              </div>
            </td>
            <td><span class="badge badge--soft">{{ task.automatic ? 'Automática' : 'Manual' }}</span></td>
            <td>{{ task.sourceKind === 'sqlFile' ? 'Archivo SQL' : 'Stored Procedure' }}</td>
            <td>{{ task.retryPolicy.maxRetries }}</td>
            <td>
              <div class="action-row">
                <button class="ghost-button icon-button" type="button" data-tooltip="Editar tarea" aria-label="Editar tarea" @click="openEditModal(task)">
                  <AppIcon class="icon-svg" name="edit" />
                </button>
                <button class="ghost-button icon-button" type="button" data-tooltip="Ejecutar tarea" aria-label="Ejecutar tarea" @click="runTask(task.id)">
                  <AppIcon class="icon-svg" name="play" />
                </button>
                <button class="ghost-button ghost-button--danger icon-button" type="button" data-tooltip="Eliminar tarea" aria-label="Eliminar tarea" @click="deleteTask(task)">
                  <AppIcon class="icon-svg" name="delete" />
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
      <p v-if="feedback" class="success-text">{{ feedback }}</p>
    </section>

    <div v-if="isEditModalOpen" class="modal-overlay" @click.self="closeEditModal">
      <section class="modal-card">
        <div class="panel-heading">
          <div>
            <p class="panel-kicker">Edit</p>
            <h3>Modificar tarea</h3>
          </div>
          <button class="ghost-button icon-button" type="button" data-tooltip="Cerrar modal" aria-label="Cerrar modal" @click="closeEditModal">
            <AppIcon class="icon-svg" name="close" />
          </button>
        </div>

        <form class="form-grid form-grid--three form-grid--styled" @submit.prevent="updateTask">
          <label>
            <span class="field-head"><span class="field-icon">ID</span>Nombre</span>
            <input v-model="editForm.name" type="text" />
          </label>
          <label>
            <span class="field-head"><span class="field-icon">DB</span>Conexión</span>
            <select v-model="editForm.connectionId">
              <option disabled value="">Selecciona una conexión</option>
              <option v-for="connection in connections" :key="connection.id" :value="connection.id">
                {{ connection.name }}
              </option>
            </select>
          </label>
          <label>
            <span class="field-head"><span class="field-icon">ENG</span>Motor</span>
            <select v-model.number="editForm.engine">
              <option value="0">MySQL</option>
              <option value="1">SQL Server</option>
            </select>
          </label>
          <label>
            <span class="field-head"><span class="field-icon">SRC</span>Origen</span>
            <select v-model.number="editForm.sourceKind">
              <option value="0">Archivo SQL</option>
              <option value="1">Stored Procedure</option>
            </select>
          </label>
          <label v-if="editForm.sourceKind === 0">
            <span class="field-head"><span class="field-icon">SQL</span>Script</span>
            <select v-model="editForm.sqlScriptId">
              <option disabled value="">Selecciona un script</option>
              <option v-for="script in scripts" :key="script.id" :value="script.id">
                {{ script.originalName }}
              </option>
            </select>
          </label>
          <label v-else>
            <span class="field-head"><span class="field-icon">SP</span>Stored Procedure</span>
            <input v-model="editForm.storedProcedureName" type="text" placeholder="usp_Procesar" />
          </label>
          <label>
            <span class="field-head"><span class="field-icon">TIME</span>Timeout (seg)</span>
            <input v-model.number="editForm.timeoutSeconds" type="number" min="1" />
          </label>
          <label class="checkbox-field">
            <input v-model="editForm.automatic" type="checkbox" />
            Ejecutar automáticamente
          </label>
          <label class="checkbox-field">
            <input v-model="editForm.enabled" type="checkbox" />
            Tarea habilitada
          </label>
          <label>
            <span class="field-head"><span class="field-icon">RTR</span>Reintentos</span>
            <input v-model.number="editForm.retryPolicy.maxRetries" type="number" min="0" />
          </label>
          <label>
            <span class="field-head"><span class="field-icon">WAIT</span>Espera entre reintentos (min)</span>
            <input v-model.number="editForm.retryPolicy.delayMinutes" type="number" min="1" />
          </label>

          <div class="schedule-builder schedule-builder--card modal-span-full">
            <div class="panel-heading panel-heading--mini">
              <div>
                <p class="panel-kicker">Schedule</p>
                <h3>Ventanas de ejecución</h3>
              </div>
            </div>
            <div class="schedule-row">
              <select v-model.number="editSchedule.dayOfWeek">
                <option :value="1">Lunes</option>
                <option :value="2">Martes</option>
                <option :value="3">Miércoles</option>
                <option :value="4">Jueves</option>
                <option :value="5">Viernes</option>
                <option :value="6">Sábado</option>
                <option :value="0">Domingo</option>
              </select>
              <input v-model="editSchedule.time" type="time" />
              <button class="ghost-button icon-button icon-button--soft" type="button" data-tooltip="Agregar horario" aria-label="Agregar horario" @click="addEditSchedule">
                <AppIcon class="icon-svg" name="plus" />
              </button>
            </div>
            <ul class="chip-list">
              <li v-for="(slot, index) in editForm.schedules" :key="`${slot.dayOfWeek}-${slot.time}-${index}`" class="chip-list__item">
                <span>{{ dayLabels[slot.dayOfWeek] }} {{ slot.time }}</span>
                <button class="chip-remove" type="button" @click="removeEditSchedule(index)">×</button>
              </li>
            </ul>
          </div>

          <div class="modal-actions modal-span-full">
            <button class="ghost-button ghost-button--danger modal-actions__danger button--compact" type="button" @click="deleteTaskById(editingTaskId, editForm.name)">Eliminar</button>
            <button class="ghost-button button--compact" type="button" @click="closeEditModal">Cancelar</button>
            <button class="button--compact" type="submit">Guardar cambios</button>
          </div>
        </form>
      </section>
    </div>
  </section>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { api, type ConnectionProfile, type SqlScriptAsset, type TaskDefinition } from '@/api'
import AppIcon from '@/components/AppIcon.vue'

const connections = ref<ConnectionProfile[]>([])
const scripts = ref<SqlScriptAsset[]>([])
const tasks = ref<TaskDefinition[]>([])
const feedback = ref('')
const isEditModalOpen = ref(false)
const editingTaskId = ref('')
const dayLabels: Record<number, string> = {
  0: 'Dom',
  1: 'Lun',
  2: 'Mar',
  3: 'Mié',
  4: 'Jue',
  5: 'Vie',
  6: 'Sáb',
}

function createTaskFormState() {
  return {
    name: '',
    connectionId: '',
    engine: 0,
    sourceKind: 0,
    sqlScriptId: '',
    storedProcedureName: '',
    parameters: [] as Array<{ name: string; value: string }>,
    automatic: true,
    enabled: true,
    schedules: [] as Array<{ dayOfWeek: number; time: string }>,
    retryPolicy: {
      maxRetries: 0,
      delayMinutes: 5,
    },
    timeoutSeconds: 300,
  }
}

const form = reactive(createTaskFormState())
const editForm = reactive(createTaskFormState())

const schedule = reactive({
  dayOfWeek: 1,
  time: '08:00',
})

const editSchedule = reactive({
  dayOfWeek: 1,
  time: '08:00',
})

async function loadAll() {
  const [connectionsResponse, scriptsResponse, tasksResponse] = await Promise.all([
    api.get<ConnectionProfile[]>('/connections'),
    api.get<SqlScriptAsset[]>('/scripts'),
    api.get<TaskDefinition[]>('/tasks'),
  ])
  connections.value = connectionsResponse.data
  scripts.value = scriptsResponse.data
  tasks.value = tasksResponse.data
}

function addSchedule() {
  form.schedules.push({ dayOfWeek: schedule.dayOfWeek, time: schedule.time })
}

function addEditSchedule() {
  editForm.schedules.push({ dayOfWeek: editSchedule.dayOfWeek, time: editSchedule.time })
}

function removeEditSchedule(index: number) {
  editForm.schedules.splice(index, 1)
}

function buildTaskPayload(taskForm: ReturnType<typeof createTaskFormState>) {
  return {
    ...taskForm,
    sqlScriptId: taskForm.sourceKind === 0 ? taskForm.sqlScriptId || null : null,
    storedProcedureName: taskForm.sourceKind === 1 ? taskForm.storedProcedureName || null : null,
  }
}

function resetCreateForm() {
  Object.assign(form, createTaskFormState())
}

function resetEditForm() {
  Object.assign(editForm, createTaskFormState())
  editingTaskId.value = ''
}

function openEditModal(task: TaskDefinition) {
  editingTaskId.value = task.id
  Object.assign(editForm, {
    name: task.name,
    connectionId: task.connectionId,
    engine: task.engine === 'mySql' ? 0 : 1,
    sourceKind: task.sourceKind === 'sqlFile' ? 0 : 1,
    sqlScriptId: task.sqlScriptId ?? '',
    storedProcedureName: task.storedProcedureName ?? '',
    parameters: task.parameters.map((parameter) => ({ ...parameter })),
    automatic: task.automatic,
    enabled: task.enabled,
    schedules: task.schedules.map((slot) => ({ ...slot })),
    retryPolicy: {
      maxRetries: task.retryPolicy.maxRetries,
      delayMinutes: task.retryPolicy.delayMinutes,
    },
    timeoutSeconds: task.timeoutSeconds,
  })
  isEditModalOpen.value = true
}

function closeEditModal() {
  isEditModalOpen.value = false
  resetEditForm()
}

async function createTask() {
  await api.post('/tasks', buildTaskPayload(form))
  feedback.value = 'Tarea creada.'
  resetCreateForm()
  await loadAll()
}

async function updateTask() {
  await api.put(`/tasks/${editingTaskId.value}`, buildTaskPayload(editForm))
  feedback.value = 'Tarea actualizada.'
  closeEditModal()
  await loadAll()
}

async function runTask(id: string) {
  await api.post(`/tasks/${id}/run`)
  feedback.value = 'Tarea enviada a ejecución.'
}

async function deleteTask(task: TaskDefinition) {
  await deleteTaskById(task.id, task.name)
}

async function deleteTaskById(id: string, name: string) {
  if (!window.confirm(`Se eliminará la tarea "${name}". El historial de ejecuciones se conservará. ¿Continuar?`)) {
    return
  }

  await api.delete(`/tasks/${id}`)
  if (editingTaskId.value === id) {
    closeEditModal()
  }
  feedback.value = 'Tarea eliminada.'
  await loadAll()
}

onMounted(loadAll)
</script>