import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { api, type LicenseStatus } from '@/api'

export const useLicenseStore = defineStore('license', () => {
  const status = ref<LicenseStatus | null>(null)
  const loading = ref(false)
  const errorMessage = ref('')

  const hasBlockingIssue = computed(() => status.value?.shouldBlock ?? false)

  async function refreshStatus() {
    try {
      loading.value = true
      errorMessage.value = ''
      const response = await api.get<LicenseStatus>('/license/status')
      status.value = response.data
    } catch {
      errorMessage.value = 'No se pudo consultar el estado de la licencia.'
    } finally {
      loading.value = false
    }
  }

  return {
    status,
    loading,
    errorMessage,
    hasBlockingIssue,
    refreshStatus,
  }
})