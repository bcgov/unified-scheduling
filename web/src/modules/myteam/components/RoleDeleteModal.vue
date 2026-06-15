<script setup lang="ts">
import type { RoleAssignedUserDto, RoleDto } from '@/api-access/generated/models';
import { deleteApiRolesId, getApiRolesIdUsers } from '@/api-access/generated/roles/roles';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { ref } from 'vue';

const props = defineProps<{
  role: RoleDto;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'deleted'): void;
}>();

// Fetch assigned users
const { data: assignedRoleUsers, error: assignedRoleUsersError, isFetching: isFetchingAssignedRoleUsers } = getApiRolesIdUsers(props.role.id!);

const isDeleting = ref(false);
const deleteError = ref('');

const assignedUserHeaders = [
  { title: 'User', key: 'fullName', sortable: false, value: (item: RoleAssignedUserDto) => `${item.firstName} ${item.lastName}` },
  { title: 'Is Active', key: 'isEnabled', sortable: false, value: (item: RoleAssignedUserDto) => (item.isEnabled ? 'Yes' : 'No') },
];

const handleClose = () => {
  if (!isDeleting.value) {
    emit('close');
  }
};

const handleConfirmDelete = async () => {
  if (isFetchingAssignedRoleUsers.value || (assignedRoleUsers.value ?? []).length > 0 || assignedRoleUsersError.value) {
    return;
  }

  isDeleting.value = true;
  deleteError.value = '';

  try {
    const { error } = await deleteApiRolesId(props.role.id!);

    if (error.value) {
      deleteError.value =
        error.value instanceof Error ? error.value.message : 'An error occurred while deleting the role';
      return;
    }

    emit('deleted');
    emit('close');
  } finally {
    isDeleting.value = false;
  }
};

</script>

<template>
  <UaModal title="Delete Role" :loading="isDeleting" tone="error" @close="handleClose">
    <template #alerts>
      <UaAlert v-if="deleteError" type="error" @close="deleteError = ''">
        {{ deleteError }}
      </UaAlert>
      <UaAlert v-if="assignedRoleUsersError" type="error" @close="assignedRoleUsersError = ''">
        {{ assignedRoleUsersError }}
      </UaAlert>
    </template>

    <div class="delete-confirmation">
      <p v-if="isFetchingAssignedRoleUsers">Checking users assigned to this role...</p>

      <template v-else-if="(assignedRoleUsers?.length ?? 0) > 0">
        <p>
          This role is assigned to <strong>{{ assignedRoleUsers?.length }}</strong> user(s).
        </p>

        <UaDataTable
          class="attached-users-table"
          :headers="assignedUserHeaders"
          :items="assignedRoleUsers ?? []"
          :items-per-page="-1"
          density="compact"
          hide-default-footer
        >
        </UaDataTable>

        <p class="warning-text">Please move users to new role(s).</p>
      </template>

      <template v-else-if="!assignedRoleUsersError">
        <p>
          Are you sure you want to delete the role <strong>{{ props.role.name }}</strong
          >?
        </p>
        <p class="warning-text">This action cannot be undone.</p>
      </template>
    </div>

    <template #actions>
      <UaBtn variant="outlined" @click="handleClose" :disabled="isDeleting"> Cancel </UaBtn>
      <UaBtn
        color="error"
        variant="flat"
        @click="handleConfirmDelete"
        :loading="isDeleting"
        :disabled="isFetchingAssignedRoleUsers || (assignedRoleUsers?.length ?? 0) > 0 || !!assignedRoleUsersError"
      >
        Delete
      </UaBtn>
    </template>
  </UaModal>
</template>

<style scoped>
.delete-confirmation {
  padding: var(--ua-spacing-md) 0;

  p {
    margin: var(--ua-spacing-md) 0;
    color: var(--ua-text-primary);
  }

  strong {
    color: rgb(var(--v-theme-primary));
  }
}

.warning-text {
  color: rgb(var(--v-theme-warning));
  font-size: var(--ua-font-size-sm);
}

.attached-users-table {
  margin: var(--ua-spacing-sm) 0;
}
</style>
