<script setup lang="ts">
import type { PermissionDto, RoleDto, RoleRequestDto, UpdateRoleRequestDto } from '@/api-access/generated/models';
import { getApiPermissions } from '@/api-access/generated/permissions/permissions';
import { postApiRoles, putApiRolesId } from '@/api-access/generated/roles/roles';
import { PostApiRolesBody } from '@/api-access/generated/roles/roles.zod';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import UaTextarea from '@/shared/components/UaTextarea.vue';
import { validationMessages } from '@/shared/validation/validationErrors';
import { mdiClose, mdiContentSave } from '@mdi/js';
import { computed, ref, watch } from 'vue';
import * as zod from 'zod';

const props = defineProps<{
  /** When provided, the modal operates in edit mode */
  role?: RoleDto | null;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'created', role: RoleDto | null): void;
  (e: 'updated', role: RoleDto | null): void;
}>();

// Fetch permissions from API
const { data: allPermissions, error: permissionsError, isFetching: isFetchingPermissions } = getApiPermissions();

const isEditMode = computed(() => !!props.role);
const isLoading = ref(false);
const apiErrorMessage = ref('');
const formErrors = ref<Record<string, string>>({});
const selectedPermissions = ref<Map<string, boolean>>(new Map());

const defaultPermissionGroup = 'Other';

const permissionsList = computed(() => (Array.isArray(allPermissions.value) ? allPermissions.value : []));

const getPermissionGroup = (permission: PermissionDto): string => {
  const group = permission.group?.trim();
  return group ? group : defaultPermissionGroup;
};

const groupedPermissions = computed(() => {
  const grouped = new Map<string, PermissionDto[]>();
  const visiblePermissions = permissionsList.value;

  for (const permission of visiblePermissions) {
    const groupName = getPermissionGroup(permission);
    const existingGroup = grouped.get(groupName) ?? [];
    existingGroup.push(permission);
    grouped.set(groupName, existingGroup);
  }

  for (const permissions of grouped.values()) {
    permissions.sort((a, b) => {
      const labelA = (a.description || a.id || '').toLowerCase();
      const labelB = (b.description || b.id || '').toLowerCase();
      return labelA.localeCompare(labelB);
    });
  }

  return Array.from(grouped.entries())
    .sort(([groupA], [groupB]) => groupA.localeCompare(groupB))
    .map(([groupName, permissions]) => ({
      checkedCount: permissions.filter((permission) => selectedPermissions.value.get(permission.id ?? '')).length,
      groupName,
      groupLabel: groupName,
      permissions,
    }));
});

const permissionsByGroupLabel = computed(
  () => new Map(groupedPermissions.value.map((group) => [group.groupLabel, group.permissions])),
);

const totalPermissionCount = computed(() => permissionsList.value.length);

const selectedPermissionCount = computed(
  () => permissionsList.value.filter((permission) => selectedPermissions.value.get(permission.id ?? '')).length,
);

const permissionTableGroupBy = ref([{ key: 'groupLabel', order: 'asc' as const }]);

const permissionTableSortBy = ref([{ key: 'id', order: 'asc' as const }]);

const permissionTableHeaders = [
  { title: 'Group', key: 'data-table-group' },
  { title: '', key: 'permissionToggle', groupable: false, align: 'center' as const, width: 72 },
  { title: 'Permission', key: 'id', groupable: false },
  { title: 'Description', key: 'description', groupable: false },
];

const permissionTableItems = computed(() =>
  groupedPermissions.value.flatMap((group) => {
    return group.permissions.map((permission) => ({
      groupLabel: group.groupLabel,
      id: permission.id ?? '',
      description: permission.description || permission.id || '',
    }));
  }),
);

type RoleFormData = Partial<RoleRequestDto & { id?: number; concurrencyToken?: number }>;

const createInitialFormData = (): RoleFormData => ({
  name: '',
  description: '',
  permissionIds: [],
});

