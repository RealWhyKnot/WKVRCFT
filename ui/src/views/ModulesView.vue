<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAppStore } from '@/stores/appStore'
import { Puzzle, Download, Trash2, Search, RefreshCw, Eye, Smile, AlertCircle, CheckCircle2, Cpu, Settings2 } from 'lucide-vue-next'
import ModuleConfigPanel from '@/components/ModuleConfigPanel.vue'

const store = useAppStore()
const searchQuery  = ref('')
const showRegistry = ref(false)
const showConfigFor = ref<string | null>(null)
const showLogFor = ref<string | null>(null)

function toggleLog(moduleId: string) {
  showLogFor.value = showLogFor.value === moduleId ? null : moduleId
}

onMounted(() => store.getModules())

function toggleConfig(moduleId: string) {
  if (showConfigFor.value === moduleId) {
    showConfigFor.value = null
    return
  }
  showConfigFor.value = moduleId
  store.getModuleConfig(moduleId)
}

function onSaveConfig(moduleId: string, values: Record<string, unknown>) {
  store.saveModuleConfig(moduleId, values)
}

function loadRegistry() {
  showRegistry.value = true
  store.getRegistry()
}

// Split modules into regular (third-party) and built-in
const regularModules = computed(() => store.modules.filter(m => !m.isBuiltIn))
const builtInModules = computed(() => store.modules.filter(m => m.isBuiltIn))

const filteredRegistry = computed(() => {
  if (!searchQuery.value) return store.registryModules
  const q = searchQuery.value.toLowerCase()
  return store.registryModules.filter(m =>
    m.displayName.toLowerCase().includes(q) ||
    m.author.toLowerCase().includes(q) ||
    (m.description || '').toLowerCase().includes(q)
  )
})

function stateStyle(status: string, enabled?: boolean) {
  if (enabled === false) return { label: 'Disabled',   bg: 'rgba(255,255,255,0.04)', border: 'rgba(255,255,255,0.08)', text: 'rgba(255,255,255,0.25)' }
  switch (status) {
    case 'Active':     return { label: 'Active',      bg: 'rgba(16,185,129,0.1)',   border: 'rgba(16,185,129,0.2)',   text: 'rgba(52,211,153,0.9)' }
    case 'Idle':       return { label: 'Idle',         bg: 'rgba(251,191,36,0.1)',   border: 'rgba(251,191,36,0.2)',   text: 'rgba(251,191,36,0.9)' }
    case 'InitFailed': return { label: 'Unavailable',  bg: 'rgba(244,63,94,0.08)',   border: 'rgba(244,63,94,0.2)',    text: 'rgba(251,113,133,0.9)' }
    default:           return { label: 'Inactive',     bg: 'rgba(255,255,255,0.04)', border: 'rgba(255,255,255,0.08)', text: 'rgba(255,255,255,0.3)' }
  }
}

function installState(m: typeof store.registryModules[0]) {
  if (m.installState === 'Installing') return { label: `${m.installProgress != null ? Math.round(m.installProgress*100)+'%' : '…'}`, installing: true }
  if (m.installState === 'Installed') return { label: 'Installed', installed: true }
  if (m.installState === 'UpdateAvailable') return { label: 'Update', update: true }
  return { label: 'Install', notInstalled: true }
}
</script>

