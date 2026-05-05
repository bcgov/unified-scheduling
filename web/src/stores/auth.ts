import type { UserInfo, UserResponse } from '@/api-access/generated/models';
import { defineStore } from 'pinia';
import { computed, ref } from 'vue';

export const useAuthStore = defineStore('auth', () => {
  const userInfo = ref<UserInfo | null>(null);
  const currentUser = ref<UserResponse | null>(null);

  const isAuthenticated = computed(() => userInfo.value?.isAuthenticated ?? false);

  const userName = computed(() => userInfo.value?.name ?? null);

  const isSupervisor = computed(() => userInfo.value?.roles.includes('Supervisor') ?? false);

  function setUserInfo(info: UserInfo | null) {
    userInfo.value = info;
  }

  function setCurrentUser(user: UserResponse | null) {
    currentUser.value = user;
  }

  function clearUserInfo() {
    userInfo.value = null;
    currentUser.value = null;
  }

  return {
    userInfo,
    currentUser,
    isAuthenticated,
    userName,
    isSupervisor,
    setUserInfo,
    setCurrentUser,
    clearUserInfo,
  };
});
