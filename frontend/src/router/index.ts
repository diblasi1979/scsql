import { createRouter, createWebHistory } from 'vue-router'
import LoginView from '@/views/LoginView.vue'
import DashboardView from '@/views/DashboardView.vue'
import ConnectionsView from '@/views/ConnectionsView.vue'
import ScriptsView from '@/views/ScriptsView.vue'
import TasksView from '@/views/TasksView.vue'
import ExecutionsView from '@/views/ExecutionsView.vue'
import LicenseStudioView from '@/views/LicenseStudioView.vue'
import { getStoredAuthToken } from '@/authToken'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', name: 'login', component: LoginView },
    { path: '/', name: 'dashboard', component: DashboardView },
    { path: '/connections', name: 'connections', component: ConnectionsView },
    { path: '/scripts', name: 'scripts', component: ScriptsView },
    { path: '/tasks', name: 'tasks', component: TasksView },
    { path: '/executions', name: 'executions', component: ExecutionsView },
    { path: '/license-studio', name: 'license-studio', component: LicenseStudioView },
  ],
})

router.beforeEach((to) => {
  const token = getStoredAuthToken()
  const publicRoutes = new Set(['login', 'license-studio'])

  if (!publicRoutes.has(String(to.name)) && !token) {
    return { name: 'login' }
  }

  if (to.name === 'login' && token) {
    return { name: 'dashboard' }
  }

  return true
})

export default router