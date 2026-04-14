<script setup lang="ts">
import { ref, reactive, watch, computed } from 'vue'
import { Save, RotateCcw } from 'lucide-vue-next'
import type { ConfigField, ModuleConfigPayload } from '@/types'

const props = defineProps<{
  config: ModuleConfigPayload
}>()

const emit = defineEmits<{
  save: [moduleId: string, values: Record<string, unknown>]
}>()

// Working copy of values
const draft = reactive<Record<string, unknown>>({})
const saved = ref(false)

// Reset draft when config changes (e.g. fresh GET_MODULE_CONFIG response)
watch(() => props.config, (cfg) => {
  Object.keys(draft).forEach(k => delete draft[k])
  const fields = cfg.schema?.Fields ?? []
  for (const field of fields) {
    const stored = cfg.values?.[field.Key]
    draft[field.Key] = stored !== undefined ? stored : field.DefaultValue
  }
  saved.value = false
}, { immediate: true })

const fields = computed<ConfigField[]>(() => props.config.schema?.Fields ?? [])

function resetToDefaults() {
  for (const field of fields.value) {
    draft[field.Key] = field.DefaultValue
  }
  saved.value = false
}

function handleSave() {
  emit('save', props.config.moduleId, { ...draft })
  saved.value = true
  setTimeout(() => { saved.value = false }, 2500)
}

function numericStep(field: ConfigField): string {
  if (field.Type === 'Int') return '1'
  const range = (field.Max ?? 1) - (field.Min ?? 0)
  return range <= 1 ? '0.01' : '0.1'
}
</script>

<template>
  <div class="mt-4 space-y-5">
    <!-- Fields -->
    <div class="space-y-4">
      <div v-for="field in fields" :key="field.Key" class="space-y-1.5">
        <!-- Label row -->
        <div class="flex items-center justify-between">
          <label class="text-xs font-medium text-white/70">{{ field.Label }}</label>
          <span v-if="field.Type === 'Float' || field.Type === 'Int'"
                class="text-[10px] font-mono tabular-nums px-1.5 py-0.5 rounded"
                style="background:rgba(255,255,255,0.05); color:rgba(255,255,255,0.5)">
            {{ typeof draft[field.Key] === 'number' ? (draft[field.Key] as number).toFixed(field.Type === 'Int' ? 0 : 2) : draft[field.Key] }}
          </span>
        </div>
        <p v-if="field.Description" class="text-[10px]" style="color:rgba(255,255,255,0.3)">
          {{ field.Description }}
        </p>

        <!-- Float / Int → slider + number -->
        <template v-if="field.Type === 'Float' || field.Type === 'Int'">
          <div class="flex items-center gap-3">
            <input type="range"
                   :min="field.Min ?? 0" :max="field.Max ?? 1" :step="numericStep(field)"
                   :value="draft[field.Key] as number"
                   @input="draft[field.Key] = field.Type === 'Int'
                     ? parseInt(($event.target as HTMLInputElement).value)
                     : parseFloat(($event.target as HTMLInputElement).value)"
                   class="flex-1 h-1.5 rounded-full appearance-none cursor-pointer"
                   style="accent-color:rgba(99,102,241,0.8)" />
            <input type="number"
                   :min="field.Min" :max="field.Max" :step="numericStep(field)"
                   :value="draft[field.Key] as number"
                   @change="draft[field.Key] = field.Type === 'Int'
                     ? parseInt(($event.target as HTMLInputElement).value)
                     : parseFloat(($event.target as HTMLInputElement).value)"
                   class="w-20 px-2.5 py-1 rounded-xl text-xs text-right tabular-nums"
                   style="background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.7)" />
          </div>
        </template>

        <!-- Bool → toggle -->
        <template v-else-if="field.Type === 'Bool'">
          <button @click="draft[field.Key] = !(draft[field.Key] as boolean)"
                  class="relative w-11 h-6 rounded-full transition-all duration-300"
                  :style="draft[field.Key]
                    ? 'background:rgba(99,102,241,0.5)'
                    : 'background:rgba(255,255,255,0.1)'">
            <span class="absolute top-0.5 left-0.5 w-4 h-4 rounded-full transition-all duration-300"
                  :style="draft[field.Key]
                    ? 'background:rgba(255,255,255,0.95); transform:translateX(22px)'
                    : 'background:rgba(255,255,255,0.6); transform:translateX(0)'" />
          </button>
        </template>

        <!-- String / FilePath → text input -->
        <template v-else-if="field.Type === 'String' || field.Type === 'FilePath'">
          <input type="text"
                 :value="draft[field.Key] as string"
                 @input="draft[field.Key] = ($event.target as HTMLInputElement).value"
                 class="w-full px-3 py-2 rounded-xl text-xs"
                 style="background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.75)" />
        </template>

        <!-- Enum → select -->
        <template v-else-if="field.Type === 'Enum'">
          <select :value="draft[field.Key] as string"
                  @change="draft[field.Key] = ($event.target as HTMLSelectElement).value"
                  class="w-full px-3 py-2 rounded-xl text-xs"
                  style="background:rgba(255,255,255,0.04); border:1px solid rgba(255,255,255,0.08); color:rgba(255,255,255,0.75)">
            <option v-for="opt in field.Options" :key="opt" :value="opt">{{ opt }}</option>
          </select>
        </template>
      </div>
    </div>

    <!-- Action bar -->
    <div class="flex items-center justify-between pt-1">
      <button @click="resetToDefaults"
              class="flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs transition-all"
              style="background:rgba(255,255,255,0.03); border:1px solid rgba(255,255,255,0.07); color:rgba(255,255,255,0.35)">
        <RotateCcw :size="11" /> Defaults
      </button>
      <button @click="handleSave"
              class="flex items-center gap-1.5 px-4 py-1.5 rounded-xl text-xs font-medium transition-all"
              :style="saved
                ? 'background:rgba(16,185,129,0.15); border:1px solid rgba(16,185,129,0.25); color:rgba(52,211,153,0.9)'
                : 'background:rgba(99,102,241,0.15); border:1px solid rgba(99,102,241,0.3); color:rgba(129,140,248,0.9)'">
        <Save :size="11" /> {{ saved ? 'Saved!' : 'Save' }}
      </button>
    </div>
    <p class="text-[10px] text-center" style="color:rgba(255,255,255,0.2)">
      Restart the module for changes to take effect
    </p>
  </div>
</template>
