<script setup lang="ts">
import { Permissions, type RoleDto, type UserResponse, type UserRoleResponseDto } from '@/api-access/generated/models';
import { getApiRoles } from '@/api-access/generated/roles/roles';
import { getApiUsersIdRoles } from '@/api-access/generated/users/users';
import { useAccessControl } from '@/composables/useAccessControl';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaCard from '@/shared/components/UaCard.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';
import AssignRoleModal from '../components/AssignRoleModal.vue';
import { mdiPencil, mdiPlus } from '@mdi/js';
import { computed, ref } from 'vue';

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

const userRoleHeaders = [
  { title: 'Role', key: 'roleName', sortable: false },
  { title: 'Effective Date', key: 'effectiveDate', sortable: false },
  { title: 'Expiry Date', key: 'expiryDate', sortable: false },
  { title: 'Actions', key: 'actions', sortable: false, align: 'end' as const, width: 100 },
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
</script>

<template>
  <div class="assign-routes">
    <div class="assign-routes-header">
      <h3>Assign Roles</h3>
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

    <UaCard v-else-if="userRoleRows.length" title="Assigned Roles">
      <UaDataTable
        :headers="userRoleHeaders"
        :items="userRoleRows"
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
              v-if="accessControl.hasPermission(Permissions.UserRoleAssign)"
              icon
              variant="text"
              size="small"
              aria-label="Edit role assignment"
              title="Edit role assignment"
              @click="handleOpenEditModal(item)"
            >
              <v-icon :icon="mdiPencil" />
            </UaBtn>
          </div>
        </template>
      </UaDataTable>
    </UaCard>

    <UaPlaceholderPage
      v-else
      title="No assigned roles"
      description="This user has no role assignments yet. Use Assign Role to create one."
    />

    <AssignRoleModal
      v-if="showAssignRoleModal"
      :user-id="props.user.id"
      :roles="roleList"
      :assignment="selectedAssignment"
      @close="handleModalClose"
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

.col-actions {
  display: flex;
  justify-content: flex-end;
}

@media (max-width: 768px) {
  .assign-routes-header {
    flex-direction: column;
    align-items: stretch;
  }
}
</style>
