<template>
  <section class="view-stack view-stack--connections">
    <header class="page-header">
      <div>
        <p class="eyebrow">Conectividad</p>
        <h2>Servidores de base de datos</h2>
        <p class="section-copy">Armá destinos nuevos con una lectura más visual y testealos desde el mismo panel.</p>
      </div>
      <button class="icon-button" type="button" data-tooltip="Recargar conexiones" aria-label="Recargar conexiones" @click="loadConnections">
        <AppIcon class="icon-svg" name="refresh" />
      </button>
    </header>

    <section class="hero-panel hero-panel--connections">
      <div class="hero-copy">
        <p class="eyebrow">Connection deck</p>
        <h3>Tu mapa de servidores, ordenado y listo para correr</h3>
      </div>
      <div class="hero-sidekick">
        <div class="mini-stat-strip">
          <span>Conexiones live</span>
          <strong>{{ connections.length }}</strong>
        </div>
      </div>
    </section>

    <section class="panel-card panel-card--dense">
      <div class="panel-heading">
        <div>
          <p class="panel-kicker">Builder</p>
          <h3>Nueva conexión</h3>
        </div>
        <span class="count-pill">DB</span>
      </div>
      <form class="form-grid form-grid--three form-grid--styled" @submit.prevent="createConnection">
        <label>
          <span class="field-head"><span class="field-icon">ID</span>Nombre</span>
          <input v-model="form.name" type="text" />
        </label>
        <label>
          <span class="field-head"><span class="field-icon">ENG</span>Motor</span>
          <select v-model.number="form.engine">
            <option value="0">MySQL</option>
            <option value="1">SQL Server</option>
          </select>
        </label>
        <label>
          <span class="field-head"><span class="field-icon">NET</span>Host</span>
          <input v-model="form.server" type="text" />
        </label>
        <label>
          <span class="field-head"><span class="field-icon">PORT</span>Puerto</span>
          <input v-model.number="form.port" type="number" />
        </label>
        <label>
          <span class="field-head"><span class="field-icon">DB</span>Base</span>
          <input v-model="form.database" type="text" />
        </label>
        <label>
          <span class="field-head"><span class="field-icon">USR</span>Usuario</span>
          <input v-model="form.username" type="text" />
        </label>
        <label>
          <span class="field-head"><span class="field-icon">KEY</span>Contraseña</span>
          <input v-model="form.password" type="password" />
        </label>
        <label class="checkbox-field">
          <input v-model="form.trustServerCertificate" type="checkbox" />
          Confiar en certificado del servidor
        </label>
        <button class="button--compact" type="submit">Guardar conexión</button>
      </form>
    </section>

    <section class="panel-card panel-card--dense">
      <div class="panel-heading">
        <div>
          <p class="panel-kicker">Directory</p>
          <h3>Conexiones registradas</h3>
        </div>
        <span class="count-pill count-pill--warm">{{ connections.length }}</span>
      </div>
      <table class="data-table">
        <thead>
          <tr>
            <th>Nombre</th>
            <th>Motor</th>
            <th>Servidor</th>
            <th>Base</th>
            <th>Acciones</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="connection in connections" :key="connection.id">
            <td>
              <div class="data-lead">
                <span class="cell-icon">DB</span>
                <div>
                  <strong>{{ connection.name }}</strong>
                  <small>{{ connection.username }}</small>
                </div>
              </div>
            </td>
            <td><span class="badge badge--soft">{{ connection.engine === 'mySql' ? 'MySQL' : 'SQL Server' }}</span></td>
            <td>{{ connection.server }}:{{ connection.port }}</td>
            <td>{{ connection.database }}</td>
            <td>
              <div class="action-row">
                <button class="ghost-button icon-button" type="button" data-tooltip="Editar conexión" aria-label="Editar conexión" @click="openEditModal(connection)">
                  <AppIcon class="icon-svg" name="edit" />
                </button>
                <button class="ghost-button icon-button" type="button" data-tooltip="Probar conexión" aria-label="Probar conexión" @click="testConnection(connection.id)">
                  <AppIcon class="icon-svg" name="test" />
                </button>
                <button class="ghost-button ghost-button--danger icon-button" type="button" data-tooltip="Eliminar conexión" aria-label="Eliminar conexión" @click="deleteConnection(connection)">
                  <AppIcon class="icon-svg" name="delete" />
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </section>

    <div v-if="isEditModalOpen" class="modal-overlay" @click.self="closeEditModal">
      <section class="modal-card">
        <div class="panel-heading">
          <div>
            <p class="panel-kicker">Edit</p>
            <h3>Modificar conexión</h3>
          </div>
          <button class="ghost-button icon-button" type="button" data-tooltip="Cerrar modal" aria-label="Cerrar modal" @click="closeEditModal">
            <AppIcon class="icon-svg" name="close" />
          </button>
        </div>

        <form class="form-grid form-grid--three form-grid--styled" @submit.prevent="updateConnection">
          <label>
            <span class="field-head"><span class="field-icon">ID</span>Nombre</span>
            <input v-model="editForm.name" type="text" />
          </label>
          <label>
            <span class="field-head"><span class="field-icon">ENG</span>Motor</span>
            <select v-model.number="editForm.engine">
              <option value="0">MySQL</option>
              <option value="1">SQL Server</option>
            </select>
          </label>
          <label>
            <span class="field-head"><span class="field-icon">NET</span>Host</span>
            <input v-model="editForm.server" type="text" />
          </label>
          <label>
            <span class="field-head"><span class="field-icon">PORT</span>Puerto</span>
            <input v-model.number="editForm.port" type="number" />
          </label>
          <label>
            <span class="field-head"><span class="field-icon">DB</span>Base</span>
            <input v-model="editForm.database" type="text" />
          </label>
          <label>
            <span class="field-head"><span class="field-icon">USR</span>Usuario</span>
            <input v-model="editForm.username" type="text" />
          </label>
          <label>
            <span class="field-head"><span class="field-icon">KEY</span>Nueva contraseña</span>
            <input v-model="editForm.password" type="password" placeholder="Dejar vacío para conservar" />
          </label>
          <label class="checkbox-field">
            <input v-model="editForm.trustServerCertificate" type="checkbox" />
            Confiar en certificado del servidor
          </label>
          <label class="checkbox-field">
            <input v-model="editForm.enabled" type="checkbox" />
            Conexión habilitada
          </label>

          <div class="modal-actions modal-span-full">
            <button class="ghost-button ghost-button--danger modal-actions__danger button--compact" type="button" @click="deleteConnectionById(editingConnectionId, editForm.name)">Eliminar</button>
            <button class="ghost-button button--compact" type="button" @click="closeEditModal">Cancelar</button>
            <button class="button--compact" type="submit">Guardar cambios</button>
          </div>
        </form>
      </section>
    </div>

    <ConfirmDialog
      :open="confirmState.open"
      :title="confirmState.title"
      :message="confirmState.message"
      :detail="confirmState.detail"
      confirm-label="Eliminar"
      @cancel="closeConfirmDialog"
      @confirm="confirmDelete"
    />

    <AppToast :open="toast.open" :message="toast.message" @close="closeToast" />
  </section>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, reactive, ref } from 'vue'
