<script setup lang="ts">
defineOptions({ inheritAttrs: false });
</script>

<template>
  <v-btn v-bind="$attrs">
    <slot />
  </v-btn>
</template>