const populateFromRole = (role: RoleDto): RoleFormData => ({
  id: role.id,
  name: role.name ?? '',
  description: role.description ?? '',
  permissionIds: role.permissions?.map((p) => p.id).filter((id): id is string => Boolean(id)) ?? [],
  concurrencyToken: role.concurrencyToken,
});

const formData = ref<RoleFormData>(props.role ? populateFromRole(props.role) : createInitialFormData());

// Initialize selected permissions when permissions or role data changes
const initializeSelectedPermissions = () => {
  selectedPermissions.value.clear();

  if (permissionsError.value) return;

  permissionsList.value.forEach((perm) => {
    const isSelected = formData.value.permissionIds?.includes(perm.id!) || false;
    selectedPermissions.value.set(perm.id!, isSelected);
  });
};

watch(
  () => props.role,
  (role) => {
    formData.value = role ? populateFromRole(role) : createInitialFormData();
    initializeSelectedPermissions();
  },
  { immediate: true },
);

watch(allPermissions, () => {
  initializeSelectedPermissions();
});

const roleFormSchema = PostApiRolesBody.extend({
  name: PostApiRolesBody.shape.name.min(1, 'Role name is required'),
  description: PostApiRolesBody.shape.description.min(1, 'Description is required'),
  permissionIds: PostApiRolesBody.shape.permissionIds.refine(
    (value) => Array.isArray(value) && value.length > 0,
    'At least one permission must be selected',
  ),
});

const getFieldErrors = (error: zod.ZodError): Record<string, string> => {
  const errors: Record<string, string> = {};

  for (const issue of error.issues) {
    const fieldName = issue.path[0];
    if (typeof fieldName !== 'string' || errors[fieldName]) {
      continue;
    }

    if (issue.code === 'invalid_type' || issue.code === 'invalid_value') {
      errors[fieldName] = validationMessages.required;
      continue;
    }

    errors[fieldName] = issue.message;
  }

  return errors;
};

const validateForm = (selectedPermissionIds: string[]): RoleRequestDto | null => {
  formErrors.value = {};
  const validationResult = roleFormSchema.safeParse({
    name: formData.value.name ?? '',
    description: formData.value.description ?? '',
    permissionIds: selectedPermissionIds,
  });

  if (!validationResult.success) {
    formErrors.value = getFieldErrors(validationResult.error);
    if (formErrors.value['permissionIds']) {
      formErrors.value['permissions'] = formErrors.value['permissionIds'];
      delete formErrors.value['permissionIds'];
    }
    return null;
  }

  return validationResult.data;
};

const handleClose = () => {
  if (!isLoading.value) {
    emit('close');
  }
};

const setPermissionSelection = (permissionId: string, isSelected: boolean) => {
  selectedPermissions.value.set(permissionId, isSelected);
};

const setGroupSelection = (groupLabel: string, isSelected: boolean) => {
  const permissions = permissionsByGroupLabel.value.get(groupLabel) ?? [];
  for (const permission of permissions) {
    if (!permission.id) continue;
    selectedPermissions.value.set(permission.id, isSelected);
  }
};

const getPermissionsForGroupLabel = (groupLabel: string): PermissionItem[] =>
  permissionsByGroupLabel.value.get(groupLabel) ?? [];

const getGroupSelectionSummary = (groupLabel: string): string => {
  const permissions = getPermissionsForGroupLabel(groupLabel);
  const selectedCount = permissions.filter((permission) => selectedPermissions.value.get(permission.id ?? '')).length;
  return `(${selectedCount} / ${permissions.length} selected)`;
};

const isGroupFullySelected = (groupLabel: string): boolean => {
  const permissions = getPermissionsForGroupLabel(groupLabel);
  return (
    permissions.length > 0 && permissions.every((permission) => selectedPermissions.value.get(permission.id ?? ''))
  );
};