<template>
  <div class="flex flex-col h-full p-8 gap-5 overflow-hidden">

    <!-- Header -->
    <div class="flex items-center justify-between shrink-0">
      <div class="space-y-1">
        <h1 class="text-2xl font-black tracking-tighter text-white/90 uppercase">
          {{ showRegistry ? 'Registry' : 'Modules' }}
        </h1>
        <p class="text-[10px] font-bold tracking-[0.3em] uppercase" style="color:rgba(255,255,255,0.2)">
          {{ showRegistry
            ? (store.registryLoading ? 'Loading…' : filteredRegistry.length + ' available')
            : regularModules.length + ' installed · ' + builtInModules.length + ' built-in' }}
        </p>
      </div>
      <div class="flex items-center gap-3">
        <button v-if="showRegistry" @click="store.getRegistry()"
                class="p-2.5 rounded-xl transition-all"
                style="background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.4)"
                :class="store.registryLoading ? 'animate-spin' : ''">
          <RefreshCw :size="14" />
        </button>
        <button @click="showRegistry ? (showRegistry=false) : loadRegistry()"
                class="px-5 py-2.5 rounded-xl text-sm font-medium tracking-wide transition-all"
                :style="showRegistry
                  ? 'background:rgba(255,255,255,0.05); border:1px solid rgba(255,255,255,0.1); color:rgba(255,255,255,0.6)'
                  : 'background:rgba(59,130,246,0.12); border:1px solid rgba(59,130,246,0.25); color:rgba(96,165,250,0.9)'">
          {{ showRegistry ? '← Installed' : 'Browse Registry' }}
        </button>
      </div>
    </div>

    <!-- Registry search bar -->
    <div v-if="showRegistry" class="relative shrink-0">
      <Search :size="13" class="absolute left-3.5 top-1/2 -translate-y-1/2" style="color:rgba(255,255,255,0.25)" />
      <input v-model="searchQuery" type="text" placeholder="Search registry…"
             class="w-full pl-10 pr-4 py-2.5 rounded-xl text-sm" />
    </div>

    <!-- Installed + Built-in modules -->
    <div v-if="!showRegistry" class="flex-1 overflow-y-auto space-y-5 min-h-0">

      <!-- Installed (third-party) modules -->
      <div class="space-y-3">
        <p v-if="builtInModules.length > 0" class="text-[10px] font-bold tracking-[0.3em] uppercase"
           style="color:rgba(255,255,255,0.2)">Installed</p>

        <div v-for="mod in regularModules" :key="mod.id"
             class="rounded-3xl p-5 transition-all"
             style="background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.05)">
          <div class="flex items-center gap-5">
            <div class="w-11 h-11 rounded-xl flex items-center justify-center shrink-0"
                 style="background:rgba(255,255,255,0.03); border:1px solid rgba(255,255,255,0.06)">
              <Puzzle :size="18" style="color:rgba(255,255,255,0.25)" />
            </div>
            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2.5 flex-wrap">
                <span class="text-base font-semibold text-white/80 truncate">{{ mod.name }}</span>
                <span class="px-2.5 py-0.5 rounded-lg text-[10px] font-bold tracking-wide uppercase"
                      :style="`background:${stateStyle(mod.status).bg}; border:1px solid ${stateStyle(mod.status).border}; color:${stateStyle(mod.status).text}`">
                  {{ stateStyle(mod.status).label }}
                </span>
                <span v-if="mod.crashCount > 0"
                      class="px-2 py-0.5 rounded-lg text-[10px]"
                      style="background:rgba(239,68,68,0.1); border:1px solid rgba(239,68,68,0.2); color:rgba(239,68,68,0.7)">
                  {{ mod.crashCount }} crash{{ mod.crashCount > 1 ? 'es' : '' }}
                </span>
              </div>
              <div class="flex items-center gap-4 mt-1.5">
                <span v-if="mod.supportsEye" class="flex items-center gap-1 text-[10px]" style="color:rgba(96,165,250,0.6)">
                  <Eye :size="10" /> Eye
                </span>
                <span v-if="mod.supportsExpression" class="flex items-center gap-1 text-[10px]" style="color:rgba(192,132,252,0.6)">
                  <Smile :size="10" /> Expression
                </span>
                <span class="text-[10px] truncate" style="color:rgba(255,255,255,0.2)">{{ mod.path }}</span>
              </div>
            </div>
            <!-- Configure button -->
            <button v-if="mod.hasConfig"
                    @click="toggleConfig(mod.id)"
                    class="shrink-0 p-2 rounded-xl transition-all"
                    :style="showConfigFor === mod.id
                      ? 'background:rgba(99,102,241,0.15); border:1px solid rgba(99,102,241,0.3); color:rgba(129,140,248,0.9)'
                      : 'background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.35)'"
                    title="Configure module">
              <Settings2 :size="14" />
            </button>
          </div>
          <!-- Last message for regular modules (click to expand recent log) -->
          <div v-if="mod.lastMessage"
               class="mt-3 rounded-xl overflow-hidden"
               style="background:rgba(255,255,255,0.02)">
            <button @click="toggleLog(mod.id)"
                    class="w-full px-3 py-2 flex items-center justify-between text-left text-[10px] font-mono break-words"
                    style="color:rgba(255,255,255,0.3)"
                    :title="(mod.recentMessages && mod.recentMessages.length > 1) ? 'Click to expand log' : ''">
              <span class="flex-1 break-words">{{ mod.lastMessage }}</span>
              <span v-if="mod.recentMessages && mod.recentMessages.length > 1"
                    class="ml-2 shrink-0 text-[9px]" style="color:rgba(255,255,255,0.25)">
                {{ showLogFor === mod.id ? '▴' : `+${mod.recentMessages.length - 1}` }}
              </span>
            </button>
            <div v-if="showLogFor === mod.id && mod.recentMessages && mod.recentMessages.length > 0"
                 class="px-3 py-2 border-t text-[10px] font-mono space-y-0.5 max-h-48 overflow-y-auto"
                 style="border-color:rgba(255,255,255,0.04); color:rgba(255,255,255,0.4)">
              <div v-for="(line, i) in mod.recentMessages" :key="i" class="break-words">{{ line }}</div>
            </div>
          </div>

          <!-- Expandable config panel -->
          <div v-if="showConfigFor === mod.id" class="mt-1">
            <div class="border-t mt-4" style="border-color:rgba(255,255,255,0.06)" />
            <ModuleConfigPanel
              v-if="store.moduleConfigs[mod.id]"
              :config="store.moduleConfigs[mod.id]"
              @save="onSaveConfig" />
            <div v-else class="mt-4 flex items-center justify-center py-4">
              <RefreshCw :size="14" class="animate-spin" style="color:rgba(255,255,255,0.2)" />
            </div>
          </div>

          <!-- Remove button for third-party modules -->
          <div v-if="mod.packageId" class="mt-3 flex justify-end">
            <button @click="store.uninstallModule(mod.packageId!)"
                    class="flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-medium transition-all"
                    style="background:rgba(239,68,68,0.06); border:1px solid rgba(239,68,68,0.12); color:rgba(239,68,68,0.5)"
                    onmouseenter="this.style.background='rgba(239,68,68,0.1)'; this.style.borderColor='rgba(239,68,68,0.2)'; this.style.color='rgba(239,68,68,0.8)'"
                    onmouseleave="this.style.background='rgba(239,68,68,0.06)'; this.style.borderColor='rgba(239,68,68,0.12)'; this.style.color='rgba(239,68,68,0.5)'">
              <Trash2 :size="11" /> Remove
            </button>
          </div>
        </div>

        <div v-if="regularModules.length === 0"
             class="flex flex-col items-center justify-center gap-5 py-10">
          <div class="w-14 h-14 rounded-2xl flex items-center justify-center"
               style="background:rgba(255,255,255,0.03); border:1px solid rgba(255,255,255,0.06)">
            <Puzzle :size="24" style="color:rgba(255,255,255,0.15)" />
          </div>
          <div class="text-center">
            <p class="text-base font-medium text-white/40">No modules installed</p>
            <p class="text-xs mt-1" style="color:rgba(255,255,255,0.2)">Browse the registry to install a tracking module</p>
          </div>
          <button @click="loadRegistry(); showRegistry=true"
                  class="px-5 py-2.5 rounded-xl text-sm font-medium tracking-wide"
                  style="background:rgba(59,130,246,0.12); border:1px solid rgba(59,130,246,0.25); color:rgba(96,165,250,0.9)">
            Browse Registry →
          </button>
        </div>
      </div>

      <!-- Built-in modules section -->
      <div v-if="builtInModules.length > 0" class="space-y-3">
        <p class="text-[10px] font-bold tracking-[0.3em] uppercase" style="color:rgba(255,255,255,0.2)">
          Built-in
        </p>

        <div v-for="mod in builtInModules" :key="mod.id"
             class="rounded-3xl p-5 transition-all"
             :style="mod.enabled === false
               ? 'background:rgba(255,255,255,0.01); border:1px solid rgba(255,255,255,0.04)'
               : 'background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.05)'">
          <div class="flex items-center gap-5">
            <!-- Icon -->
            <div class="w-11 h-11 rounded-xl flex items-center justify-center shrink-0"
                 style="background:rgba(99,102,241,0.08); border:1px solid rgba(99,102,241,0.15)">
              <Cpu :size="18" style="color:rgba(129,140,248,0.6)" />
            </div>

            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2.5 flex-wrap">
                <span class="text-base font-semibold text-white/80 truncate">{{ mod.name }}</span>
                <!-- Built-in badge -->
                <span class="px-2 py-0.5 rounded-lg text-[10px] font-bold tracking-wide uppercase"
                      style="background:rgba(99,102,241,0.1); border:1px solid rgba(99,102,241,0.2); color:rgba(129,140,248,0.7)">
                  Built-in
                </span>
                <!-- Status badge (only when enabled) -->
                <span v-if="mod.enabled !== false"
                      class="px-2.5 py-0.5 rounded-lg text-[10px] font-bold tracking-wide uppercase"
                      :style="`background:${stateStyle(mod.status).bg}; border:1px solid ${stateStyle(mod.status).border}; color:${stateStyle(mod.status).text}`">
                  {{ stateStyle(mod.status).label }}
                </span>
                <span v-if="mod.crashCount > 0"
                      class="px-2 py-0.5 rounded-lg text-[10px]"
                      style="background:rgba(239,68,68,0.1); border:1px solid rgba(239,68,68,0.2); color:rgba(239,68,68,0.7)">
                  {{ mod.crashCount }} crash{{ mod.crashCount > 1 ? 'es' : '' }}
                </span>
              </div>
              <div class="flex items-center gap-4 mt-1.5">
                <span v-if="mod.supportsEye && mod.enabled !== false" class="flex items-center gap-1 text-[10px]" style="color:rgba(96,165,250,0.6)">
                  <Eye :size="10" /> Eye
                </span>
                <span v-if="mod.supportsExpression && mod.enabled !== false" class="flex items-center gap-1 text-[10px]" style="color:rgba(192,132,252,0.6)">
                  <Smile :size="10" /> Expression
                </span>
                <span class="text-[10px]" style="color:rgba(255,255,255,0.2)">{{ mod.path }}</span>
              </div>
            </div>

            <!-- Configure button (only when enabled and has config) -->
            <button v-if="mod.hasConfig && mod.enabled !== false"
                    @click="toggleConfig(mod.id)"
                    class="shrink-0 p-2 rounded-xl transition-all"
                    :style="showConfigFor === mod.id
                      ? 'background:rgba(99,102,241,0.15); border:1px solid rgba(99,102,241,0.3); color:rgba(129,140,248,0.9)'
                      : 'background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.35)'"
                    title="Configure module">
              <Settings2 :size="14" />
            </button>

            <!-- Enable / Disable toggle -->
            <button @click="mod.enabled === false ? store.enableModule(mod.id) : store.disableModule(mod.id)"
                    class="relative shrink-0 w-12 h-6 rounded-full transition-all duration-300 focus:outline-none"
                    :style="mod.enabled !== false
                      ? 'background:rgba(99,102,241,0.5); box-shadow:0 0 12px rgba(99,102,241,0.3)'
                      : 'background:rgba(255,255,255,0.08)'"
                    :title="mod.enabled === false ? 'Enable module' : 'Disable module'">
              <span class="absolute top-0.5 left-0.5 w-5 h-5 rounded-full transition-all duration-300"
                    :style="mod.enabled !== false
                      ? 'background:rgba(255,255,255,0.95); transform:translateX(24px)'
                      : 'background:rgba(255,255,255,0.7); transform:translateX(0)'" />
            </button>
          </div>

          <!-- Last message when enabled (click to expand recent log) -->
          <div v-if="mod.enabled !== false && mod.lastMessage"
               class="mt-3 rounded-xl overflow-hidden"
               style="background:rgba(255,255,255,0.02)">
            <button @click="toggleLog(mod.id)"
                    class="w-full px-3 py-2 flex items-center justify-between text-left text-[10px] font-mono break-words"
                    style="color:rgba(255,255,255,0.3)"
                    :title="(mod.recentMessages && mod.recentMessages.length > 1) ? 'Click to expand log' : ''">
              <span class="flex-1 break-words">{{ mod.lastMessage }}</span>
              <span v-if="mod.recentMessages && mod.recentMessages.length > 1"
                    class="ml-2 shrink-0 text-[9px]" style="color:rgba(255,255,255,0.25)">
                {{ showLogFor === mod.id ? '▴' : `+${mod.recentMessages.length - 1}` }}
              </span>
            </button>
            <div v-if="showLogFor === mod.id && mod.recentMessages && mod.recentMessages.length > 0"
                 class="px-3 py-2 border-t text-[10px] font-mono space-y-0.5 max-h-48 overflow-y-auto"
                 style="border-color:rgba(255,255,255,0.04); color:rgba(255,255,255,0.4)">
              <div v-for="(line, i) in mod.recentMessages" :key="i" class="break-words">{{ line }}</div>
            </div>
          </div>

          <!-- Disabled description -->
          <p v-if="mod.enabled === false"
             class="mt-2 text-xs" style="color:rgba(255,255,255,0.25)">
            Disabled — toggle to enable on next startup cycle
          </p>

          <!-- Expandable config panel -->
          <div v-if="showConfigFor === mod.id && mod.enabled !== false" class="mt-1">
            <div class="border-t mt-4" style="border-color:rgba(255,255,255,0.06)" />
            <ModuleConfigPanel
              v-if="store.moduleConfigs[mod.id]"
              :config="store.moduleConfigs[mod.id]"
              @save="onSaveConfig" />
            <div v-else class="mt-4 flex items-center justify-center py-4">
              <RefreshCw :size="14" class="animate-spin" style="color:rgba(255,255,255,0.2)" />
            </div>
          </div>
        </div>
      </div>

    </div>

    <!-- Registry modules -->
    <div v-else class="flex-1 overflow-y-auto min-h-0">
      <!-- Loading skeleton -->
      <div v-if="store.registryLoading" class="space-y-3">
        <div v-for="i in 5" :key="i" class="rounded-3xl p-5 h-24 animate-pulse"
             style="background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.04)" />
      </div>

      <div v-else class="space-y-3">
        <div v-for="mod in filteredRegistry" :key="mod.packageId"
             class="rounded-3xl p-5 transition-all"
             style="background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.05)">
          <div class="flex items-start gap-5">
            <!-- Icon -->
            <div class="w-11 h-11 rounded-xl flex items-center justify-center shrink-0"
                 style="background:rgba(255,255,255,0.03); border:1px solid rgba(255,255,255,0.06)">
              <Puzzle :size="18" style="color:rgba(255,255,255,0.25)" />
            </div>

            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2.5 flex-wrap">
                <span class="text-base font-semibold text-white/80">{{ mod.displayName }}</span>
                <span class="text-[10px]" style="color:rgba(255,255,255,0.25)">v{{ mod.version }}</span>
                <span v-if="mod.usesEye" class="flex items-center gap-1 text-[10px]" style="color:rgba(96,165,250,0.5)">
                  <Eye :size="9" /> Eye
                </span>
                <span v-if="mod.usesExpression" class="flex items-center gap-1 text-[10px]" style="color:rgba(192,132,252,0.5)">
                  <Smile :size="9" /> Expr
                </span>
              </div>
              <p class="text-[10px] mt-1" style="color:rgba(255,255,255,0.3)">by {{ mod.author }}</p>
              <p class="text-xs mt-1.5 text-white/40 line-clamp-2">{{ mod.description }}</p>
            </div>

            <!-- Install/status button -->
            <div class="shrink-0 flex flex-col items-end gap-2">
              <!-- Installed -->
              <template v-if="installState(mod).installed">
                <div class="flex items-center gap-2 text-xs" style="color:rgba(52,211,153,0.7)">
                  <CheckCircle2 :size="14" /> Installed
                </div>
                <button @click="store.uninstallModule(mod.packageId)"
                        class="px-3 py-1.5 rounded-xl text-xs font-medium transition-all flex items-center gap-1.5"
                        style="background:rgba(239,68,68,0.08); border:1px solid rgba(239,68,68,0.15); color:rgba(239,68,68,0.6)">
                  <Trash2 :size="11" /> Remove
                </button>
              </template>

              <!-- Update available -->
              <template v-else-if="installState(mod).update">
                <span class="text-[10px]" style="color:rgba(251,191,36,0.7)">v{{ mod.installedVersion }} installed</span>
                <button @click="store.installModule(mod.packageId)"
                        class="px-3 py-1.5 rounded-xl text-xs font-medium transition-all flex items-center gap-1.5"
                        style="background:rgba(251,191,36,0.1); border:1px solid rgba(251,191,36,0.25); color:rgba(251,191,36,0.8)">
                  <Download :size="11" /> Update
                </button>
              </template>

              <!-- Installing -->
              <template v-else-if="installState(mod).installing">
                <div class="px-3 py-1.5 rounded-xl text-xs font-medium flex items-center gap-2"
                     style="background:rgba(59,130,246,0.1); border:1px solid rgba(59,130,246,0.2); color:rgba(96,165,250,0.8)">
                  <RefreshCw :size="11" class="animate-spin" />
                  {{ installState(mod).label }}
                </div>
              </template>

              <!-- Not installed -->
              <template v-else>
                <button @click="store.installModule(mod.packageId)"
                        class="px-4 py-2 rounded-xl text-xs font-medium transition-all flex items-center gap-2"
                        style="background:rgba(59,130,246,0.12); border:1px solid rgba(59,130,246,0.25); color:rgba(96,165,250,0.9)">
                  <Download :size="12" /> Install
                </button>
              </template>
            </div>
          </div>

          <!-- Install progress bar -->
          <div v-if="mod.installState === 'Installing' && mod.installProgress != null"
               class="mt-4 h-1 rounded-full overflow-hidden"
               style="background:rgba(255,255,255,0.05)">
            <div class="h-full rounded-full transition-all"
                 style="background:rgba(96,165,250,0.7)"
                 :style="{ width: (mod.installProgress * 100) + '%' }" />
          </div>
        </div>

        <div v-if="filteredRegistry.length === 0 && !store.registryLoading"
             class="py-16 text-center">
          <AlertCircle :size="28" class="mx-auto mb-4" style="color:rgba(255,255,255,0.12)" />
          <p class="text-sm text-white/30">
            {{ searchQuery ? 'No modules match your search' : 'Registry unavailable — check your connection' }}
          </p>
        </div>
      </div>
    </div>
  </div>
</template>
