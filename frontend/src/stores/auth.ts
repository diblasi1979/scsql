import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { api } from '@/api'
import { clearStoredAuthToken, getStoredAuthToken, storeAuthToken } from '@/authToken'
import router from '@/router'

export const useAuthStore = defineStore('auth', () => {
  const token = ref(getStoredAuthToken())
  const isAuthenticated = computed(() => token.value.length > 0)

  async function login(username: string, password: string) {
    const response = await api.post('/auth/login', { username, password })
    token.value = response.data.token
    storeAuthToken(token.value)
  }

  async function logout() {
    token.value = ''
    clearStoredAuthToken()
    await router.push({ name: 'login' })
  }

  return {
    token,
    isAuthenticated,
    login,
    logout,
  }
})