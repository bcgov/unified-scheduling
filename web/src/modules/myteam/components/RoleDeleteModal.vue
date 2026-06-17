<script setup lang="ts">
import type { RoleAssignedUserDto, RoleDto } from '@/api-access/generated/models';
import { getApiRolesIdUsers } from '@/api-access/generated/roles/roles';
import { useFetchAPI } from '@/api-access/useFetchAPI';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import type { SelectOption } from '@/types/select';
import { computed, ref } from 'vue';

const props = defineProps<{
  role: RoleDto;
  allRoles: RoleDto[];
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'deleted'): void;
}>();

// Fetch assigned users
const {
  data: assignedRoleUsers,
  error: assignedRoleUsersError,
  isFetching: isFetchingAssignedRoleUsers,
} = getApiRolesIdUsers(props.role.id!);

const isDeleting = ref(false);
const deleteError = ref('');

// Reassignment form state
const newRoleId = ref<number | null>(null);
const expiryDate = ref<string | null>(null);
const effectiveDate = ref<string>(new Date().toISOString().split('T')[0]);
const assignedUserHeaders = [
  {
    title: 'User',
    key: 'fullName',
    sortable: false,
    value: (item: RoleAssignedUserDto) => `${item.firstName} ${item.lastName}`,
  },
  {
    title: 'Is Active',
    key: 'isEnabled',
    sortable: false,
    value: (item: RoleAssignedUserDto) => (item.isEnabled ? 'Yes' : 'No'),
  },
];

// Computed properties
const availableRoles = computed(() =>
  props.allRoles.filter((r) => typeof r.id === 'number' && r.id !== props.role.id)
);

const availableRoleOptions = computed<SelectOption[]>(() =>
  availableRoles.value.map((r) => ({
    code: r.id!,
    description: r.name ?? `Role ${r.id}`,
  }))
);

const hasAssignedUsers = computed(() => (assignedRoleUsers.value ?? []).length > 0);

const isReassignmentComplete = computed(() => {
  if (!hasAssignedUsers.value) return true;
  return newRoleId.value !== null && !!effectiveDate.value;
});

const hasInvalidDateRange = computed(() => {
  if (!effectiveDate.value || !expiryDate.value) {
    return false;
  }

  return expiryDate.value <= effectiveDate.value;
});

const deleteButtonDisabled = computed(
  () =>
    isDeleting.value ||
    isFetchingAssignedRoleUsers.value ||
    !!assignedRoleUsersError.value ||
    !isReassignmentComplete.value ||
    hasInvalidDateRange.value ||
    (hasAssignedUsers.value && availableRoleOptions.value.length === 0)
);

const handleClose = () => {
  if (!isDeleting.value) {
    emit('close');
  }
};

const handleConfirmDelete = async () => {
  if (deleteButtonDisabled.value) {
    return;
  }

  isDeleting.value = true;
  deleteError.value = '';

  try {
    const payload = {
      newRoleId: hasAssignedUsers.value ? newRoleId.value : null,
      newRoleEffectiveDate: hasAssignedUsers.value ? effectiveDate.value : null,
      newRoleExpiryDate: hasAssignedUsers.value ? expiryDate.value : null,
    };

    const { error } = await useFetchAPI<void>(
      {
        url: `/api/roles/${props.role.id}`,
        method: 'DELETE',
        headers: { 'Content-Type': 'application/json' },
        data: payload,
      }
    );

    if (error.value) {
      const problemDetail = (error.value as { detail?: string } | null | undefined)?.detail;
      deleteError.value = problemDetail || error.value.message || 'An error occurred while deleting the role';
      return;
    }

    emit('deleted');
    emit('close');
  } catch (err) {
    deleteError.value = err instanceof Error ? err.message : 'An error occurred while deleting the role';
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
      <UaAlert v-if="assignedRoleUsersError" type="error" :closable="false">
        {{ assignedRoleUsersError.message }}
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

        <div class="reassignment-form">
          <p class="form-label">Reassign users to:</p>

          <UaSelect
            v-model="newRoleId"
            label="New Role"
            :items="availableRoleOptions"
            :disabled="isDeleting"
          />

          <UaTextField
            id="reassignment-effective-date"
            v-model="effectiveDate"
            label="Effective Date"
            type="date"
            :disabled="isDeleting"
          />

          <UaTextField
            id="reassignment-expiry-date"
            v-model="expiryDate"
            label="Expiry Date (Optional)"
            type="date"
            :disabled="isDeleting"
          />

          <p v-if="availableRoleOptions.length === 0" class="warning-text">
            No alternate role is available for reassignment.
          </p>
          <p v-if="hasInvalidDateRange" class="warning-text">Expiry date must be after effective date.</p>
        </div>
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
        :disabled="deleteButtonDisabled"
      >
        {{ (assignedRoleUsers?.length ?? 0) > 0 ? 'Reassign and Delete' : 'Delete' }}
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

.reassignment-form {
  margin-top: var(--ua-spacing-lg);
  padding: var(--ua-spacing-md);
  background-color: rgb(var(--v-theme-surface-dim));
  border-radius: 4px;
}

.form-label {
  margin-bottom: var(--ua-spacing-md);
  font-weight: 500;
  color: var(--ua-text-primary);
}

.reassignment-form :deep(.v-field) {
  margin-bottom: var(--ua-spacing-md);
}

.reassignment-form :deep(.v-field:last-child) {
  margin-bottom: 0;
}
</style>
