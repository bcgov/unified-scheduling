<script setup lang="ts">
import { Permissions, type RoleDto } from '@/api-access/generated/models';
import { getApiRoles } from '@/api-access/generated/roles/roles';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaCard from '@/shared/components/UaCard.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaPageHeader from '@/shared/components/UaPageHeader.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import { useAccessControl } from '@/composables/useAccessControl';
import type { SelectOption } from '@/types/select';
import { mdiDelete, mdiPencil, mdiPlus } from '@mdi/js';
import { computed, ref } from 'vue';
import RoleDeleteModal from '../components/RoleDeleteModal.vue';
import RoleFormModal from '../components/RoleFormModal.vue';

// Access control
const accessControl = useAccessControl();

// Data state
const { data: roles, error: rolesError, isFetching: isFetchingRoles, execute: fetchRoles } = getApiRoles();

// Filter state
const selectedRoleFilter = ref<'active' | 'inactive'>('active');

const roleFilterOptions: SelectOption[] = [
  { code: 'active', description: 'Active' },
  { code: 'inactive', description: 'Inactive' },
];

const isRoleInactive = (role: RoleDto) => role.deletedOn != null || role.deletedById != null;

const filteredRoles = computed<RoleDto[]>(() => {
  return (roles.value ?? []).filter((role) =>
    selectedRoleFilter.value === 'inactive' ? isRoleInactive(role) : !isRoleInactive(role),
  );
});

const emptyStateDescription = computed(() =>
  selectedRoleFilter.value === 'inactive' ? 'There are no inactive roles.' : 'There are no active roles.',
);

// Modal states
const showCreateEditModal = ref(false);
const showDeleteConfirm = ref(false);
const selectedRole = ref<RoleDto | null>(null);
const roleToDelete = ref<RoleDto | null>(null);

const roleHeaders = [
  { title: 'Name', key: 'name', sortable: false },
  { title: 'Description', key: 'description', sortable: false },
  { title: 'Actions', key: 'actions', sortable: false, align: 'end' as const, width: 120 },
];

// Form handlers
const handleAddRole = () => {
  selectedRole.value = null;
  showCreateEditModal.value = true;
};

const handleEditRole = (role: RoleDto) => {
  selectedRole.value = role;
  showCreateEditModal.value = true;
};

const resetDeleteModalState = () => {
  showDeleteConfirm.value = false;
  roleToDelete.value = null;
};

const handleDeleteRole = (role: RoleDto) => {
  roleToDelete.value = role;
  showDeleteConfirm.value = true;
};

const handleRoleDeleted = async () => {
  resetDeleteModalState();
  await fetchRoles();
};

const handleRoleFormClose = () => {
  showCreateEditModal.value = false;
  selectedRole.value = null;
};

const handleRoleCreated = async () => {
  await fetchRoles();
  showCreateEditModal.value = false;
};

const handleRoleUpdated = async () => {
  await fetchRoles();
  showCreateEditModal.value = false;
};
</script>

<template>
  <div class="roles-permissions">
    <UaPageHeader title="Roles & Permissions">
      <template #actions>
        <UaBtn
          v-if="accessControl.hasPermission(Permissions.RolesCreate)"
          @click="handleAddRole"
          :prepend-icon="mdiPlus"
        >
          Add Role
        </UaBtn>
      </template>
    </UaPageHeader>

    <!-- Error Messages -->
    <UaAlert v-if="rolesError" type="error" @close="() => {}" class="roles-alert">
      Failed to load roles: {{ rolesError.message }}
    </UaAlert>

    <!-- Loading State -->
    <div v-if="isFetchingRoles" class="loading-state">Loading roles...</div>

    <template v-else>
      <!-- Filter -->
      <div class="roles-filters">
        <UaSelect v-model="selectedRoleFilter" label="View" :items="roleFilterOptions" />
      </div>

      <!-- Roles Table -->
      <UaCard v-if="filteredRoles.length > 0" title="Available Roles">
        <UaDataTable
          :headers="roleHeaders"
          :items="filteredRoles"
          :items-per-page="-1"
          density="comfortable"
          hide-default-footer
        >
          <template #[`item.name`]="{ item }">
            <span class="col-name">{{ item.name || '-' }}</span>
          </template>

          <template #[`item.description`]="{ item }">
            {{ item.description || '-' }}
          </template>

          <template #[`item.actions`]="{ item }">
            <div class="col-actions">
              <UaBtn
                v-if="accessControl.hasPermission(Permissions.RolesEdit) && !isRoleInactive(item)"
                icon
                variant="text"
                size="small"
                aria-label="Edit role"
                @click="handleEditRole(item)"
                title="Edit role"
              >
                <v-icon :icon="mdiPencil" />
              </UaBtn>
              <UaBtn
                v-if="accessControl.hasPermission(Permissions.RolesExpire) && !isRoleInactive(item)"
                icon
                variant="text"
                size="small"
                color="error"
                aria-label="Delete role"
                @click="handleDeleteRole(item)"
                title="Delete role"
              >
                <v-icon :icon="mdiDelete" />
              </UaBtn>
            </div>
          </template>
        </UaDataTable>
      </UaCard>

      <!-- Empty State -->
      <UaPlaceholderPage v-else title="No roles available" :description="emptyStateDescription" />
    </template>

    <!-- Create/Edit Modal -->
    <RoleFormModal
      v-if="showCreateEditModal"
      :role="selectedRole"
      @close="handleRoleFormClose"
      @created="handleRoleCreated"
      @updated="handleRoleUpdated"
    />

    <!-- Delete Confirmation Modal -->
    <RoleDeleteModal
      v-if="showDeleteConfirm && roleToDelete"
      :role="roleToDelete"
      :all-roles="roles ?? []"
      @close="resetDeleteModalState"
      @deleted="handleRoleDeleted"
    />
  </div>
</template>

<style scoped>
.roles-permissions {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-lg);
  padding: var(--ua-spacing-xl);
}

.roles-alert {
  max-width: 100%;
}

.roles-filters {
  width: min(280px, 100%);
}

.loading-state {
  padding: var(--ua-spacing-xl);
  text-align: center;
  color: var(--ua-text-secondary);
}

.col-actions {
  display: flex;
  gap: var(--ua-spacing-sm);
  justify-content: flex-end;
}

.col-name {
  font-weight: var(--ua-font-weight-semibold);
  word-break: break-word;
}

@media (max-width: 768px) {
  .roles-permissions {
    padding: var(--ua-spacing-md);
  }

  .table-header,
  .table-row {
    grid-template-columns: 1fr;
  }

  .col-name::before {
    content: 'Name: ';
    font-weight: var(--ua-font-weight-bold);
  }

  .col-description::before {
    content: 'Description: ';
    font-weight: var(--ua-font-weight-bold);
  }

  .col-actions::before {
    content: 'Actions: ';
    font-weight: var(--ua-font-weight-bold);
  }
}
</style>
