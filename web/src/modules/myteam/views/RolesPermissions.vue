<script setup lang="ts">
import { Permissions, type RoleDto } from '@/api-access/generated/models';
import { getApiRoles, deleteApiRolesId } from '@/api-access/generated/roles/roles';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaCard from '@/shared/components/UaCard.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaPageHeader from '@/shared/components/UaPageHeader.vue';
import UaPlaceholderPage from '@/shared/components/UaPlaceholderPage.vue';
import { useAccessControl } from '@/composables/useAccessControl';
import { mdiDelete, mdiPencil, mdiPlus } from '@mdi/js';
import { ref } from 'vue';
import RoleFormModal from '../components/RoleFormModal.vue';

// Access control
const accessControl = useAccessControl();

// Data state
const { data: roles, error: rolesError, isFetching: isFetchingRoles, execute: fetchRoles } = getApiRoles();

// Modal states
const showCreateEditModal = ref(false);
const showDeleteConfirm = ref(false);
const selectedRole = ref<RoleDto | null>(null);
const roleToDelete = ref<RoleDto | null>(null);
const isDeleting = ref(false);
const deleteError = ref('');

// Form handlers
const handleAddRole = () => {
  selectedRole.value = null;
  showCreateEditModal.value = true;
};

const handleEditRole = (role: RoleDto) => {
  selectedRole.value = role;
  showCreateEditModal.value = true;
};

const handleDeleteRole = (role: RoleDto) => {
  roleToDelete.value = role;
  showDeleteConfirm.value = true;
};

const handleConfirmDelete = async () => {
  if (!roleToDelete.value) return;

  isDeleting.value = true;
  deleteError.value = '';

  try {
    const { error } = await deleteApiRolesId(roleToDelete.value.id!);

    if (error.value) {
      deleteError.value =
        error.value instanceof Error ? error.value.message : 'An error occurred while deleting the role';
      return;
    }

    showDeleteConfirm.value = false;
    roleToDelete.value = null;
    await fetchRoles();
  } finally {
    isDeleting.value = false;
  }
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

    <!-- Roles Table -->
    <UaCard v-else-if="roles && roles.length > 0" title="Available Roles">
      <div class="roles-table">
        <div class="table-header">
          <div class="col-name">Name</div>
          <div class="col-description">Description</div>
          <div class="col-actions">Actions</div>
        </div>

        <div v-for="role in roles" :key="role.id" class="table-row">
          <div class="col-name">{{ role.name }}</div>
          <div class="col-description">{{ role.description }}</div>
          <div class="col-actions">
            <UaBtn
              v-if="accessControl.hasPermission(Permissions.RolesEdit)"
              icon
              variant="text"
              size="small"
              @click="handleEditRole(role)"
              title="Edit role"
            >
              <v-icon :icon="mdiPencil" />
            </UaBtn>
            <UaBtn
              v-if="accessControl.hasPermission(Permissions.RolesExpire)"
              icon
              variant="text"
              size="small"
              color="error"
              @click="handleDeleteRole(role)"
              title="Delete role"
            >
              <v-icon :icon="mdiDelete" />
            </UaBtn>
          </div>
        </div>
      </div>
    </UaCard>

    <!-- Empty State -->
    <UaPlaceholderPage
      v-else
      title="No roles available"
      description="Create your first role to get started managing permissions."
    />

    <!-- Create/Edit Modal -->
    <RoleFormModal
      v-if="showCreateEditModal"
      :role="selectedRole"
      @close="handleRoleFormClose"
      @created="handleRoleCreated"
      @updated="handleRoleUpdated"
    />

    <!-- Delete Confirmation Modal -->
    <UaModal v-if="showDeleteConfirm" title="Delete Role" :loading="isDeleting" @close="showDeleteConfirm = false">
      <template #alerts>
        <UaAlert v-if="deleteError" type="error" @close="deleteError = ''">
          {{ deleteError }}
        </UaAlert>
      </template>

      <div class="delete-confirmation">
        <p>
          Are you sure you want to delete the role <strong>{{ roleToDelete?.name }}</strong
          >?
        </p>
        <p class="warning-text">This action cannot be undone.</p>
      </div>

      <template #actions>
        <UaBtn variant="outlined" @click="showDeleteConfirm = false" :disabled="isDeleting"> Cancel </UaBtn>
        <UaBtn color="error" variant="flat" @click="handleConfirmDelete" :loading="isDeleting"> Delete </UaBtn>
      </template>
    </UaModal>
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

.loading-state {
  padding: var(--ua-spacing-xl);
  text-align: center;
  color: var(--ua-text-secondary);
}

.roles-table {
  display: flex;
  flex-direction: column;
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  overflow: hidden;
}

.table-header {
  display: grid;
  grid-template-columns: 150px 1fr 100px;
  gap: var(--ua-spacing-md);
  padding: var(--ua-spacing-md) var(--ua-spacing-lg);
  background-color: rgb(var(--v-theme-surface-variant));
  font-weight: var(--ua-font-weight-bold);
  font-size: var(--ua-font-size-sm);
}

.table-row {
  display: grid;
  grid-template-columns: 150px 1fr 100px;
  gap: var(--ua-spacing-md);
  align-items: center;
  padding: var(--ua-spacing-md) var(--ua-spacing-lg);
  border-top: 1px solid var(--ua-border-color);

  &:hover {
    background-color: rgba(var(--v-theme-primary), 0.05);
  }
}

.col-name {
  font-weight: var(--ua-font-weight-semibold);
  word-break: break-word;
}

.col-description {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  word-break: break-word;
}

.col-actions {
  display: flex;
  gap: var(--ua-spacing-sm);
  justify-content: flex-end;
}

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
