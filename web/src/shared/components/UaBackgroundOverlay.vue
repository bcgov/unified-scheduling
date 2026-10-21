<script setup lang="ts">
withDefaults(
  defineProps<{
    /** Path to the overlay image, relative to the public/ folder (e.g. "/images/dashboard/bcss/foo.png"). Pass null/undefined to render no overlay. */
    image?: string | null;
    /** Overlay opacity between 0 and 1. */
    opacity?: number;
  }>(),
  {
    image: null,
    opacity: 0.1,
  },
);
</script>

<template>
  <div class="ua-background-overlay">
    <div v-if="image" class="ua-background-overlay__image" :style="{ backgroundImage: `url(${image})`, opacity }"></div>
    <div class="ua-background-overlay__content">
      <slot />
    </div>
  </div>
</template>

<style scoped>
.ua-background-overlay {
  position: relative;
}

.ua-background-overlay__image {
  position: fixed;
  inset: 0;
  background-repeat: no-repeat;
  background-position: center;
  background-size: 25%;
  pointer-events: none;
  z-index: 0;
}

.ua-background-overlay__content {
  position: relative;
  z-index: 1;
}
</style>
