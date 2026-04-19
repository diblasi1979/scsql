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
          </tr>
        </tbody>
      </table>
    </section>
  </section>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api, type SqlScriptAsset } from '@/api'
import AppIcon from '@/components/AppIcon.vue'

const scripts = ref<SqlScriptAsset[]>([])
const feedback = ref('')
const selectedFile = ref<File | null>(null)

function onFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  selectedFile.value = input.files?.[0] ?? null
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

onMounted(loadScripts)
</script>