const isGroupPartiallySelected = (groupLabel: string): boolean => {
  const permissions = getPermissionsForGroupLabel(groupLabel);
  const selectedCount = permissions.filter((permission) => selectedPermissions.value.get(permission.id ?? '')).length;
  return selectedCount > 0 && selectedCount < permissions.length;
};

const handleSave = async () => {
  // Collect selected permission IDs
  const selectedPermissionIds = Array.from(selectedPermissions.value.entries())
    .filter(([, checked]) => checked)
    .map(([id]) => id);

  const payload = validateForm(selectedPermissionIds);
  if (!payload) return;

  isLoading.value = true;
  apiErrorMessage.value = '';

  try {
    if (isEditMode.value && formData.value.id) {
      // --- Edit mode ---
      const updateDto: UpdateRoleRequestDto = {
        id: formData.value.id,
        name: formData.value.name || '',
        description: formData.value.description || '',
        permissionIds: selectedPermissionIds,
        concurrencyToken: formData.value.concurrencyToken || 0,
      };

      const { data, error } = await putApiRolesId(formData.value.id, updateDto);
      if (error.value) {
        apiErrorMessage.value = error.value.message || 'Failed to update role';
        return;
      }

      emit('updated', data.value ?? null);
    } else {
      // --- Create mode ---
      const createDto: RoleRequestDto = {
        ...payload,
      };

      const { data, error } = await postApiRoles(createDto);
      if (error.value) {
        apiErrorMessage.value = error.value.message || 'Failed to create role';
        return;
      }

      emit('created', data.value ?? null);
    }

    emit('close');
  } catch (err: unknown) {
    apiErrorMessage.value = err instanceof Error ? err.message : 'An unexpected error occurred';
  } finally {
    isLoading.value = false;
  }
};
</script>

<template>
  <UaModal :title="isEditMode ? 'Edit Role' : 'Create Role'" :loading="isLoading" @close="handleClose">
    <template #alerts>
      <UaAlert v-if="apiErrorMessage" type="error" @close="apiErrorMessage = ''">
        Request failed: {{ apiErrorMessage }}
      </UaAlert>
    </template>

    <UaFormGrid>
      <UaTextField
        id="role-name"
        label="Role Name"
        :model-value="formData.name"
        :error-messages="formErrors.name"
        :disabled="isLoading"
        @update:model-value="(v: string) => (formData.name = v)"
      />

      <UaTextarea
        id="role-description"
        label="Description"
        :model-value="formData.description"
        :error-messages="formErrors.description"
        :disabled="isLoading"
        @update:model-value="(v: string) => (formData.description = v)"
      />

      <!-- Permissions Section -->
      <div class="permissions-section">
        <div class="permissions-header">
          <label class="ua-form-label">Permissions</label>
          <p class="permissions-counter">{{ selectedPermissionCount }} / {{ totalPermissionCount }} selected</p>
        </div>
        <div class="permissions-list">
          <div v-if="permissionsError" class="error-message">
            Failed to load permissions: {{ permissionsError.message }}
          </div>
          <div v-else-if="isFetchingPermissions" class="no-permissions-message">Loading permissions...</div>
          <div v-else-if="!permissionsError && groupedPermissions.length === 0" class="no-permissions-message">
            No permissions available.
          </div>

          <!-- <p class="permission-table-title">Permissions by module</p> -->
          <v-data-table
            v-else
            :headers="permissionTableHeaders"
            :items="permissionTableItems"
            :group-by="permissionTableGroupBy"
            :sort-by="permissionTableSortBy"
            item-value="id"
            :items-per-page="-1"
            density="comfortable"
            hide-default-header
            hide-default-footer
            class="permission-table"
          >
            <template #group-header="{ item, columns, toggleGroup, isGroupOpen }">
              <tr>
                <td :colspan="columns.length" class="permission-group-header-cell">
                  <div class="permission-group-header-row">
                    <button
                      type="button"
                      class="permission-group-toggle-btn"
                      :aria-label="`Toggle ${item.value} group`"
                      @click="toggleGroup(item)"
                    >
                      {{ isGroupOpen(item) ? '▾' : '▸' }}
                    </button>
                    <input
                      type="checkbox"
                      :id="`table-group-${item.value}`"
                      :checked="isGroupFullySelected(item.value)"
                      :indeterminate.prop="isGroupPartiallySelected(item.value)"
                      :disabled="isLoading"
                      @change="setGroupSelection(item.value, ($event.target as HTMLInputElement).checked)"
                    />
                    <label :for="`table-group-${item.value}`" class="permission-group-label">{{ item.value }}</label>
                    <span class="permission-group-summary">{{ getGroupSelectionSummary(item.value) }}</span>
                  </div>
                </td>
              </tr>
            </template>

            <template #[`item.permissionToggle`]="{ item }">
              <div class="permission-table-permission-toggle">
                <input
                  type="checkbox"
                  :id="`table-perm-${item.id}`"
                  :checked="selectedPermissions.get(item.id) || false"
                  :disabled="isLoading"
                  @change="setPermissionSelection(item.id, ($event.target as HTMLInputElement).checked)"
                />
              </div>
            </template>
          </v-data-table>
        </div>
        <div v-if="formErrors.permissions" class="error-message">{{ formErrors.permissions }}</div>
      </div>
    </UaFormGrid>

    <template #actions>
      <UaBtn variant="outlined" @click="handleClose" :disabled="isLoading" :prepend-icon="mdiClose">Cancel</UaBtn>
      <UaBtn color="primary" variant="flat" @click="handleSave" :loading="isLoading" :prepend-icon="mdiContentSave">
        {{ isEditMode ? 'Save Changes' : 'Create Role' }}
      </UaBtn>
    </template>
  </UaModal>
