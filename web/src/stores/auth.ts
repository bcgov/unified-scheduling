import { ref, computed } from 'vue';
import { defineStore } from 'pinia';
import type { UserInfo } from '@/api-access/generated/models';

export const useAuthStore = defineStore('auth', () => {
  const userInfo = ref<UserInfo | null>(null);

  const isAuthenticated = computed(() => userInfo.value?.isAuthenticated ?? false);

  const userName = computed(() => userInfo.value?.name ?? null);

  function setUserInfo(info: UserInfo | null) {
    userInfo.value = info;
  }

  function clearUserInfo() {
    userInfo.value = null;
  }

  return {
    userInfo,
    isAuthenticated,
    userName,
    setUserInfo,
    clearUserInfo,
  };
});
