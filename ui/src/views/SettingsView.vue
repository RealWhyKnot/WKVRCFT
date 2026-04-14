<script setup lang="ts">
import { useAppStore } from '@/stores/appStore'
import { Wifi, Bug, FolderOpen, ExternalLink, RotateCcw } from 'lucide-vue-next'

const store = useAppStore()

function openUrl(url: string) {
  store.send('OPEN_BROWSER', { url })
}

function resetOscDefaults() {
  store.oscConfig.ip = '127.0.0.1'
  store.oscConfig.sendPort = 9000
  store.oscConfig.recvPort = 9001
  store.saveOscConfig()
}
</script>

<template>
  <div class="flex flex-col h-full overflow-hidden">
  <div class="flex-1 overflow-y-auto p-8 space-y-6">

    <div class="space-y-1">
      <h1 class="text-2xl font-black uppercase tracking-tighter text-white/90">Settings</h1>
      <p class="text-[10px] font-bold tracking-[0.3em] uppercase" style="color:rgba(255,255,255,0.2)">Configuration</p>
    </div>

    <!-- OSC Configuration -->
    <div class="rounded-3xl p-6 space-y-5" style="background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.05)">
      <div class="flex items-center gap-2.5">
        <Wifi :size="15" style="color:rgba(96,165,250,0.7)" />
        <span class="text-xs font-bold tracking-[0.15em] uppercase" style="color:rgba(255,255,255,0.4)">OSC Configuration</span>
        <div class="ml-auto flex items-center gap-2 text-[10px]"
             :style="store.oscStatus.connected
               ? 'color:rgba(52,211,153,0.8)'
               : 'color:rgba(255,255,255,0.25)'">
          <div class="w-2 h-2 rounded-full"
               :class="store.oscStatus.connected ? 'bg-emerald-400 animate-pulse' : 'bg-white/20'" />
          {{ store.oscStatus.connected ? 'Sending' : 'Idle' }}
        </div>
      </div>

      <div class="grid grid-cols-3 gap-4">
        <div class="space-y-2">
          <label class="text-[10px] tracking-[0.15em] uppercase block font-medium" style="color:rgba(255,255,255,0.3)">Target IP</label>
          <input v-model="store.oscConfig.ip"
                 @change="store.saveOscConfig()"
                 type="text"
                 class="w-full px-4 py-2.5 rounded-xl text-sm" />
        </div>
        <div class="space-y-2">
          <label class="text-[10px] tracking-[0.15em] uppercase block font-medium" style="color:rgba(255,255,255,0.3)">Send Port</label>
          <input v-model.number="store.oscConfig.sendPort"
                 @change="store.saveOscConfig()"
                 type="number"
                 class="w-full px-4 py-2.5 rounded-xl text-sm" />
        </div>
        <div class="space-y-2">
          <label class="text-[10px] tracking-[0.15em] uppercase block font-medium" style="color:rgba(255,255,255,0.3)">Receive Port</label>
          <input v-model.number="store.oscConfig.recvPort"
                 @change="store.saveOscConfig()"
                 type="number"
                 class="w-full px-4 py-2.5 rounded-xl text-sm" />
        </div>
      </div>

      <div class="flex items-center justify-between">
        <p class="text-[10px]" style="color:rgba(255,255,255,0.2)">
          VRChat default: send to 127.0.0.1:9000, receive on 9001
        </p>
        <button @click="resetOscDefaults"
                class="flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-[10px] font-medium transition-all"
                style="background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.35)"
                onmouseenter="this.style.color='rgba(255,255,255,0.6)'"
                onmouseleave="this.style.color='rgba(255,255,255,0.35)'">
          <RotateCcw :size="10" /> Reset Defaults
        </button>
      </div>
    </div>

    <!-- Application -->
    <div class="rounded-3xl p-6 space-y-5" style="background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.05)">
      <div class="flex items-center gap-2.5">
        <Bug :size="15" style="color:rgba(192,132,252,0.7)" />
        <span class="text-xs font-bold tracking-[0.15em] uppercase" style="color:rgba(255,255,255,0.4)">Application</span>
      </div>

      <!-- Debug mode toggle -->
      <div class="flex items-center justify-between py-2">
        <div class="space-y-1">
          <span class="text-sm text-white/70 font-medium">Debug Mode</span>
          <p class="text-[10px]" style="color:rgba(255,255,255,0.25)">
            Shows Debug-level log entries in the Output page
          </p>
        </div>
        <button
          @click="store.config.debugMode = !store.config.debugMode; store.saveConfig()"
          class="w-12 h-6 rounded-full relative transition-all duration-300 shrink-0"
          :style="store.config.debugMode
            ? 'background:rgba(59,130,246,0.6); box-shadow:0 0 12px rgba(59,130,246,0.3)'
            : 'background:rgba(255,255,255,0.08)'">
          <div class="absolute top-1 w-4 h-4 bg-white rounded-full transition-all duration-300 shadow-sm"
               :class="store.config.debugMode ? 'left-7' : 'left-1'" />
        </button>
      </div>
    </div>

    <!-- Data & Directories -->
    <div class="rounded-3xl p-6 space-y-4" style="background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.05)">
      <div class="flex items-center gap-2.5">
        <FolderOpen :size="15" style="color:rgba(251,191,36,0.7)" />
        <span class="text-xs font-bold tracking-[0.15em] uppercase" style="color:rgba(255,255,255,0.4)">Data &amp; Directories</span>
      </div>

      <div class="space-y-2">
        <div class="flex items-center justify-between py-3 px-4 rounded-2xl" style="background:rgba(255,255,255,0.02)">
          <div>
            <p class="text-sm text-white/60">Modules directory</p>
            <p class="text-[10px] mt-0.5 font-mono" style="color:rgba(255,255,255,0.2)">%LOCALAPPDATA%\VRCFaceTracking\modules</p>
          </div>
          <button @click="store.send('OPEN_MODULES_DIR')"
                  class="px-3 py-2 rounded-xl text-xs font-medium transition-all"
                  style="background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.4)"
                  onmouseenter="this.style.color='rgba(255,255,255,0.7)'"
                  onmouseleave="this.style.color='rgba(255,255,255,0.4)'">
            Open
          </button>
        </div>
        <div class="flex items-center justify-between py-3 px-4 rounded-2xl" style="background:rgba(255,255,255,0.02)">
          <div>
            <p class="text-sm text-white/60">Logs directory</p>
            <p class="text-[10px] mt-0.5 font-mono" style="color:rgba(255,255,255,0.2)">%LOCALAPPDATA%\VRCFaceTracking\logs</p>
          </div>
          <button @click="store.send('OPEN_LOGS_DIR')"
                  class="px-3 py-2 rounded-xl text-xs font-medium transition-all"
                  style="background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.4)"
                  onmouseenter="this.style.color='rgba(255,255,255,0.7)'"
                  onmouseleave="this.style.color='rgba(255,255,255,0.4)'">
            Open
          </button>
        </div>
      </div>
    </div>

    <!-- About -->
    <div class="rounded-3xl p-6 space-y-4" style="background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.05)">
      <div class="flex items-center justify-between">
        <div>
          <p class="text-lg font-bold text-white/80">VRCFaceTracking</p>
          <p class="text-xs mt-0.5" style="color:rgba(255,255,255,0.25)">v{{ store.version }}</p>
        </div>
      </div>
      <div class="flex gap-2">
        <button @click="openUrl('https://github.com/benaclejames/VRCFaceTracking')"
                class="flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-medium transition-all"
                style="background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.4)"
                onmouseenter="this.style.color='rgba(255,255,255,0.7)'"
                onmouseleave="this.style.color='rgba(255,255,255,0.4)'">
          <ExternalLink :size="12" /> GitHub
        </button>
        <button @click="openUrl('https://docs.vrcft.io')"
                class="flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-medium transition-all"
                style="background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.4)"
                onmouseenter="this.style.color='rgba(255,255,255,0.7)'"
                onmouseleave="this.style.color='rgba(255,255,255,0.4)'">
          <ExternalLink :size="12" /> Docs
        </button>
        <button @click="openUrl('https://discord.gg/vrcft')"
                class="flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-medium transition-all"
                style="background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.4)"
                onmouseenter="this.style.color='rgba(255,255,255,0.7)'"
                onmouseleave="this.style.color='rgba(255,255,255,0.4)'">
          <ExternalLink :size="12" /> Discord
        </button>
      </div>
    </div>
  </div>
  </div>
</template>
