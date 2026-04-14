import { ref, computed } from 'vue'
import { defineStore } from 'pinia'
import type {
  LogEntry, ModuleInfo, TrackingData, ParameterValue,
  CalibrationState, OscStatus, AvatarInfo, AppConfig, OscConfig, RegistryModule,
  ModuleConfigPayload
} from '@/types'

export const useAppStore = defineStore('app', () => {
  const version = ref('dev')

  // State
  const logs = ref<LogEntry[]>([])
  const modules = ref<ModuleInfo[]>([])
  const trackingData = ref<TrackingData | null>(null)
  const parameterValues = ref<ParameterValue[]>([])
  const calibration = ref<CalibrationState>({ active: false, progress: [] })
  const oscStatus = ref<OscStatus>({ connected: false, ip: '127.0.0.1', port: 9000, msgsPerSec: 0 })
  const avatarInfo = ref<AvatarInfo | null>(null)
  // In dev builds (vite build --mode development) debug logs are visible by default.
  // In production builds (vite build) they are hidden by default.
  const config = ref<AppConfig>({ debugMode: import.meta.env.DEV })
  const oscConfig = ref<OscConfig>({ ip: '127.0.0.1', sendPort: 9000, recvPort: 9001 })
  const registryModules = ref<RegistryModule[]>([])
  const registryLoading = ref(false)
  const moduleConfigs = ref<Record<string, ModuleConfigPayload>>({})

  // Computed
  const activeModules = computed(() => modules.value.filter(m => m.status === 'Active'))
  const hasActiveTracking = computed(() => activeModules.value.length > 0)
  const visibleLogs = computed(() =>
    config.value.debugMode
      ? logs.value
      : logs.value.filter(l => l.level?.toLowerCase() !== 'debug')
  )

  function handleMessage(type: string, data: unknown) {
    switch (type) {
      case 'LOG':
        logs.value.push(data as LogEntry)
        if (logs.value.length > 2000) logs.value.shift()
        break
      case 'CONFIG':
        if (data) config.value = data as AppConfig
        break
      case 'OSC_CONFIG':
        if (data) oscConfig.value = data as OscConfig
        break
      case 'MODULE_LIST':
        modules.value = data as ModuleInfo[]
        break
      case 'TRACKING_DATA':
        trackingData.value = data as TrackingData
        break
      case 'PARAMETER_VALUES':
        parameterValues.value = data as ParameterValue[]
        break
      case 'CALIBRATION_STATE':
        calibration.value = data as CalibrationState
        break
      case 'OSC_STATUS':
        oscStatus.value = data as OscStatus
        break
      case 'AVATAR_INFO':
        avatarInfo.value = data as AvatarInfo
        break
      case 'REGISTRY_MODULES':
        registryModules.value = data as RegistryModule[]
        registryLoading.value = false
        break
      case 'INSTALL_PROGRESS': {
        const p = data as { packageId: string; progress: number }
        const mod = registryModules.value.find(m => m.packageId === p.packageId)
        if (mod) {
          mod.installState = 'Installing'
          mod.installProgress = p.progress
        }
        break
      }
      case 'INSTALL_RESULT': {
        const r = data as { packageId: string; success: boolean; error?: string }
        const mod = registryModules.value.find(m => m.packageId === r.packageId)
        if (mod) {
          mod.installState = r.success ? 'Installed' : 'Error'
          mod.installProgress = undefined
        }
        if (r.success) getModules()
        break
      }
      case 'UNINSTALL_RESULT': {
        const u = data as { packageId: string; success: boolean }
        const mod = registryModules.value.find(m => m.packageId === u.packageId)
        if (mod && u.success) mod.installState = 'NotInstalled'
        if (u.success) getModules()
        break
      }
      case 'MODULE_CONFIG': {
        const c = data as ModuleConfigPayload
        if (c?.moduleId) moduleConfigs.value[c.moduleId] = c
        break
      }
    }
  }

  function send(type: string, data?: unknown) {
    window.photino.sendMessage(JSON.stringify({ type, data }))
  }

  function syncLogs()    { send('SYNC_LOGS') }
  function getConfig()   { send('GET_CONFIG') }
  function saveConfig()  { send('SAVE_CONFIG', config.value) }
  function getOscConfig(){ send('GET_OSC_CONFIG') }
  function saveOscConfig(){ send('SAVE_OSC_CONFIG', oscConfig.value) }
  function getModules()  { send('GET_MODULES') }
  function getRegistry() {
    registryLoading.value = true
    send('GET_REGISTRY')
  }
  function installModule(packageId: string)  { send('INSTALL_MODULE', { packageId }) }
  function uninstallModule(packageId: string){ send('UNINSTALL_MODULE', { packageId }) }
  function restartModule(moduleId: string)   { send('RESTART_MODULE', { moduleId }) }
  function enableModule(moduleId: string)  { send('ENABLE_MODULE',  { moduleId }) }
  function disableModule(moduleId: string) { send('DISABLE_MODULE', { moduleId }) }
  function getModuleConfig(moduleId: string)  { send('GET_MODULE_CONFIG',  { moduleId }) }
  function saveModuleConfig(moduleId: string, values: Record<string, unknown>) {
    send('SAVE_MODULE_CONFIG', { moduleId, values })
  }
  function startCalibration()  { send('START_CALIBRATION') }
  function stopCalibration()   { send('STOP_CALIBRATION') }
  function resetCalibration(expressionIndex?: number) { send('RESET_CALIBRATION', { expressionIndex }) }
  function exit() { send('EXIT') }

  function initBridge() {
    if (window.chrome?.webview) {
      window.chrome.webview.addEventListener('message', (e: { data: string }) => {
        try { const msg = JSON.parse(e.data); handleMessage(msg.type, msg.data) } catch { }
      })
    } else {
      window.addEventListener('message', (e: MessageEvent) => {
        try {
          const msg = typeof e.data === 'string' ? JSON.parse(e.data) : e.data
          handleMessage(msg.type, msg.data)
        } catch { }
      })
    }
    syncLogs()
    getConfig()
    getOscConfig()
    getModules()
  }

  return {
    version, logs, visibleLogs, modules, activeModules, hasActiveTracking,
    trackingData, parameterValues, calibration, oscStatus, avatarInfo,
    config, oscConfig, registryModules, registryLoading, moduleConfigs,
    handleMessage, send, initBridge,
    syncLogs, getConfig, saveConfig, getOscConfig, saveOscConfig,
    getModules, getRegistry, installModule, uninstallModule, restartModule,
    enableModule, disableModule,
    getModuleConfig, saveModuleConfig,
    startCalibration, stopCalibration, resetCalibration, exit
  }
})









