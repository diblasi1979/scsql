<template>
  <section class="view-stack">
    <header class="page-header">
      <div>
        <p class="eyebrow">Repositorio local</p>
        <h2>Scripts SQL</h2>
        <p class="section-copy">Subí archivos, centralizá versiones y dejá el material listo para tareas manuales o automáticas.</p>
      </div>
      <button class="icon-button" type="button" data-tooltip="Recargar scripts" aria-label="Recargar scripts" @click="loadScripts">
        <AppIcon class="icon-svg" name="refresh" />
      </button>
    </header>

    <section class="hero-panel hero-panel--scripts">
      <div class="hero-copy">
        <p class="eyebrow">Script cloud</p>
        <h3>Una biblioteca viva para tus jobs</h3>
      </div>
      <div class="hero-sidekick">
        <div class="mini-stat-strip">
          <span>Archivos guardados</span>
          <strong>{{ scripts.length }}</strong>
        </div>
      </div>
    </section>

    <section class="panel-card panel-card--dense">
      <div class="panel-heading">
        <div>
          <p class="panel-kicker">Upload</p>
          <h3>Subir archivo .sql</h3>
        </div>
        <span class="count-pill">SQL</span>
      </div>
      <form class="upload-row upload-row--card" @submit.prevent="uploadScript">
        <div class="upload-field">
          <span class="field-head"><span class="field-icon">FILE</span>Script fuente</span>
          <input accept=".sql" type="file" @change="onFileChange" />
        </div>
        <button class="button--compact" type="submit">Subir</button>
      </form>
      <p v-if="feedback" class="success-text">{{ feedback }}</p>
    </section>

    <section class="panel-card panel-card--dense">
      <div class="panel-heading">
        <div>
          <p class="panel-kicker">Library</p>
          <h3>Scripts almacenados</h3>
        </div>
        <span class="count-pill count-pill--warm">{{ scripts.length }}</span>
      </div>
      <table class="data-table">
        <thead>
          <tr>
            <th>Nombre original</th>
            <th>Ruta</th>
            <th>Subido</th>
            <th>Acciones</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="script in scripts" :key="script.id">
            <td>
              <div class="data-lead">
                <span class="cell-icon">SQL</span>
                <div>
                  <strong>{{ script.originalName }}</strong>
                  <small>{{ script.id }}</small>
                </div>
              </div>
            </td>
            <td><span class="path-pill">{{ script.relativePath }}</span></td>
            <td>{{ new Date(script.uploadedAtUtc).toLocaleString('es-AR') }}</td>
            <td>
              <div class="action-row">
                <button class="ghost-button icon-button" type="button" data-tooltip="Editar script" aria-label="Editar script" @click="openEditModal(script)">
                  <AppIcon class="icon-svg" name="edit" />
                </button>
                <button class="ghost-button ghost-button--danger icon-button" type="button" data-tooltip="Eliminar script" aria-label="Eliminar script" @click="deleteScript(script)">
                  <AppIcon class="icon-svg" name="delete" />
                </button>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </section>

    <div v-if="isEditModalOpen" class="modal-overlay" @click.self="closeEditModal">
      <section class="modal-card modal-card--compact">
        <div class="panel-heading">
          <div>
            <p class="panel-kicker">Edit</p>
            <h3>Modificar script</h3>
          </div>
          <button class="ghost-button icon-button" type="button" data-tooltip="Cerrar modal" aria-label="Cerrar modal" @click="closeEditModal">
            <AppIcon class="icon-svg" name="close" />
          </button>
        </div>

        <form class="form-grid form-grid--styled" @submit.prevent="updateScript">
          <label class="modal-span-full">
            <span class="field-head"><span class="field-icon">FILE</span>Nombre visible</span>
            <input v-model="editOriginalName" type="text" />
          </label>
          <label class="modal-span-full upload-field">
            <span class="field-head"><span class="field-icon">SQL</span>Reemplazar archivo .sql</span>
            <input accept=".sql" type="file" @change="onEditFileChange" />
          </label>

          <div class="modal-actions modal-span-full">
            <button class="ghost-button ghost-button--danger modal-actions__danger button--compact" type="button" @click="deleteScriptById(editingScriptId, editOriginalName)">Eliminar</button>
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
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api, type SqlScriptAsset } from '@/api'
import AppIcon from '@/components/AppIcon.vue'
import ConfirmDialog from '@/components/ConfirmDialog.vue'

const scripts = ref<SqlScriptAsset[]>([])
const feedback = ref('')
const selectedFile = ref<File | null>(null)
const isEditModalOpen = ref(false)
const editingScriptId = ref('')
const editOriginalName = ref('')
const editSelectedFile = ref<File | null>(null)
const confirmState = ref({
  open: false,
  title: '',
  message: '',
  detail: '',
})
let pendingDeleteId = ''
let pendingDeleteName = ''

function onFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  selectedFile.value = input.files?.[0] ?? null
}

function onEditFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  editSelectedFile.value = input.files?.[0] ?? null
}

async function loadScripts() {
  const response = await api.get<SqlScriptAsset[]>('/scripts')
  scripts.value = response.data
}

async function uploadScript() {
  if (!selectedFile.value) {
    return
  }

  const form = new FormData()
  form.append('file', selectedFile.value)
  await api.post('/scripts', form)
  feedback.value = 'Script cargado y almacenado en el servidor.'
  selectedFile.value = null
  await loadScripts()
}

function openEditModal(script: SqlScriptAsset) {
  editingScriptId.value = script.id
  editOriginalName.value = script.originalName
  editSelectedFile.value = null
  isEditModalOpen.value = true
}

function closeEditModal() {
  isEditModalOpen.value = false
  editingScriptId.value = ''
  editOriginalName.value = ''
  editSelectedFile.value = null
}

async function updateScript() {
  const form = new FormData()
  form.append('originalName', editOriginalName.value)
  if (editSelectedFile.value) {
    form.append('file', editSelectedFile.value)
  }

  await api.put(`/scripts/${editingScriptId.value}`, form)
  feedback.value = 'Script actualizado.'
  closeEditModal()
  await loadScripts()
}

async function deleteScript(script: SqlScriptAsset) {
  deleteScriptById(script.id, script.originalName)
}

function deleteScriptById(id: string, name: string) {
  pendingDeleteId = id
  pendingDeleteName = name
  confirmState.value = {
    open: true,
    title: 'Eliminar script',
    message: `Se eliminará el script "${name}".`,
    detail: 'Si el script está asociado a tareas, el backend bloqueará la operación para evitar romper ejecuciones futuras.',
  }
}

function closeConfirmDialog() {
  confirmState.value = {
    open: false,
    title: '',
    message: '',
    detail: '',
  }
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

  await api.delete(`/scripts/${id}`)
  if (editingScriptId.value === id) {
    closeEditModal()
  }
  feedback.value = `Script "${name}" eliminado.`
  await loadScripts()
}

onMounted(loadScripts)
</script>