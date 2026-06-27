import type { UserInfo } from '@/api-access/generated/models';
import { defineStore } from 'pinia';
import { computed, ref } from 'vue';

export const useAuthStore = defineStore('auth', () => {
  const userInfo = ref<UserInfo | null>(null);

  const isAuthenticated = computed(() => userInfo.value?.isAuthenticated ?? false);

  const userName = computed(() => userInfo.value?.name ?? null);

  const initials = computed(() => {
    if (!userName.value) return '';
    const parts = userName.value.trim().split(/\s+/);
    if (parts.length === 1) return parts[0][0]?.toUpperCase() ?? '';
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  });

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
    initials,
    currentUserId,
    homeLocationId,
    setUserInfo,
    clearUserInfo,
  };
});