</template>

<style scoped>
.ua-form-label {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
}

.permissions-section {
  grid-column: 1 / -1;
}

.permissions-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--ua-spacing-md);
}

.permissions-list {
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-md);
  padding: var(--ua-spacing-md);
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  background-color: rgb(var(--v-theme-surface));
  min-height: 150px;
}

.permissions-counter {
  margin: var(--ua-spacing-xs) 0 var(--ua-spacing-sm);
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  text-align: right;
}

.no-permissions-message {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
  text-align: center;
  padding: var(--ua-spacing-md);
}

.permission-table-wrapper {
  margin-top: var(--ua-spacing-xs);
}

.permission-table-title {
  margin: 0 0 var(--ua-spacing-sm);
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
}

.permission-table {
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius-sm);
}

.permission-group-header-cell {
  background-color: rgba(var(--v-theme-surface-variant), 0.35);
}

.permission-group-header-row {
  display: flex;
  align-items: center;
  gap: var(--ua-spacing-sm);
  padding: var(--ua-spacing-sm) var(--ua-spacing-xs);
}

.permission-group-toggle-btn {
  border: none;
  background: transparent;
  cursor: pointer;
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-base);
  line-height: 1;
  padding: 2px 4px;
}

.permission-group-label {
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
}

.permission-group-summary {
  color: var(--ua-text-secondary);
  font-size: var(--ua-font-size-sm);
}

.permission-table-permission-toggle {
  display: flex;
  justify-content: center;
}

.permission-table-permission-toggle input[type='checkbox'] {
  width: 18px;
  height: 18px;
  cursor: pointer;
}

.permission-table-permission-toggle input[type='checkbox']:disabled,
.permission-group-header-row input[type='checkbox']:disabled {
  cursor: not-allowed;
  opacity: 0.5;
}

.error-message {
  color: rgb(var(--v-theme-error));
  font-size: var(--ua-font-size-sm);
  margin-top: var(--ua-spacing-sm);
}
</style>
