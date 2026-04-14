<script setup lang="ts">
import { ref, computed } from 'vue'
import { useAppStore } from '@/stores/appStore'
import { Search, SlidersHorizontal } from 'lucide-vue-next'

const store = useAppStore()
const searchQuery    = ref('')
const showOnlyActive = ref(false)

const filteredParams = computed(() => {
  let params = store.parameterValues
  if (searchQuery.value) {
    const q = searchQuery.value.toLowerCase()
    params = params.filter(p => p.name.toLowerCase().includes(q))
  }
  if (showOnlyActive.value) params = params.filter(p => Math.abs(p.value) > 0.01)
  return params
})
</script>

<template>
  <div class="flex flex-col h-full w-full p-8 gap-4 overflow-hidden">

    <!-- Header + toolbar -->
    <div class="flex items-center justify-between shrink-0">
      <div class="space-y-1">
        <h1 class="text-2xl font-black tracking-tighter text-white/90 uppercase">Parameters</h1>
        <p class="text-[10px] font-bold tracking-[0.3em] uppercase" style="color:rgba(255,255,255,0.2)">
          {{ filteredParams.length }} / {{ store.parameterValues.length }} params
        </p>
      </div>
    </div>

    <div class="flex gap-3 shrink-0">
      <div class="relative flex-1">
        <Search :size="13" class="absolute left-3.5 top-1/2 -translate-y-1/2" style="color:rgba(255,255,255,0.25)" />
        <input v-model="searchQuery" type="text" placeholder="Search parameters…"
               class="w-full pl-10 pr-4 py-2.5 rounded-xl text-sm" />
      </div>
      <button @click="showOnlyActive = !showOnlyActive"
              class="px-4 py-2.5 rounded-xl text-xs font-medium tracking-wide transition-all"
              :style="showOnlyActive
                ? 'background:rgba(59,130,246,0.12); border:1px solid rgba(59,130,246,0.25); color:rgba(96,165,250,0.9)'
                : 'background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.35)'">
        Active only
      </button>
    </div>

    <!-- Table -->
    <div class="flex-1 rounded-3xl overflow-hidden min-h-0 flex flex-col"
         style="background:rgba(255,255,255,0.02); border:1px solid rgba(255,255,255,0.05)">
      <!-- Head -->
      <div class="grid grid-cols-[1fr_2fr_80px] text-[10px] font-bold tracking-[0.15em] uppercase px-5 py-3 border-b shrink-0"
           style="color:rgba(255,255,255,0.25); border-color:rgba(255,255,255,0.05)">
        <span>Parameter</span>
        <span>Value</span>
        <span class="text-right">Raw</span>
      </div>
      <!-- Body -->
      <div class="flex-1 overflow-y-auto">
        <div v-for="(param, i) in filteredParams" :key="param.name"
             class="grid grid-cols-[1fr_2fr_80px] items-center px-5 py-2 border-b transition-colors hover:bg-white/[0.03]"
             :style="`border-color:rgba(255,255,255,0.03); ${i % 2 === 1 ? 'background:rgba(255,255,255,0.01)' : ''}`">
          <span class="font-mono text-xs truncate pr-3" style="color:rgba(255,255,255,0.5)">{{ param.name }}</span>
          <div class="h-2 rounded-full overflow-hidden mr-4" style="background:rgba(255,255,255,0.04)">
            <div class="h-full rounded-full transition-all duration-75"
                 :style="{
                   width: (Math.abs(param.value) * 100) + '%',
                   background: Math.abs(param.value) > 0.7 ? 'rgba(192,132,252,0.7)' : 'rgba(96,165,250,0.5)'
                 }" />
          </div>
          <span class="font-mono text-xs text-right tabular-nums" style="color:rgba(255,255,255,0.35)">
            {{ param.value.toFixed(3) }}
          </span>
        </div>
        <div v-if="filteredParams.length === 0"
             class="h-full flex flex-col items-center justify-center py-16 gap-3">
          <SlidersHorizontal :size="28" style="color:rgba(255,255,255,0.08)" />
          <p class="text-xs tracking-widest uppercase" style="color:rgba(255,255,255,0.15)">
            {{ store.parameterValues.length === 0 ? 'Waiting for parameter data…' : 'No matches' }}
          </p>
        </div>
      </div>
    </div>
  </div>
</template>
