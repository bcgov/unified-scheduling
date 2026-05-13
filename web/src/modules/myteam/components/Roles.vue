<template>
  <div class="user-roles">
    <UaPageHeader title="User Roles" />

    <UaCard v-if="roles.length > 0" title="Assigned Roles">
      <template #body>
        <div class="roles-list">
          <div v-for="role in roles" :key="role" class="role-item">
            {{ role }}
          </div>
        </div>
      </template>
    </UaCard>

    <UaPlaceholderPage v-else title="No roles assigned" description="This user has no roles currently assigned." />
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import UaPageHeader from '@/shared/components/UaPageHeader.vue';
import UaCard from '@/shared/components/UaCard.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';

// TODO: Fetch global roles and permissions from API
const roles = computed(() => {
  return [];
});
</script>

<style scoped>
.user-roles {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.roles-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.role-item {
  padding: 0.5rem 1rem;
  border: 1px solid var(--bc-border-color, #ccc);
  border-radius: 4px;
  background-color: var(--bc-surface-secondary, #f5f5f5);
}
</style>