import { api, type ConnectionProfile } from '@/api'
import AppIcon from '@/components/AppIcon.vue'
import AppToast from '@/components/AppToast.vue'
import ConfirmDialog from '@/components/ConfirmDialog.vue'

const connections = ref<ConnectionProfile[]>([])
const isEditModalOpen = ref(false)
const editingConnectionId = ref('')
const toast = reactive({
  open: false,
  message: '',
})
const confirmState = reactive({
  open: false,
  title: '',
  message: '',
  detail: '',
})
let pendingDeleteId = ''
let pendingDeleteName = ''
let toastTimer: ReturnType<typeof setTimeout> | undefined

function createConnectionFormState() {
  return {
    name: '',
    engine: 0,
    server: '',
    port: 3306,
    database: '',
    username: '',
    password: '',
    trustServerCertificate: false,
    enabled: true,
  }
}

const form = reactive(createConnectionFormState())
const editForm = reactive(createConnectionFormState())

function showToast(message: string) {
  toast.message = message
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

async function loadConnections() {
  const response = await api.get<ConnectionProfile[]>('/connections')
  connections.value = response.data
}

async function createConnection() {
  await api.post('/connections', form)
  showToast('Conexión guardada.')
  Object.assign(form, createConnectionFormState())
  await loadConnections()
}

function openEditModal(connection: ConnectionProfile) {
  editingConnectionId.value = connection.id
  Object.assign(editForm, {
    name: connection.name,
    engine: connection.engine === 'mySql' ? 0 : 1,
    server: connection.server,
    port: connection.port,
    database: connection.database,
    username: connection.username,
    password: '',
    trustServerCertificate: connection.trustServerCertificate,
    enabled: connection.enabled,
  })
  isEditModalOpen.value = true
}

function closeEditModal() {
  isEditModalOpen.value = false
  editingConnectionId.value = ''
  Object.assign(editForm, createConnectionFormState())
}

async function updateConnection() {
  await api.put(`/connections/${editingConnectionId.value}`, editForm)
  showToast('Conexión actualizada.')
  closeEditModal()
  await loadConnections()
}

async function testConnection(id: string) {
  await api.post(`/connections/${id}/test`)
  showToast('Conexión validada correctamente.')
}

async function deleteConnection(connection: ConnectionProfile) {
  deleteConnectionById(connection.id, connection.name)
}

function deleteConnectionById(id: string, name: string) {
  pendingDeleteId = id
  pendingDeleteName = name
  confirmState.title = 'Eliminar conexión'
  confirmState.message = `Se eliminará la conexión "${name}".`
  confirmState.detail = 'Si la conexión está asociada a tareas, el backend bloqueará la operación para evitar inconsistencias.'
  confirmState.open = true
}

function closeConfirmDialog() {
  confirmState.open = false
  pendingDeleteId = ''
  pendingDeleteName = ''
}

async function confirmDelete() {
  const id = pendingDeleteId
  const name = pendingDeleteName
  closeConfirmDialog()
  if (!id) {
    return
  }

  await api.delete(`/connections/${id}`)
  if (editingConnectionId.value === id) {
    closeEditModal()
  }
  showToast(`Conexión "${name}" eliminada.`)
  await loadConnections()
}

onMounted(loadConnections)
onUnmounted(closeToast)
</script>