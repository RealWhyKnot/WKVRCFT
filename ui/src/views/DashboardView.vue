<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue'
import { useAppStore } from '@/stores/appStore'
import { Eye, Smile, Move3d, Puzzle, RotateCcw, Wifi, Activity, ArrowRight } from 'lucide-vue-next'

const store = useAppStore()

const eyeModule = computed(() => store.modules.find(m => m.supportsEye && m.status === 'Active'))
const activeExprCount = computed(() => store.trackingData?.shapes.filter(v => v > 0.01).length ?? 0)

// Uptime counter
const startTime = Date.now()
const uptime = ref('0:00')
let uptimeInterval: number
onMounted(() => {
  uptimeInterval = window.setInterval(() => {
    const s = Math.floor((Date.now() - startTime) / 1000)
    const m = Math.floor(s / 60)
    const h = Math.floor(m / 60)
    uptime.value = h > 0 ? `${h}:${String(m % 60).padStart(2,'0')}:${String(s % 60).padStart(2,'0')}` : `${m}:${String(s % 60).padStart(2,'0')}`
  }, 1000)
})
onUnmounted(() => clearInterval(uptimeInterval))

const expressionNames = [
  'JawOpen','MouthClose','LipFunnelUL','LipFunnelUR','LipFunnelLL','LipFunnelLR',
  'LipPuckerUL','LipPuckerUR','MouthSmileL','MouthSmileR','CheekPuffL','CheekPuffR',
  'BrowInnerUp','BrowOuterUpL','BrowOuterUpR','NoseSneerL'
]

function statusColor(status: string) {
  switch (status) {
    case 'Active':     return { dot: 'bg-emerald-400 animate-pulse', glow: 'box-shadow:0 0 8px rgba(52,211,153,0.6)', text: 'rgba(52,211,153,0.9)', bg: 'rgba(16,185,129,0.06)', border: 'rgba(16,185,129,0.15)' }
    case 'Idle':       return { dot: 'bg-amber-400', glow: '', text: 'rgba(251,191,36,0.9)', bg: 'rgba(251,191,36,0.06)', border: 'rgba(251,191,36,0.15)' }
    case 'InitFailed': return { dot: 'bg-rose-400', glow: '', text: 'rgba(251,113,133,0.9)', bg: 'rgba(244,63,94,0.06)', border: 'rgba(244,63,94,0.15)' }
    default:           return { dot: 'bg-white/20', glow: '', text: 'rgba(255,255,255,0.3)', bg: 'rgba(255,255,255,0.02)', border: 'rgba(255,255,255,0.05)' }
  }
}
</script>

