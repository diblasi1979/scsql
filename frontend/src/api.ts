import axios from 'axios'

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:2008/api',
})

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('scsql-token')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  return config
})

export type Dashboard = {
  activeTasks: number
  totalConnections: number
  failedExecutionsLast7Days: number
  upcomingTasks: TaskDefinition[]
  recentExecutions: ExecutionRecord[]
}

export type ConnectionProfile = {
  id: string
  name: string
  engine: 'mySql' | 'sqlServer'
  server: string
  port: number
  database: string
  username: string
  password?: string
  trustServerCertificate: boolean
  enabled: boolean
}

export type SqlScriptAsset = {
  id: string
  originalName: string
  storedFileName: string
  relativePath: string
  uploadedAtUtc: string
}

export type ScheduleSlot = {
  dayOfWeek: number
  time: string
}

export type RetryPolicy = {
  maxRetries: number
  delayMinutes: number
}

export type TaskParameter = {
  name: string
  value: string
}

export type TaskDefinition = {
  id: string
  name: string
  connectionId: string
  engine: 'mySql' | 'sqlServer'
  sourceKind: 'sqlFile' | 'storedProcedure'
  sqlScriptId?: string | null
  storedProcedureName?: string | null
  parameters: TaskParameter[]
  automatic: boolean
  enabled: boolean
  schedules: ScheduleSlot[]
  retryPolicy: RetryPolicy
  timeoutSeconds: number
  lastScheduledRunUtc?: string | null
}

export type ExecutionRecord = {
  id: string
  taskId: string
  taskName: string
  status: 'pending' | 'running' | 'success' | 'failed' | 'retrying'
  startedAtUtc: string
  finishedAtUtc?: string | null
  manualTrigger: boolean
  attempts: number
  errorSummary?: string | null
  errorDetail?: string | null
  durationMs?: number | null
  rowsAffected?: number | null
}