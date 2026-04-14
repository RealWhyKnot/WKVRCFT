<script setup lang="ts">
import { ref, computed, nextTick, watch } from 'vue'
import { useAppStore } from '@/stores/appStore'
import { Search, Download, Trash2 } from 'lucide-vue-next'

const store = useAppStore()
const searchQuery  = ref('')
const levelFilter  = ref('all')
const autoScroll   = ref(true)
const logContainer = ref<HTMLElement>()

const filteredLogs = computed(() => {
  let logs = store.visibleLogs
  if (levelFilter.value !== 'all')
    logs = logs.filter(l => l.level?.toLowerCase() === levelFilter.value)
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    logs = logs.filter(l => l.message?.toLowerCase().includes(q) || l.source?.toLowerCase().includes(q))
  }
  return logs
})

watch(() => store.logs.length, () => {
  if (autoScroll.value) nextTick(() => {
    if (logContainer.value) logContainer.value.scrollTop = logContainer.value.scrollHeight
  })
})

function exportLogs() {
  const text = filteredLogs.value.map(l =>
    `[${l.timestamp}] [${l.level?.toUpperCase().padEnd(5)}] [${l.source}] ${l.message}`
  ).join('\n')
  const a = Object.assign(document.createElement('a'), {
    href: URL.createObjectURL(new Blob([text], { type: 'text/plain' })),
    download: 'vrcft_' + new Date().toISOString().slice(0,10) + '.log'
  })
  a.click()
  URL.revokeObjectURL(a.href)
}

function levelStyle(level: string): string {
  switch (level?.toLowerCase()) {
    case 'error':       return 'color:rgba(239,68,68,0.9)'
    case 'warning':     return 'color:rgba(251,191,36,0.9)'
    case 'information': return 'color:rgba(52,211,153,0.8)'
    case 'debug':       return 'color:rgba(96,165,250,0.5)'
    default:            return 'color:rgba(255,255,255,0.3)'
  }
}

function levelTag(level: string): string {
  switch (level?.toLowerCase()) {
    case 'error':       return 'ERR'
    case 'warning':     return 'WARN'
    case 'information': return 'INFO'
    case 'debug':       return 'DBG'
    default:            return '???'
  }
}
</script>

<template>
  <div class="flex flex-col h-full p-8 gap-4 overflow-hidden">

    <!-- Header + Toolbar -->
    <div class="flex items-center gap-4 shrink-0">
      <div class="shrink-0">
        <h1 class="text-2xl font-black tracking-tighter text-white/90 uppercase">Output</h1>
        <p class="text-[10px] font-bold tracking-[0.3em] uppercase" style="color:rgba(255,255,255,0.2)">
          {{ filteredLogs.length }} / {{ store.visibleLogs.length }} entries
        </p>
      </div>

      <!-- Search -->
      <div class="flex-1 relative">
        <Search :size="13" class="absolute left-3.5 top-1/2 -translate-y-1/2" style="color:rgba(255,255,255,0.25)" />
        <input v-model="searchQuery" type="text" placeholder="Search logs…"
               class="w-full pl-10 pr-4 py-2.5 rounded-xl text-sm" />
      </div>

      <!-- Level filter -->
      <select v-model="levelFilter"
              class="px-4 py-2.5 rounded-xl text-sm">
        <option value="all">All levels</option>
        <option value="error">Error</option>
        <option value="warning">Warning</option>
        <option value="information">Info</option>
        <option value="debug">Debug</option>
      </select>

      <!-- Auto-scroll -->
      <button @click="autoScroll = !autoScroll"
              class="px-4 py-2.5 rounded-xl text-xs font-medium tracking-wide transition-all"
              :style="autoScroll
                ? 'background:rgba(59,130,246,0.12); border:1px solid rgba(59,130,246,0.25); color:rgba(96,165,250,0.9)'
                : 'background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.35)'">
        Auto
      </button>

      <!-- Export -->
      <button @click="exportLogs" title="Export logs"
              class="p-2.5 rounded-xl transition-all"
              style="background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.35)"
              onmouseenter="this.style.color='rgba(255,255,255,0.7)'"
              onmouseleave="this.style.color='rgba(255,255,255,0.35)'">
        <Download :size="14" />
      </button>

      <!-- Clear -->
      <button @click="store.logs.splice(0)" title="Clear logs"
              class="p-2.5 rounded-xl transition-all"
              style="background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.35)"
              onmouseenter="this.style.color='rgba(239,68,68,0.7)'; this.style.borderColor='rgba(239,68,68,0.2)'"
              onmouseleave="this.style.color='rgba(255,255,255,0.35)'; this.style.borderColor='rgba(255,255,255,0.08)'">
        <Trash2 :size="14" />
      </button>
    </div>

    <!-- Log container -->
    <div ref="logContainer"
         class="flex-1 rounded-3xl overflow-y-auto font-mono text-xs min-h-0"
         style="background:rgba(0,0,0,0.3); border:1px solid rgba(255,255,255,0.05); padding:16px 12px">
      <div v-for="(log, i) in filteredLogs" :key="i"
           class="flex gap-4 py-1 px-3 rounded-lg transition-colors hover:bg-white/[0.02] items-baseline"
           :class="i % 2 === 0 ? '' : 'bg-white/[0.01]'">
        <span class="shrink-0 w-16 text-[10px] tabular-nums" style="color:rgba(255,255,255,0.2)">
          {{ new Date(log.timestamp).toLocaleTimeString('en', { hour12: false }) }}
        </span>
        <span class="shrink-0 w-11 text-[10px] font-bold uppercase" :style="levelStyle(log.level)">
          {{ levelTag(log.level) }}
        </span>
        <span class="shrink-0 w-32 truncate text-[10px]" style="color:rgba(96,165,250,0.5)">{{ log.source }}</span>
        <span class="break-all leading-relaxed" style="color:rgba(255,255,255,0.65)">{{ log.message }}</span>
      </div>
      <div v-if="filteredLogs.length === 0"
           class="h-full flex items-center justify-center">
        <p class="text-xs tracking-widest uppercase" style="color:rgba(255,255,255,0.1)">
          {{ store.logs.length === 0 ? 'Waiting for log entries…' : 'No matches' }}
        </p>
      </div>
    </div>
  </div>
</template>
