import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { api } from '@/api'
import router from '@/router'

export const useAuthStore = defineStore('auth', () => {
  const token = ref(localStorage.getItem('scsql-token') ?? '')
  const isAuthenticated = computed(() => token.value.length > 0)

  async function login(username: string, password: string) {
    const response = await api.post('/auth/login', { username, password })
    token.value = response.data.token
    localStorage.setItem('scsql-token', token.value)
  }

  async function logout() {
    token.value = ''
    localStorage.removeItem('scsql-token')
    await router.push({ name: 'login' })
  }

  return {
    token,
    isAuthenticated,
    login,
    logout,
  }
})