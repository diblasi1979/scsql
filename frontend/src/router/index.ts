import { createRouter, createWebHistory } from 'vue-router'
import LoginView from '@/views/LoginView.vue'
import DashboardView from '@/views/DashboardView.vue'
import ConnectionsView from '@/views/ConnectionsView.vue'
import ScriptsView from '@/views/ScriptsView.vue'
import TasksView from '@/views/TasksView.vue'
import ExecutionsView from '@/views/ExecutionsView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', name: 'login', component: LoginView },
    { path: '/', name: 'dashboard', component: DashboardView },
    { path: '/connections', name: 'connections', component: ConnectionsView },
    { path: '/scripts', name: 'scripts', component: ScriptsView },
    { path: '/tasks', name: 'tasks', component: TasksView },
    { path: '/executions', name: 'executions', component: ExecutionsView },
  ],
})

router.beforeEach((to) => {
  const token = localStorage.getItem('scsql-token')
  if (to.name !== 'login' && !token) {
    return { name: 'login' }
  }

  if (to.name === 'login' && token) {
    return { name: 'dashboard' }
  }

  return true
})

export default router