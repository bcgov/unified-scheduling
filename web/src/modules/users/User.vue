<script setup lang="ts">
import { onMounted } from 'vue';
import UserCard from './UserCard.vue';
import { getApiUsers } from '@/api-access/generated/users/users';
import { useUsersStore } from '@/stores/Users';

const usersStore = useUsersStore();

onMounted(async () => {
  const { data, execute } = getApiUsers();
  await execute();
  usersStore.entities = data.value || [];
});
</script>

<template>
  <h2 style="margin-left: 4rem">My Team</h2>

  <div style="padding: 2rem; background-color: #fff; display: flex; justify-content: flex-start; align-items: center">
    <input type="text" placeholder="Search" />
  </div>

  <div
    style="
      display: flex;
      flex-wrap: wrap;
      gap: 2rem;
      justify-content: flex-start;
      align-items: flex-start;
      padding: 2rem;
    "
  >
    <UserCard v-for="user in usersStore.entities" :key="user.id" :user="user" />
  </div>
</template>

<style scoped></style>
