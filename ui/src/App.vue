<script setup lang="ts">
import { onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAppStore } from '@/stores/appStore'
import ThreeBackground from '@/components/ThreeBackground.vue'
import {
  LayoutDashboard, Puzzle, SlidersHorizontal, Target,
  ScrollText, Settings, X
} from 'lucide-vue-next'

const store = useAppStore()
const route = useRoute()

const navItems = [
  { path: '/',            icon: LayoutDashboard,   label: 'Dashboard' },
  { path: '/modules',     icon: Puzzle,             label: 'Modules' },
  { path: '/parameters',  icon: SlidersHorizontal,  label: 'Parameters' },
  { path: '/calibration', icon: Target,             label: 'Calibration' },
  { path: '/output',      icon: ScrollText,         label: 'Output' },
  { path: '/settings',    icon: Settings,           label: 'Settings' },
]

onMounted(() => {
  store.initBridge()
})
</script>

<template>
  <ThreeBackground :is-reduced="false" />
  <div class="flex h-screen relative z-10">
    <!-- Sidebar -->
    <nav class="w-60 xl:w-[17rem] flex flex-col relative shrink-0"
         style="background: rgba(0,0,0,0.55); border-right: 1px solid rgba(255,255,255,0.05); backdrop-filter: blur(32px);">

      <!-- Vertical accent line -->
      <div class="absolute inset-y-0 right-0 w-px"
           style="background: linear-gradient(to bottom, transparent, rgba(59,130,246,0.3), transparent);" />

      <!-- Brand -->
      <div class="px-6 py-5 border-b" style="border-color: rgba(255,255,255,0.05);">
        <div class="flex items-center gap-3">
          <img src="/favicon.png" alt="VRCFT" class="w-9 h-9 rounded-xl shrink-0 object-cover"
               :style="store.hasActiveTracking ? 'box-shadow: 0 0 14px rgba(59,130,246,0.5)' : ''" />
          <div>
            <p class="text-sm font-black tracking-tighter uppercase italic text-white/90">VRCFaceTracking</p>
            <p class="text-[10px] tracking-[0.15em] uppercase" style="color: rgba(255,255,255,0.25);">{{ store.version }}</p>
          </div>
        </div>
      </div>

      <!-- Nav -->
      <div class="flex-1 py-4 space-y-1 px-3">
        <router-link
          v-for="item in navItems"
          :key="item.path"
          :to="item.path"
          class="flex items-center gap-3 px-4 py-3 rounded-xl transition-all duration-200 group relative"
          :style="route.path === item.path
            ? 'background: rgba(59,130,246,0.12); border: 1px solid rgba(59,130,246,0.2); color: rgba(255,255,255,0.95);'
            : 'border: 1px solid transparent; color: rgba(255,255,255,0.35);'"
        >
          <component
            :is="item.icon"
            :size="17"
            class="transition-all duration-200 shrink-0"
            :style="route.path === item.path ? 'color: rgb(96,165,250)' : ''"
          />
          <span class="text-sm font-medium tracking-wide">{{ item.label }}</span>

          <!-- Active dot -->
          <div v-if="route.path === item.path"
               class="ml-auto w-1.5 h-1.5 rounded-full bg-blue-400"
               style="box-shadow: 0 0 8px rgba(59,130,246,1);" />
        </router-link>
      </div>

      <!-- Exit -->
      <div class="p-3 border-t" style="border-color: rgba(255,255,255,0.05);">
        <button
          @click="store.exit()"
          class="flex items-center gap-3 w-full px-4 py-3 rounded-xl text-sm transition-all duration-200 group"
          style="color: rgba(255,255,255,0.25); border: 1px solid transparent;"
          onmouseenter="this.style.background='rgba(239,68,68,0.08)'; this.style.borderColor='rgba(239,68,68,0.15)'; this.style.color='rgba(239,68,68,0.8)'"
          onmouseleave="this.style.background=''; this.style.borderColor='transparent'; this.style.color='rgba(255,255,255,0.25)'"
        >
          <X :size="15" />
          <span class="font-medium tracking-wide">Exit</span>
        </button>
      </div>
    </nav>

    <!-- Main content -->
    <main class="flex-1 overflow-hidden flex flex-col min-h-0">
      <router-view v-slot="{ Component }">
        <transition name="page" mode="out-in">
          <component :is="Component" :key="route.path" class="flex-1 min-h-0" />
        </transition>
      </router-view>

      <!-- Footer -->
      <footer class="shrink-0 px-8 py-3 flex items-center justify-between border-t z-20"
              style="border-color: rgba(255,255,255,0.04); background: rgba(0,0,0,0.25); backdrop-filter: blur(16px);">
        <div class="flex items-center gap-3 text-[9px] font-bold text-white/20 uppercase tracking-[0.2em]">
          <span>&copy; {{ new Date().getFullYear() }} VRCFaceTracking</span>
        </div>
        <div class="flex items-center gap-6 text-[9px] uppercase tracking-widest text-white/20">
          <div class="flex items-center gap-2">
            <span class="w-1.5 h-1.5 rounded-full" :class="store.oscStatus.connected ? 'bg-emerald-500/60' : 'bg-white/10'" />
            OSC: <span class="text-white/40">{{ store.oscStatus.connected ? 'Live' : 'Idle' }}</span>
          </div>
          <div class="flex items-center gap-2">
            <span class="w-1.5 h-1.5 rounded-full" :class="store.activeModules.length > 0 ? 'bg-blue-500/60' : 'bg-white/10'" />
            Modules: <span class="text-white/40">{{ store.activeModules.length }} active</span>
          </div>
          <div class="flex items-center gap-2">
            <span class="w-1.5 h-1.5 bg-blue-500/30 rounded-full" />
            Build: <span class="text-white/40">{{ store.version }}</span>
          </div>
        </div>
      </footer>
    </main>
  </div>
</template>

<style>
.page-enter-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}
.page-leave-active {
  transition: opacity 0.1s ease;
}
.page-enter-from {
  opacity: 0;
  transform: translateY(6px);
}
.page-leave-to {
  opacity: 0;
}
</style>
