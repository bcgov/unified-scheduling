<script setup lang="ts">
import { useUsersStore } from '@/stores/Users';

const props = defineProps<{
  userId: string;
}>();

const usersStore = useUsersStore();
const user = usersStore.entitiesMap[props.userId];
</script>

<template>
  <h2 style="margin-left: 4rem">Profile</h2>
  <div style="display: flex; background-color: #fff; padding: 2rem">
    <div>
      <v-avatar color="brown" size="120">
        <span class="text-headline-small">{{ user?.firstName?.[0] || '' }}{{ user?.lastName?.[0] || '' }}</span>
      </v-avatar>
      <h3>{{ user?.firstName }} {{ user?.lastName }}</h3>
      <p>Email: {{ user?.email }}</p>
      <!-- Add more user details as needed -->
    </div>
    <div>
      <h3>Identification</h3>
      <p>ID: {{ user?.id }}</p>
    </div>
  </div>
</template>
