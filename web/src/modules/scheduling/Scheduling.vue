<script setup lang="ts">
import Calendar from '@/shared/components/Calendar.vue';
import { useSchedulingStore } from '@/stores/Scheduling';
console.log('Scheduling Store:', useSchedulingStore().events);
</script>

<template>
  <div class="scheduling-container">
    <h2>Scheduling Module</h2>
    <Calendar style="padding: 8px 24px;" />
  </div>
</template>

<style scoped></style>
