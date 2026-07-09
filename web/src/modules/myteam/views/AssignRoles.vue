<script setup lang="ts">
import { Permissions, type RoleDto, type UserResponse, type UserRoleResponseDto } from '@/api-access/generated/models';
import { getApiRoles } from '@/api-access/generated/roles/roles';
import { getApiUsersIdRoles } from '@/api-access/generated/users/users';
import { useAccessControl } from '@/composables/useAccessControl';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import AssignRoleModal from '../components/AssignRoleModal.vue';
import ExpireRoleModal from '../components/ExpireRoleModal.vue';
import { mdiClockRemove, mdiPencil, mdiPlus } from '@mdi/js';
import { isDateInputHistorical, toCalendarDateString } from '@/utils/date';
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
const showExpireRoleModal = ref(false);
const selectedExpiryAssignment = ref<UserRoleResponseDto | null>(null);
const selectedRoleFilter = ref<'active' | 'historical'>('active');

const roleFilterOptions: SelectOption[] = [
  { code: 'active', description: 'Active' },
  { code: 'historical', description: 'Historical' },
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

const roleList = computed<RoleDto[]>(() =>
  (roles.value ?? []).filter((r) => r.deletedOn == null && r.deletedById == null),
);

const userRoleRows = computed(() => assignedRoles.value ?? []);

const filteredUserRoleRows = computed(() => {
  return userRoleRows.value.filter((assignment) => {
    const isHistorical = isDateInputHistorical(assignment.expiryDate);

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
  selectedExpiryAssignment.value = assignment;
  showExpireRoleModal.value = true;
};

const handleExpireModalClose = () => {
  showExpireRoleModal.value = false;
  selectedExpiryAssignment.value = null;
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

const isHistoricalRole = (assignment: UserRoleResponseDto): boolean => {
  return isDateInputHistorical(assignment.expiryDate);
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
        {{ toCalendarDateString(item.effectiveDate) ?? '-' }}
      </template>

      <template #[`item.expiryDate`]="{ item }">
        {{ toCalendarDateString(item.expiryDate) ?? '-' }}
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

    <ExpireRoleModal
      v-if="showExpireRoleModal && selectedExpiryAssignment"
      :user-id="props.user.id"
      :assignment="selectedExpiryAssignment"
      :role-name="resolveRoleName(selectedExpiryAssignment.roleId)"
      @close="handleExpireModalClose"
      @saved="handleAssignmentSaved"
    />
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

@media (max-width: 768px) {
  .assign-routes-header {
    flex-direction: column;
    align-items: stretch;
  }
}
</style>
