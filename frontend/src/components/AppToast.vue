<template>
  <transition name="toast-pop">
    <div v-if="open" class="app-toast" role="status" aria-live="polite">
      <div class="app-toast__icon">
        <AppIcon class="icon-svg" name="test" />
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

withDefaults(defineProps<{
  open: boolean
  title?: string
  message: string
}>(), {
  title: 'Listo',
})

defineEmits<{
  close: []
}>()
</script>