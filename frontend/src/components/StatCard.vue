<template>
  <article class="stat-card">
    <span class="stat-kicker">Live metric</span>
    <p>{{ label }}</p>
    <strong>{{ value }}</strong>
    <span>{{ detail }}</span>
  </article>
</template>

<script setup lang="ts">
defineProps<{
  label: string
  value: string | number
  detail: string
}>()
</script>