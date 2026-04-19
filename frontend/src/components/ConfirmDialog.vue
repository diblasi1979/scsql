<template>
  <div v-if="open" class="modal-overlay" @click.self="$emit('cancel')">
    <section class="modal-card modal-card--compact confirm-dialog">
      <div class="panel-heading">
        <div>
          <p class="panel-kicker">Confirm</p>
          <h3>{{ title }}</h3>
        </div>
        <button class="ghost-button icon-button" type="button" data-tooltip="Cerrar confirmación" aria-label="Cerrar confirmación" @click="$emit('cancel')">
          <AppIcon class="icon-svg" name="close" />
        </button>
      </div>

      <div class="confirm-dialog__body">
        <p>{{ message }}</p>
        <p v-if="detail" class="confirm-dialog__detail">{{ detail }}</p>
      </div>

      <div class="modal-actions">
        <button class="ghost-button button--compact" type="button" @click="$emit('cancel')">Cancelar</button>
        <button class="ghost-button ghost-button--danger button--compact" type="button" @click="$emit('confirm')">{{ confirmLabel }}</button>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import AppIcon from '@/components/AppIcon.vue'

withDefaults(defineProps<{
  open: boolean
  title: string
  message: string
  detail?: string
  confirmLabel?: string
}>(), {
  detail: '',
  confirmLabel: 'Confirmar',
})

defineEmits<{
  confirm: []
  cancel: []
}>()
</script>