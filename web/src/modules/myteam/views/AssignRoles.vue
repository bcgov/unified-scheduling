<script setup lang="ts">
import { Permissions, type RoleDto, type UserResponse, type UserRoleResponseDto } from '@/api-access/generated/models';
import { getApiRoles } from '@/api-access/generated/roles/roles';
import { getApiUsersIdRoles, postApiUsersIdRoles } from '@/api-access/generated/users/users';
import { useAccessControl } from '@/composables/useAccessControl';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import AssignRoleModal from '../components/AssignRoleModal.vue';
import { mdiClockRemove, mdiPencil, mdiPlus } from '@mdi/js';
import { computed, ref } from 'vue';
import type { SelectOption } from '@/types/select';

const props = defineProps<{
  user: UserResponse;
}>();

const accessControl = useAccessControl();

const { data: roles, error: rolesError, isFetching: isFetchingRoles } = getApiRoles();

const {
  data: assignedRoles,
  error: assignedRolesError,
  isFetching: isFetchingAssignedRoles,
  execute: fetchAssignedRoles,
} = getApiUsersIdRoles(props.user.id);

const showAssignRoleModal = ref(false);
const selectedAssignment = ref<UserRoleResponseDto | null>(null);
const showExpiryConfirm = ref(false);
const assignmentToDelete = ref<UserRoleResponseDto | null>(null);
const isExpiringRole = ref(false);
const deleteError = ref('');
const selectedExpiryReason = ref('');
const selectedRoleFilter = ref<'active' | 'historical'>('active');

const roleFilterOptions: SelectOption[] = [
  { code: 'active', description: 'Active' },
  { code: 'historical', description: 'Historical' },
];

const expireReasonOptions: SelectOption[] = [
  { code: 'OPERDEMAND', description: 'Cover Operational Demands' },
  { code: 'PERSONAL', description: 'Personal Decision' },
  { code: 'ENTRYERR', description: 'Entry Error' },
];

const roleNameById = computed(() => {
  const map = new Map<number, string>();
  for (const role of roles.value ?? []) {
    if (typeof role.id === 'number' && role.name) {
      map.set(role.id, role.name);
    }
  }
  return map;
});

const roleList = computed<RoleDto[]>(() => roles.value ?? []);

const userRoleRows = computed(() => assignedRoles.value ?? []);

const filteredUserRoleRows = computed(() => {
  const now = Date.now();

  return userRoleRows.value.filter((assignment) => {
    const expiry = assignment.expiryDate;
    const expiryTimestamp = expiry ? new Date(expiry).getTime() : Number.NaN;
    const isHistorical = Number.isFinite(expiryTimestamp) && expiryTimestamp <= now;

    if (selectedRoleFilter.value === 'historical') {
      return isHistorical;
    }

    return !isHistorical;
  });
});

const emptyStateDescription = computed(() =>
  selectedRoleFilter.value === 'historical'
    ? 'This user has no historical role assignments.'
    : 'This user has no active role assignments yet. Use Assign Role to create one.',
);

const userRoleHeaders = [
  { title: 'Role', key: 'roleName', sortable: false },
  { title: 'Effective Date', key: 'effectiveDate', sortable: false },
  { title: 'Expiry Date', key: 'expiryDate', sortable: false },
  { title: 'Actions', key: 'actions', sortable: false, align: 'end' as const, width: 140 },
];

const handleOpenAddModal = () => {
  selectedAssignment.value = null;
  showAssignRoleModal.value = true;
};

const handleOpenEditModal = (assignment: UserRoleResponseDto) => {
  selectedAssignment.value = assignment;
  showAssignRoleModal.value = true;
};

const handleModalClose = () => {
  showAssignRoleModal.value = false;
  selectedAssignment.value = null;
};

const handleOpenExpiryConfirm = (assignment: UserRoleResponseDto) => {
  assignmentToDelete.value = assignment;
  selectedExpiryReason.value = '';
  deleteError.value = '';
  showExpiryConfirm.value = true;
};

const handleExpireConfirmClose = () => {
  showExpiryConfirm.value = false;
  assignmentToDelete.value = null;
  selectedExpiryReason.value = '';
  deleteError.value = '';
};

const handleAssignmentSaved = async () => {
  await fetchAssignedRoles();
};

const resolveRoleName = (roleId?: number): string => {
  if (!roleId) {
    return '-';
  }

  return roleNameById.value.get(roleId) ?? `Role ${roleId}`;
};

const formatDate = (value?: string | null): string => {
  if (!value) {
    return '-';
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return '-';
  }

  return parsed.toLocaleDateString('en-CA');
};

const isHistoricalRole = (assignment: UserRoleResponseDto): boolean => {
  const expiry = assignment.expiryDate;
  if (!expiry) {
    return false;
  }

  const expiryTimestamp = new Date(expiry).getTime();
  return Number.isFinite(expiryTimestamp) && expiryTimestamp <= Date.now();
};

