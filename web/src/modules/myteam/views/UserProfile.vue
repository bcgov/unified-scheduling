<script setup lang="ts">
import { getApiUsersId } from '@/api-access/generated/users/users';
import { useAccessControl } from '@/composables/useAccessControl';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaPageHeader from '@/shared/components/UaPageHeader.vue';
import { computed, ref } from 'vue';
import UserFormModal from '../components/UserFormModal.vue';
import { Permissions } from '@/api-access/generated/models';
import { mdiPencil } from '@mdi/js';

const props = defineProps<{
  userId: string;
}>();

const { data, error, isFetching, execute } = getApiUsersId(props.userId);
const accessControl = useAccessControl();
const showBadgeNumber = computed(() => accessControl.isFeatureFlagEnabled('userBadgeNumber'));
const showEditUserModal = ref(false);

const handleEditMember = () => {
  showEditUserModal.value = true;
};

const handleUserUpdated = async () => {
  await execute();
};

const handleEditModalClose = () => {
  showEditUserModal.value = false;
};
</script>

<template>
  <div v-if="isFetching">Loading ...</div>
  <div v-else-if="error">Error: {{ error.message }}</div>
  <UaPageHeader title="Profile">
    <template #actions>
      <UaBtn
        v-if="accessControl.hasPermission(Permissions.UsersEdit)"
        @click="handleEditMember"
        :disabled="!data"
        :prepend-icon="mdiPencil"
        >Edit Member</UaBtn
      >
    </template>
  </UaPageHeader>
  <div class="profile-layout">
    <!-- Left Panel -->
    <div class="left-panel">
      <div class="avatar-container">
        <v-avatar color="brown" size="80">
          <span class="text-headline-small">{{ data?.firstName?.[0] || '' }}{{ data?.lastName?.[0] || '' }}</span>
        </v-avatar>
      </div>

      <div>
        <div>{{ data?.firstName }} {{ data?.lastName }}</div>
        <div>Chief Sheriff</div>
        <div v-if="showBadgeNumber">{{ data?.badgeNumber }}</div>
      </div>

      <div class="profile-subnav">
        <UaBtn variant="outlined">Identification</UaBtn>
        <UaBtn variant="outlined">Acting rank</UaBtn>
        <UaBtn variant="outlined">Schedule</UaBtn>
        <UaBtn variant="outlined">Work History</UaBtn>
        <UaBtn variant="outlined">Schedule</UaBtn>
        <UaBtn variant="outlined">Deactivate</UaBtn>
      </div>
    </div>
    <!-- Right Panel -->
    <div class="right-panel">
      <RouterView v-if="data" v-slot="{ Component }">
        <component :is="Component" :user="data" />
      </RouterView>
    </div>
  </div>

  <UserFormModal
    v-if="showEditUserModal && data"
    :user="data"
    @close="handleEditModalClose"
    @updated="handleUserUpdated"
  />
</template>

<style scoped>
.profile-layout {
  display: flex;
  gap: var(--ua-spacing-md);
  background-color: rgb(var(--v-theme-surface));
  padding: var(--ua-spacing-xl);
}

.left-panel {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-xl);
  align-items: center;
  margin-right: var(--ua-spacing-2xl);
}

.avatar-container {
  border: 1px solid rgb(var(--v-theme-surface-light));
  padding: var(--ua-spacing-xl);
}

.profile-subnav {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-md);
  width: 100%;
}

.right-panel {
  flex: 1;
  background-color: rgb(var(--v-theme-surface-light));
  padding: var(--ua-spacing-sm) var(--ua-spacing-xl);
}
</style>
