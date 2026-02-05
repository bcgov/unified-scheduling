<script setup lang="ts">
import { defineAsyncComponent } from 'vue'
import { inject } from 'vue';

const Training = defineAsyncComponent(() => import('../training/Training.vue'))

const config = inject('config') as { modules: string[] };

</script>

<template>
  <div class="users-container">
    <h2>User Management</h2>
    <Training v-if="config.modules.includes('training')" />
  </div>
</template>

<style scoped></style>