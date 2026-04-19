<template>
  <transition name="toast-pop">
    <div v-if="open" :class="['app-toast', `app-toast--${variant}`]" role="status" aria-live="polite">
      <div class="app-toast__icon">
        <AppIcon class="icon-svg" :name="iconName" />
      </div>
      <div class="app-toast__copy">
        <strong>{{ title }}</strong>
        <p>{{ message }}</p>
      </div>
      <button class="ghost-button icon-button app-toast__close" type="button" data-tooltip="Cerrar aviso" aria-label="Cerrar aviso" @click="$emit('close')">
        <AppIcon class="icon-svg" name="close" />
      </button>
    </div>
  </transition>
</template>

<script setup lang="ts">
import AppIcon from '@/components/AppIcon.vue'
import { computed } from 'vue'

const props = withDefaults(defineProps<{
  open: boolean
  title?: string
  message: string
  variant?: 'success' | 'info' | 'warning' | 'error'
}>(), {
  title: 'Listo',
  variant: 'success',
})

const iconByVariant = {
  success: 'success',
  info: 'info',
  warning: 'warning',
  error: 'error',
} as const

const iconName = computed(() => iconByVariant[props.variant])

defineEmits<{
  close: []
}>()
</script>