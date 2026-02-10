<script setup lang="ts">
import { defineAsyncComponent, shallowRef } from 'vue';
import { Modules } from '@/stores/config';
import { useAccessControl } from '@/composables/useAccessControl';

const Training = defineAsyncComponent(() => import('../training/Training.vue'));

const accessControl = useAccessControl();

const modules = shallowRef(Modules);

</script>

<template>
  <div class="users-container">
    <h2>User Management</h2>
    <Training v-if="accessControl.canAccessModule(modules.training)" />
  </div>
</template>

<style scoped></style>
