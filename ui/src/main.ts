import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { createRouter, createWebHashHistory } from 'vue-router'
import App from './App.vue'
import './assets/main.css'

// Set up Photino bridge
if (window.chrome?.webview) {
  window.photino = {
    sendMessage: (msg: string) => window.chrome!.webview!.postMessage(msg)
  }
} else if (window.external && 'sendMessage' in window.external) {
  window.photino = {
    sendMessage: (msg: string) => (window.external as unknown as { sendMessage: (m: string) => void }).sendMessage(msg)
  }
} else {
  // Dev fallback - log messages to console
  window.photino = {
    sendMessage: (msg: string) => console.log('[Bridge TX]', JSON.parse(msg))
  }
}

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    { path: '/', name: 'dashboard', component: () => import('./views/DashboardView.vue') },
    { path: '/modules', name: 'modules', component: () => import('./views/ModulesView.vue') },
    { path: '/parameters', name: 'parameters', component: () => import('./views/ParametersView.vue') },
    { path: '/calibration', name: 'calibration', component: () => import('./views/CalibrationView.vue') },
    { path: '/output', name: 'output', component: () => import('./views/OutputView.vue') },
    { path: '/settings', name: 'settings', component: () => import('./views/SettingsView.vue') },
  ],
})

const app = createApp(App)
app.use(createPinia())
app.use(router)
app.mount('#app')
