<script setup lang="ts">
import { computed } from 'vue'
import { useAppStore } from '@/stores/appStore'
import { Target, Play, Square, RotateCcw, Puzzle } from 'lucide-vue-next'

const store = useAppStore()
const canCalibrate = computed(() => store.hasActiveTracking)
</script>

<template>
  <div class="flex flex-col h-full overflow-hidden">
    <div class="flex-1 overflow-y-auto p-8 space-y-6">

      <div class="flex items-center justify-between">
        <div class="space-y-1">
          <h1 class="text-2xl font-black tracking-tighter text-white/90 uppercase">Calibration</h1>
          <p class="text-[10px] font-bold tracking-[0.3em] uppercase" style="color:rgba(255,255,255,0.2)">Expression Range Optimization</p>
        </div>
        <div class="flex items-center gap-3">
          <button
            v-if="canCalibrate && !store.calibration.active"
            @click="store.startCalibration()"
            class="flex items-center gap-2 px-5 py-2.5 rounded-xl text-sm font-medium tracking-wide transition-all"
            style="background:rgba(59,130,246,0.15); border:1px solid rgba(59,130,246,0.3); color:rgba(96,165,250,0.9)">
            <Play :size="14" />
            Start Calibration
          </button>
          <button
            v-else-if="store.calibration.active"
            @click="store.stopCalibration()"
            class="flex items-center gap-2 px-5 py-2.5 rounded-xl text-sm font-medium tracking-wide transition-all"
            style="background:rgba(239,68,68,0.1); border:1px solid rgba(239,68,68,0.25); color:rgba(239,68,68,0.8)">
            <Square :size="14" />
            Stop
          </button>
          <button
            v-if="canCalibrate && store.calibration.progress.length > 0"
            @click="store.resetCalibration()"
            class="flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-medium tracking-wide transition-all"
            style="background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.4)"
            onmouseenter="this.style.color='rgba(255,255,255,0.7)'"
            onmouseleave="this.style.color='rgba(255,255,255,0.4)'">
            <RotateCcw :size="14" />
            Reset All
          </button>
        </div>
      </div>

      <!-- No modules warning -->
      <div v-if="!canCalibrate"
           class="rounded-3xl p-8 flex items-center gap-5"
           style="background:rgba(251,191,36,0.04); border:1px solid rgba(251,191,36,0.12)">
        <Puzzle :size="24" style="color:rgba(251,191,36,0.5)" class="shrink-0" />
        <div>
          <p class="text-base font-semibold" style="color:rgba(251,191,36,0.8)">No active tracking module</p>
          <p class="text-xs mt-1" style="color:rgba(255,255,255,0.3)">
            Load and activate a tracking module before starting calibration.
          </p>
        </div>
      </div>

      <!-- Active calibration status -->
      <div v-if="store.calibration.active"
           class="rounded-3xl p-6 flex items-center gap-5"
           style="background:rgba(16,185,129,0.05); border:1px solid rgba(16,185,129,0.15)">
        <Target :size="24" class="animate-pulse shrink-0" style="color:rgba(52,211,153,0.8)" />
        <div>
          <p class="text-base font-semibold" style="color:rgba(52,211,153,0.9)">Calibrating…</p>
          <p class="text-xs mt-1" style="color:rgba(255,255,255,0.3)">
            Move your face through your full range of motion to capture min/max values
          </p>
        </div>
      </div>

      <!-- Expression ranges -->
      <div v-if="store.calibration.progress.length > 0"
           class="rounded-3xl overflow-hidden"
           style="background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.05)">
        <div class="px-6 py-4 border-b" style="border-color:rgba(255,255,255,0.05)">
          <p class="text-[10px] font-bold tracking-[0.2em] uppercase" style="color:rgba(255,255,255,0.3)">
            Expression Ranges · {{ store.calibration.progress.length }} expressions
          </p>
        </div>
        <div class="divide-y" style="border-color:rgba(255,255,255,0.03)">
          <div v-for="(expr, i) in store.calibration.progress" :key="i"
               class="flex items-center gap-5 px-6 py-3 hover:bg-white/[0.02] transition-colors"
               :class="i % 2 === 1 ? 'bg-white/[0.01]' : ''">
            <span class="text-xs w-44 truncate font-medium" style="color:rgba(255,255,255,0.4)">{{ expr.name }}</span>
            <div class="flex-1 h-3 rounded-full relative overflow-hidden" style="background:rgba(255,255,255,0.04)">
              <!-- Range fill -->
              <div class="absolute h-full rounded-full"
                   style="background:rgba(96,165,250,0.2)"
                   :style="{ left: (expr.min * 100) + '%', width: ((expr.max - expr.min) * 100) + '%' }" />
              <!-- Current position -->
              <div class="absolute h-full w-1 rounded-full transition-all duration-75 bg-white/70"
                   :style="{ left: (expr.current * 100) + '%' }" />
            </div>
            <span class="text-xs font-mono w-28 text-right tabular-nums" style="color:rgba(255,255,255,0.3)">
              {{ expr.min.toFixed(2) }} – {{ expr.max.toFixed(2) }}
            </span>
            <button @click="store.resetCalibration(i)"
                    class="p-1.5 rounded-lg transition-all"
                    style="color:rgba(255,255,255,0.2)"
                    onmouseenter="this.style.color='rgba(239,68,68,0.6)'"
                    onmouseleave="this.style.color='rgba(255,255,255,0.2)'"
                    title="Reset">
              <RotateCcw :size="12" />
            </button>
          </div>
        </div>
      </div>

      <div v-else-if="canCalibrate" class="py-16 text-center">
        <Target :size="32" class="mx-auto mb-4" style="color:rgba(255,255,255,0.08)" />
        <p class="text-xs tracking-widest uppercase" style="color:rgba(255,255,255,0.15)">
          Start calibration to see expression ranges
        </p>
      </div>

    </div>
  </div>
</template>