<template>
  <div class="flex-1 overflow-y-auto p-8 space-y-6">

    <!-- Header -->
    <div class="space-y-1">
      <h1 class="text-2xl font-black uppercase tracking-tighter text-white/90">Dashboard</h1>
      <p class="text-[10px] font-bold tracking-[0.3em] uppercase" style="color:rgba(255,255,255,0.2)">
        {{ store.activeModules.length > 0 ? store.activeModules.length + ' module(s) active' : 'System idle' }}
      </p>
    </div>

    <!-- ═══════════════ MODULE STATUS CARDS ═══════════════ -->
    <div v-if="store.modules.length > 0" class="space-y-3">
      <p class="text-[10px] font-bold tracking-[0.2em] uppercase" style="color:rgba(255,255,255,0.25)">Loaded Modules</p>
      <div v-for="mod in store.modules" :key="mod.id"
           class="rounded-3xl p-6 transition-all"
           :style="`background:${statusColor(mod.status).bg}; border:1px solid ${statusColor(mod.status).border}`">
        <div class="flex items-start gap-5">
          <!-- Status indicator -->
          <div class="w-12 h-12 rounded-2xl flex items-center justify-center shrink-0"
               style="background:rgba(255,255,255,0.03); border:1px solid rgba(255,255,255,0.06)">
            <div class="w-3.5 h-3.5 rounded-full" :class="statusColor(mod.status).dot" :style="statusColor(mod.status).glow" />
          </div>

          <!-- Module info -->
          <div class="flex-1 min-w-0">
            <div class="flex items-center gap-3 flex-wrap">
              <span class="text-lg font-bold text-white/85">{{ mod.name }}</span>
              <span class="px-2.5 py-0.5 rounded-lg text-[10px] font-bold tracking-wide uppercase"
                    :style="`color:${statusColor(mod.status).text}; background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.06)`">
                {{ mod.status }}
              </span>
              <span v-if="mod.crashCount > 0"
                    class="px-2 py-0.5 rounded-lg text-[10px] font-medium"
                    style="background:rgba(239,68,68,0.08); border:1px solid rgba(239,68,68,0.15); color:rgba(239,68,68,0.7)">
                {{ mod.crashCount }} crash{{ mod.crashCount > 1 ? 'es' : '' }}
              </span>
            </div>

            <!-- Capability badges -->
            <div class="flex items-center gap-4 mt-2">
              <span v-if="mod.supportsEye" class="flex items-center gap-1.5 text-xs" style="color:rgba(96,165,250,0.6)">
                <Eye :size="12" /> Eye Tracking
              </span>
              <span v-if="mod.supportsExpression" class="flex items-center gap-1.5 text-xs" style="color:rgba(192,132,252,0.6)">
                <Smile :size="12" /> Expression
              </span>
              <span class="text-[10px] font-mono" style="color:rgba(255,255,255,0.15)">{{ mod.path }}</span>
            </div>

            <!-- Last message from module -->
            <p v-if="mod.lastMessage"
               class="mt-2.5 text-xs leading-relaxed px-3 py-2 rounded-xl"
               style="color:rgba(255,255,255,0.4); background:rgba(0,0,0,0.2)">
              {{ mod.lastMessage }}
            </p>
          </div>

          <!-- Restart button -->
          <button @click="store.restartModule(mod.id)"
                  class="px-3 py-2 rounded-xl text-xs font-medium transition-all flex items-center gap-1.5 shrink-0"
                  style="background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.3)"
                  onmouseenter="this.style.color='rgba(255,255,255,0.7)'"
                  onmouseleave="this.style.color='rgba(255,255,255,0.3)'">
            <RotateCcw :size="12" /> Restart
          </button>
        </div>
      </div>
    </div>

    <!-- No modules CTA -->
    <div v-else class="rounded-3xl p-8 flex items-center gap-6"
         style="background:rgba(59,130,246,0.04); border:1px solid rgba(59,130,246,0.12)">
      <div class="w-14 h-14 rounded-2xl flex items-center justify-center shrink-0"
           style="background:rgba(59,130,246,0.1); border:1px solid rgba(59,130,246,0.2)">
        <Puzzle :size="24" style="color:rgba(96,165,250,0.6)" />
      </div>
      <div class="flex-1">
        <p class="text-lg font-semibold text-white/70">No tracking modules loaded</p>
        <p class="text-xs mt-1" style="color:rgba(255,255,255,0.3)">
          Install a tracking module from the Registry to start sending face tracking data to VRChat.
        </p>
      </div>
      <router-link to="/modules"
                   class="px-5 py-2.5 rounded-xl text-sm font-medium flex items-center gap-2 shrink-0 transition-all"
                   style="background:rgba(59,130,246,0.12); border:1px solid rgba(59,130,246,0.25); color:rgba(96,165,250,0.9)">
        Browse Registry <ArrowRight :size="14" />
      </router-link>
    </div>

    <!-- ═══════════════ LIVE TRACKING ═══════════════ -->
    <div class="grid grid-cols-1 lg:grid-cols-2 gap-5">

      <!-- Eye Tracking -->
      <div class="rounded-3xl p-6" style="background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.05)">
        <div class="flex items-center gap-2.5 mb-5">
          <Eye :size="16" style="color:rgba(96,165,250,0.7)" />
          <span class="text-xs font-bold tracking-[0.15em] uppercase" style="color:rgba(255,255,255,0.4)">Eye Tracking</span>
          <span v-if="eyeModule" class="ml-auto text-[10px]" style="color:rgba(96,165,250,0.5)">{{ eyeModule.name }}</span>
        </div>
        <div v-if="store.trackingData" class="grid grid-cols-2 gap-8">
          <div v-for="side in ['left','right'] as const" :key="side" class="flex flex-col items-center gap-4">
            <p class="text-[10px] tracking-[0.2em] uppercase font-bold" style="color:rgba(255,255,255,0.25)">{{ side }}</p>
            <!-- Gaze circle -->
            <div class="relative w-24 h-24 rounded-full"
                 style="background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.07);">
              <div class="absolute left-1/2 inset-y-0 w-px" style="background:rgba(255,255,255,0.04); transform:translateX(-50%)" />
              <div class="absolute top-1/2 inset-x-0 h-px" style="background:rgba(255,255,255,0.04); transform:translateY(-50%)" />
              <div class="absolute w-4 h-4 rounded-full transition-all duration-75"
                   style="background:rgb(96,165,250); box-shadow:0 0 12px rgba(96,165,250,0.5); transform:translate(-50%,-50%)"
                   :style="{
                     left: (50 + store.trackingData.eye[side].gazeX * 36) + '%',
                     top:  (50 - store.trackingData.eye[side].gazeY * 36) + '%',
                   }" />
            </div>
            <!-- Openness -->
            <div class="w-full space-y-1.5">
              <div class="flex justify-between text-[10px]" style="color:rgba(255,255,255,0.3)">
                <span>Openness</span>
                <span class="font-mono tabular-nums">{{ (store.trackingData.eye[side].openness * 100).toFixed(0) }}%</span>
              </div>
              <div class="h-2 rounded-full overflow-hidden" style="background:rgba(255,255,255,0.04)">
                <div class="h-full rounded-full transition-all duration-75"
                     style="background:rgba(96,165,250,0.6)"
                     :style="{ width: (store.trackingData.eye[side].openness * 100) + '%' }" />
              </div>
            </div>
            <!-- Pupil -->
            <div class="w-full space-y-1.5">
              <div class="flex justify-between text-[10px]" style="color:rgba(255,255,255,0.3)">
                <span>Pupil</span>
                <span class="font-mono tabular-nums">{{ store.trackingData.eye[side].pupil.toFixed(1) }}mm</span>
              </div>
              <div class="h-2 rounded-full overflow-hidden" style="background:rgba(255,255,255,0.04)">
                <div class="h-full rounded-full transition-all duration-75"
                     style="background:rgba(96,165,250,0.35)"
                     :style="{ width: Math.min(store.trackingData.eye[side].pupil / 8 * 100, 100) + '%' }" />
              </div>
            </div>
          </div>
        </div>
        <div v-else class="h-32 flex items-center justify-center">
          <p class="text-xs tracking-widest uppercase" style="color:rgba(255,255,255,0.1)">No eye data</p>
        </div>
      </div>

      <!-- Head Rotation -->
      <div class="rounded-3xl p-6" style="background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.05)">
        <div class="flex items-center gap-2.5 mb-5">
          <Move3d :size="16" style="color:rgba(192,132,252,0.7)" />
          <span class="text-xs font-bold tracking-[0.15em] uppercase" style="color:rgba(255,255,255,0.4)">Head Rotation</span>
        </div>
        <div v-if="store.trackingData" class="space-y-5">
          <div v-for="axis in [
            { label:'Yaw',   value: store.trackingData.head.yaw,   color:'rgba(96,165,250,0.7)', bg:'rgba(96,165,250,0.15)' },
            { label:'Pitch', value: store.trackingData.head.pitch, color:'rgba(192,132,252,0.7)', bg:'rgba(192,132,252,0.15)' },
            { label:'Roll',  value: store.trackingData.head.roll,  color:'rgba(52,211,153,0.7)',  bg:'rgba(52,211,153,0.15)' },
          ]" :key="axis.label" class="space-y-2">
            <div class="flex justify-between text-xs" style="color:rgba(255,255,255,0.35)">
              <span class="tracking-[0.1em] uppercase font-medium">{{ axis.label }}</span>
              <span class="font-mono tabular-nums">{{ (axis.value * 90).toFixed(1) }}°</span>
            </div>
            <div class="h-3 rounded-full overflow-hidden relative" style="background:rgba(255,255,255,0.04)">
              <div class="absolute inset-y-0 left-1/2 w-px" style="background:rgba(255,255,255,0.1)" />
              <div class="absolute h-full w-3 rounded-full transition-all duration-75"
                   :style="{ background: axis.color, left: ((axis.value + 1) / 2 * 100) + '%', transform: 'translateX(-50%)' }" />
              <!-- Trail fill from center -->
              <div class="absolute h-full rounded-full transition-all duration-75"
                   :style="{
                     background: axis.bg,
                     left: axis.value >= 0 ? '50%' : ((axis.value + 1) / 2 * 100) + '%',
                     width: (Math.abs(axis.value) * 50) + '%'
                   }" />
            </div>
          </div>
        </div>
        <div v-else class="h-32 flex items-center justify-center">
          <p class="text-xs tracking-widest uppercase" style="color:rgba(255,255,255,0.1)">No head data</p>
        </div>
      </div>
    </div>

    <!-- ═══════════════ BOTTOM ROW ═══════════════ -->
    <div class="grid grid-cols-1 lg:grid-cols-3 gap-5">

      <!-- Expression Grid (takes 2 cols) -->
      <div class="lg:col-span-2 rounded-3xl p-6" style="background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.05)">
        <div class="flex items-center gap-2.5 mb-5">
          <Smile :size="16" style="color:rgba(192,132,252,0.7)" />
          <span class="text-xs font-bold tracking-[0.15em] uppercase" style="color:rgba(255,255,255,0.4)">Expressions</span>
          <span v-if="store.trackingData" class="ml-auto text-[10px]" style="color:rgba(255,255,255,0.2)">
            {{ activeExprCount }} active
          </span>
        </div>
        <div v-if="store.trackingData" class="grid grid-cols-2 xl:grid-cols-4 gap-x-6 gap-y-3">
          <div v-for="(val, i) in store.trackingData.shapes.slice(0,16)" :key="i" class="space-y-1.5">
            <div class="flex justify-between text-[10px]" style="color:rgba(255,255,255,0.3)">
              <span class="truncate">{{ expressionNames[i] ?? `Shape ${i}` }}</span>
              <span class="font-mono tabular-nums">{{ (val * 100).toFixed(0) }}%</span>
            </div>
            <div class="h-2 rounded-full overflow-hidden" style="background:rgba(255,255,255,0.04)">
              <div class="h-full rounded-full transition-all duration-75"
                   :style="{ width: (val * 100) + '%', background: val > 0.5 ? 'rgba(192,132,252,0.7)' : 'rgba(96,165,250,0.5)' }" />
            </div>
          </div>
        </div>
        <div v-else class="h-24 flex items-center justify-center">
          <p class="text-xs tracking-widest uppercase" style="color:rgba(255,255,255,0.1)">No expression data</p>
        </div>
      </div>

      <!-- Live Stats -->
      <div class="rounded-3xl p-6 space-y-5" style="background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.05)">
        <div class="flex items-center gap-2.5">
          <Activity :size="16" style="color:rgba(52,211,153,0.7)" />
          <span class="text-xs font-bold tracking-[0.15em] uppercase" style="color:rgba(255,255,255,0.4)">Live Stats</span>
        </div>
        <div class="space-y-4">
          <!-- OSC Throughput -->
          <div>
            <p class="text-[10px] tracking-[0.15em] uppercase font-medium mb-1" style="color:rgba(255,255,255,0.25)">OSC Throughput</p>
            <div class="flex items-baseline gap-2">
              <span class="text-2xl font-black text-white/80 tabular-nums">{{ store.oscStatus.msgsPerSec }}</span>
              <span class="text-[10px]" style="color:rgba(255,255,255,0.2)">msg/s</span>
            </div>
          </div>
          <!-- Parameters -->
          <div>
            <p class="text-[10px] tracking-[0.15em] uppercase font-medium mb-1" style="color:rgba(255,255,255,0.25)">Parameters</p>
            <div class="flex items-baseline gap-2">
              <span class="text-2xl font-black text-white/80 tabular-nums">{{ store.parameterValues.length }}</span>
              <span class="text-[10px]" style="color:rgba(255,255,255,0.2)">active</span>
            </div>
          </div>
          <!-- OSC Target -->
          <div>
            <p class="text-[10px] tracking-[0.15em] uppercase font-medium mb-1" style="color:rgba(255,255,255,0.25)">OSC Target</p>
            <div class="flex items-center gap-2">
              <Wifi :size="12" :style="store.oscStatus.connected ? 'color:rgba(52,211,153,0.7)' : 'color:rgba(255,255,255,0.15)'" />
              <span class="text-sm font-mono text-white/50">{{ store.oscConfig.ip }}:{{ store.oscConfig.sendPort }}</span>
            </div>
          </div>
          <!-- Uptime -->
          <div>
            <p class="text-[10px] tracking-[0.15em] uppercase font-medium mb-1" style="color:rgba(255,255,255,0.25)">Uptime</p>
            <span class="text-sm font-mono text-white/50 tabular-nums">{{ uptime }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
