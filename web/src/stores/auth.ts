import type { UserInfo } from '@/api-access/generated/models';
import { defineStore } from 'pinia';
import { computed, ref } from 'vue';

export const useAuthStore = defineStore('auth', () => {
  const userInfo = ref<UserInfo | null>(null);

  const isAuthenticated = computed(() => userInfo.value?.isAuthenticated ?? false);

  const userName = computed(() => userInfo.value?.name ?? null);

  const currentUserId = computed(() => userInfo.value?.userId ?? null);

  const homeLocationId = computed(() => userInfo.value?.homeLocationId ?? null);

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
    currentUserId,
    homeLocationId,
    setUserInfo,
    clearUserInfo,
  };
});
