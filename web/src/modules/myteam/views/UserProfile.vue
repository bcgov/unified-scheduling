<script setup lang="ts">
import { getApiUsersId } from '@/api-access/generated/users/users';
import { useAccessControl } from '@/composables/useAccessControl';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaPageHeader from '@/shared/components/UaPageHeader.vue';
import { computed, onMounted, ref } from 'vue';
import UserFormModal from '../components/UserFormModal.vue';
import { LookupCodeTypes, Permissions } from '@/api-access/generated/models';
import { useLookupStore } from '@/stores/LookupStore';
import { mdiPencil } from '@mdi/js';

const props = defineProps<{
  userId: string;
}>();

const { data, error, isFetching, execute } = getApiUsersId(props.userId);
const accessControl = useAccessControl();
const lookupStore = useLookupStore();
const showBadgeNumber = computed(() => accessControl.isFeatureFlagEnabled('userBadgeNumber'));
const showEditUserModal = ref(false);

// Uses lastPhotoUpdate as a cache-busting query param so the browser re-fetches
// the photo image whenever it changes after an edit.
const photoSrc = computed(() => {
  if (!data.value?.photoUrl) return null;
  const bust = data.value.lastPhotoUpdate ?? data.value.id;
  return `${data.value.photoUrl}?v=${encodeURIComponent(bust)}`;
});
const avatarInitials = computed(() => `${data.value?.firstName?.[0] ?? ''}${data.value?.lastName?.[0] ?? ''}`);

const positionDescription = computed(() => {
  if (!data?.value?.rank) {
    return '-';
  }

  return lookupStore.entityMap[LookupCodeTypes.PositionTypes]?.[data.value.rank]?.description ?? '-';
});

const handleEditMember = () => {
  showEditUserModal.value = true;
};

const handleUserUpdated = async () => {
  await execute();
};

const handleEditModalClose = () => {
  showEditUserModal.value = false;
};

onMounted(async () => {
  await lookupStore.load(LookupCodeTypes.PositionTypes);
});
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
        <v-avatar color="brown" size="120">
          <v-img v-if="photoSrc" :src="photoSrc" cover :alt="avatarInitials" />
          <span v-else class="text-headline-small">{{ avatarInitials }}</span>
        </v-avatar>
      </div>

      <div>
        <div>{{ data?.firstName }} {{ data?.lastName }}</div>
        <div>{{ positionDescription }}</div>
        <div v-if="showBadgeNumber">{{ data?.badgeNumber }}</div>
      </div>

      <div class="profile-subnav">
        <UaBtn variant="outlined" :to="{ name: 'UserIdentification', params: { userId: props.userId } }">
          Identification
        </UaBtn>
        <UaBtn
          v-if="accessControl.hasPermission(Permissions.UserRoleAssign)"
          variant="outlined"
          :to="{ name: 'UserAssignRoles', params: { userId: props.userId } }"
        >
          Assign Roles
        </UaBtn>
        <UaBtn
          v-if="accessControl.hasPermission(Permissions.ActingPositionsView)"
          variant="outlined"
          :to="{ name: 'UserActingPositions', params: { userId: props.userId } }"
        >
          Acting Positions
        </UaBtn>
        <UaBtn
          v-if="accessControl.hasPermission(Permissions.AwayLocationsView)"
          variant="outlined"
          :to="{ name: 'UserAwayLocations', params: { userId: props.userId } }"
        >
          Away Locations
        </UaBtn>
        <UaBtn variant="outlined" disabled>Schedule</UaBtn>
        <UaBtn variant="outlined" disabled>Work History</UaBtn>
        <UaBtn variant="outlined" disabled>Deactivate</UaBtn>
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
