<script setup lang="ts">
import UserCard from './UserCard.vue';
import { getApiUsers } from '@/api-access/generated/users/users';

const { data, error, isFetching } = getApiUsers();


</script>

<template>
  
  <div style="display: flex; align-items: center; justify-content: space-between;">
    <div>
      <h2 style="margin-left: 4rem">My Team</h2>
    </div>
    <div>
      <v-btn>+ Add Member</v-btn>
    </div>
  </div>

  <div style="padding: 2rem; background-color: #fff; display: flex; justify-content: flex-start; align-items: center">
    <input type="text" placeholder="Search" />
  </div>

  <div style="
      display: flex;
      flex-wrap: wrap;
      gap: 2rem;
      justify-content: flex-start;
      align-items: flex-start;
      padding: 2rem;
    ">
    <div v-if="isFetching">Loading</div>
    <div v-else-if="error">Error: {{ error.message }}</div>
    <UserCard v-else v-for="user in data" :key="user.id" :user="user" />
  </div>
</template>

<style scoped></style>
