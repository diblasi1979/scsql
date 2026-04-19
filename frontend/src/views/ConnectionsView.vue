<template>
  <section class="view-stack">
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
            <th></th>
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
              <button class="ghost-button icon-button" type="button" data-tooltip="Probar conexión" aria-label="Probar conexión" @click="testConnection(connection.id)">
                <AppIcon class="icon-svg" name="test" />
              </button>
            </td>
          </tr>
        </tbody>
      </table>
      <p v-if="feedback" class="success-text">{{ feedback }}</p>
    </section>
  </section>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { api, type ConnectionProfile } from '@/api'
import AppIcon from '@/components/AppIcon.vue'

const connections = ref<ConnectionProfile[]>([])
const feedback = ref('')
const form = reactive({
  name: '',
  engine: 0,
  server: '',
  port: 3306,
  database: '',
  username: '',
  password: '',
  trustServerCertificate: false,
})

async function loadConnections() {
  const response = await api.get<ConnectionProfile[]>('/connections')
  connections.value = response.data
}

async function createConnection() {
  await api.post('/connections', form)
  feedback.value = 'Conexión guardada.'
  Object.assign(form, {
    name: '',
    engine: 0,
    server: '',
    port: 3306,
    database: '',
    username: '',
    password: '',
    trustServerCertificate: false,
  })
  await loadConnections()
}

async function testConnection(id: string) {
  await api.post(`/connections/${id}/test`)
  feedback.value = 'Conexión validada correctamente.'
}

onMounted(loadConnections)
</script>