const handleExpireAssignment = async () => {
  if (!assignmentToDelete.value) {
    return;
  }

  const roleId = assignmentToDelete.value.roleId;
  const effectiveDate = assignmentToDelete.value.effectiveDate;

  if (!roleId || !effectiveDate) {
    deleteError.value = 'Unable to expire this assignment due to missing role details.';
    return;
  }

  isExpiringRole.value = true;
  deleteError.value = '';

  try {
    const requestPayload = {
      roleId,
      effectiveDate,
      expiryDate: new Date().toISOString(),
      ...(selectedExpiryReason.value ? { expiryReason: selectedExpiryReason.value } : {}),
    };

    const { error } = await postApiUsersIdRoles(props.user.id, requestPayload);

    if (error.value) {
      deleteError.value = error.value.message || 'Failed to expire role assignment.';
      return;
    }

    handleExpireConfirmClose();
    await fetchAssignedRoles();
  } catch (err: unknown) {
    deleteError.value = err instanceof Error ? err.message : 'Failed to expire role assignment.';
  } finally {
    isExpiringRole.value = false;
  }
};
</script>

<template>
  <div class="assign-routes">
    <div class="assign-routes-header">
      <h3>User Roles</h3>
      <UaBtn
        v-if="accessControl.hasPermission(Permissions.UserRoleAssign)"
        :prepend-icon="mdiPlus"
        @click="handleOpenAddModal"
      >
        Assign Role
      </UaBtn>
    </div>

    <UaAlert v-if="rolesError" type="error" @close="() => {}">Failed to load roles: {{ rolesError.message }}</UaAlert>
    <UaAlert v-if="assignedRolesError" type="error" @close="() => {}">
      Failed to load assigned roles: {{ assignedRolesError.message }}
    </UaAlert>

    <div v-if="isFetchingRoles || isFetchingAssignedRoles" class="loading-state">Loading assigned roles...</div>

    <div v-else class="assign-routes-filters">
      <UaSelect v-model="selectedRoleFilter" label="View" :items="roleFilterOptions" />
    </div>

    <UaDataTable
      v-if="!isFetchingRoles && !isFetchingAssignedRoles && filteredUserRoleRows.length"
      :headers="userRoleHeaders"
      :items="filteredUserRoleRows"
      :items-per-page="-1"
      density="comfortable"
      hide-default-footer
    >
      <template #[`item.roleName`]="{ item }">
        {{ resolveRoleName(item.roleId) }}
      </template>

      <template #[`item.effectiveDate`]="{ item }">
        {{ formatDate(item.effectiveDate) }}
      </template>

      <template #[`item.expiryDate`]="{ item }">
        {{ formatDate(item.expiryDate) }}
      </template>

      <template #[`item.actions`]="{ item }">
        <div class="col-actions">
          <UaBtn
            v-if="accessControl.hasPermission(Permissions.UserRoleAssign) && !isHistoricalRole(item)"
            icon
            variant="text"
            size="small"
            aria-label="Edit role assignment"
            title="Edit role assignment"
            @click="handleOpenEditModal(item)"
          >
            <v-icon :icon="mdiPencil" />
          </UaBtn>
          <UaBtn
            v-if="accessControl.hasPermission(Permissions.UserRoleAssign) && !isHistoricalRole(item)"
            icon
            variant="text"
            size="small"
            color="error"
            aria-label="Expire role assignment"
            title="Expire role assignment"
            @click="handleOpenExpiryConfirm(item)"
          >
            <v-icon :icon="mdiClockRemove" />
          </UaBtn>
        </div>
      </template>
    </UaDataTable>

    <UaPlaceholderPage
      v-else-if="!isFetchingRoles && !isFetchingAssignedRoles"
      title="No assigned roles"
      :description="emptyStateDescription"
    />

    <AssignRoleModal
      v-if="showAssignRoleModal"
      :user-id="props.user.id"
      :roles="roleList"
      :assignment="selectedAssignment"
      @close="handleModalClose"
      @saved="handleAssignmentSaved"
    />

    <UaModal
      v-if="showExpiryConfirm"
      title="Expire Role Assignment"
      tone="error"
      :loading="isExpiringRole"
      @close="handleExpireConfirmClose"
    >
      <template #alerts>
        <UaAlert v-if="deleteError" type="error" @close="deleteError = ''">
          {{ deleteError }}
        </UaAlert>
      </template>

      <p class="expire-message">
        Are you sure you want to expire the role assignment
        <strong>{{ resolveRoleName(assignmentToDelete?.roleId) }}</strong
        >?
      </p>

      <div class="expire-reason-field">
        <UaSelect v-model="selectedExpiryReason" label="Reason for Expiry" :items="expireReasonOptions" />
      </div>

      <template #actions>
        <UaBtn variant="outlined" :disabled="isExpiringRole" @click="handleExpireConfirmClose">Cancel</UaBtn>
        <UaBtn color="error" :loading="isExpiringRole" @click="handleExpireAssignment">Expire</UaBtn>
      </template>
    </UaModal>
  </div>
</template>

<style scoped>
.assign-routes {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-lg);
  padding-top: var(--ua-spacing-md);
}

.assign-routes-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ua-spacing-md);
}

.loading-state {
  padding: var(--ua-spacing-lg);
  text-align: center;
}

.assign-routes-filters {
  width: min(280px, 100%);
}

.col-actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--ua-spacing-xs);
}

.expire-message {
  margin: 0;
}

.expire-reason-field {
  margin-top: var(--ua-spacing-md);
}

@media (max-width: 768px) {
  .assign-routes-header {
    flex-direction: column;
    align-items: stretch;
  }
}
</style>
