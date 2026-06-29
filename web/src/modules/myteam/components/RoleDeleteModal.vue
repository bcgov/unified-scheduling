<script setup lang="ts">
import type { RoleAssignedUserDto, RoleDto } from '@/api-access/generated/models';
import { getApiRolesIdUsers, postApiRolesIdReassignAndDelete } from '@/api-access/generated/roles/roles';
import { PostApiRolesIdReassignAndDeleteBody } from '@/api-access/generated/roles/roles.zod';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaDataTable from '@/shared/components/UaDataTable.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import type { SelectOption } from '@/types/select';
import { validationMessages } from '@/shared/validation/validationErrors';
import { getTodayDateInputValue, toApiDateString } from '@/utils/date';
import { computed, ref } from 'vue';
import * as zod from 'zod';

const props = defineProps<{
  role: RoleDto;
  allRoles: RoleDto[];
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'deleted'): void;
}>();

const {
  data: assignedRoleUsers,
  error: assignedRoleUsersError,
  isFetching: isFetchingAssignedRoleUsers,
  execute: refetchAssignedRoleUsers,
} = getApiRolesIdUsers(props.role.id!);

const isDeleting = ref(false);
const apiError = ref('');
const formErrors = ref<Record<string, string>>({});

type ReassignFormData = Partial<zod.infer<typeof PostApiRolesIdReassignAndDeleteBody>>;

const formData = ref<ReassignFormData>({
  newRoleId: undefined,
  newRoleEffectiveDate: getTodayDateInputValue(),
  newRoleExpiryDate: null,
});

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

const availableRoleOptions = computed<SelectOption[]>(() =>
  props.allRoles
    .filter((r) => typeof r.id === 'number' && r.id !== props.role.id)
    .map((r) => ({ code: r.id!, description: r.name ?? `Role ${r.id}` })),
);

const hasAssignedUsers = computed(() => (assignedRoleUsers.value ?? []).length > 0);

const reassignSchema = PostApiRolesIdReassignAndDeleteBody.extend({
  newRoleId: PostApiRolesIdReassignAndDeleteBody.shape.newRoleId.refine((v) => v !== undefined, {
    message: validationMessages.required,
  }),
  newRoleEffectiveDate: PostApiRolesIdReassignAndDeleteBody.shape.newRoleEffectiveDate.refine((v) => !!v, {
    message: validationMessages.required,
  }),
}).superRefine((data, ctx) => {
  if (data.newRoleExpiryDate && data.newRoleEffectiveDate && data.newRoleExpiryDate <= data.newRoleEffectiveDate) {
    ctx.addIssue({
      code: 'custom',
      path: ['newRoleExpiryDate'],
      message: 'Expiry date must be after effective date.',
    });
  }
});

const getFieldErrors = (error: zod.ZodError): Record<string, string> => {
  const errors: Record<string, string> = {};
  for (const issue of error.issues) {
    const field = issue.path[0];
    if (typeof field === 'string' && !errors[field]) {
      errors[field] = issue.message;
    }
  }
  return errors;
};

const validateReassignForm = (): zod.infer<typeof reassignSchema> | null => {
  formErrors.value = {};
  const result = reassignSchema.safeParse({
    ...formData.value,
    newRoleExpiryDate: formData.value.newRoleExpiryDate || null,
  });
  if (!result.success) {
    formErrors.value = getFieldErrors(result.error);
    return null;
  }
  return result.data;
};

const deleteButtonDisabled = computed(
  () =>
    isDeleting.value ||
    isFetchingAssignedRoleUsers.value ||
    !!assignedRoleUsersError.value ||
    (hasAssignedUsers.value && availableRoleOptions.value.length === 0),
);

const handleClose = () => {
  if (!isDeleting.value) emit('close');
};

const handleConfirmDelete = async () => {
  if (deleteButtonDisabled.value) return;

  if (hasAssignedUsers.value) {
    const payload = validateReassignForm();
    if (!payload) return;

    isDeleting.value = true;
    apiError.value = '';

    try {
      const { error } = await postApiRolesIdReassignAndDelete(props.role.id!, {
        newRoleId: payload.newRoleId!,
        newRoleEffectiveDate: toApiDateString(payload.newRoleEffectiveDate!),
        newRoleExpiryDate: payload.newRoleExpiryDate ? toApiDateString(payload.newRoleExpiryDate) : null,
      });

      if (error.value) {
        const problemDetail = (error.value as { detail?: string } | null | undefined)?.detail;
        apiError.value = problemDetail || error.value.message || 'An error occurred while deleting the role';
        await refetchAssignedRoleUsers();
        return;
      }
    } catch (err) {
      apiError.value = err instanceof Error ? err.message : 'An error occurred while deleting the role';
      await refetchAssignedRoleUsers();
      return;
    } finally {
      isDeleting.value = false;
    }
  } else {
    isDeleting.value = true;
    apiError.value = '';

    try {
      const { error } = await postApiRolesIdReassignAndDelete(props.role.id!, {});
      if (error.value) {
        const problemDetail = (error.value as { detail?: string } | null | undefined)?.detail;
        apiError.value = problemDetail || error.value.message || 'An error occurred while deleting the role';
        await refetchAssignedRoleUsers();
        return;
      }
    } catch (err) {
      apiError.value = err instanceof Error ? err.message : 'An error occurred while deleting the role';
      await refetchAssignedRoleUsers();
      return;
    } finally {
      isDeleting.value = false;
    }
  }

  emit('deleted');
  emit('close');
};
</script>

<template>
  <UaModal title="Delete Role" :loading="isDeleting" tone="error" @close="handleClose">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">
        {{ apiError }}
      </UaAlert>
      <UaAlert v-if="assignedRoleUsersError" type="error" :closable="false">
        {{ assignedRoleUsersError.message }}
      </UaAlert>
    </template>

    <div class="delete-confirmation">
      <p v-if="isFetchingAssignedRoleUsers">Checking users assigned to this role...</p>

      <template v-else-if="hasAssignedUsers">
        <p>
          The role <strong>{{ role.name }}</strong> is currently assigned to
          <strong>{{ assignedRoleUsers?.length }}</strong> user(s). Before deleting, reassign them to a different role.
        </p>

        <UaDataTable
          class="assigned-users-table"
          :headers="assignedUserHeaders"
          :items="assignedRoleUsers ?? []"
          :items-per-page="-1"
          density="compact"
          hide-default-footer
        />

        <div class="reassignment-form">
          <p class="reassignment-form__heading">Reassign users to</p>

          <UaAlert v-if="availableRoleOptions.length === 0" type="warning" :closable="false">
            No other roles are available for reassignment. Create another role before deleting this one.
          </UaAlert>

          <template v-else>
            <UaSelect
              v-model="formData.newRoleId"
              label="New Role"
              :items="availableRoleOptions"
              :disabled="isDeleting"
              :error-messages="formErrors.newRoleId"
            />

            <UaTextField
              id="reassignment-effective-date"
              v-model="formData.newRoleEffectiveDate"
              label="Effective Date"
              type="date"
              :disabled="isDeleting"
              :error-messages="formErrors.newRoleEffectiveDate"
            />

            <UaTextField
              id="reassignment-expiry-date"
              v-model="formData.newRoleExpiryDate as string"
              label="Expiry Date (Optional)"
              type="date"
              :disabled="isDeleting"
              :error-messages="formErrors.newRoleExpiryDate"
            />
          </template>
        </div>
      </template>

      <template v-else-if="!assignedRoleUsersError">
        <p>
          Are you sure you want to delete the role <strong>{{ role.name }}</strong
          >?
        </p>
        <p class="warning-text">This action cannot be undone.</p>
      </template>
    </div>

    <template #actions>
      <UaBtn variant="outlined" :disabled="isDeleting" @click="handleClose">Cancel</UaBtn>
      <UaBtn
        color="error"
        variant="flat"
        :loading="isDeleting"
        :disabled="deleteButtonDisabled"
        @click="handleConfirmDelete"
      >
        {{ hasAssignedUsers ? 'Reassign and Delete' : 'Delete' }}
      </UaBtn>
    </template>
  </UaModal>
</template>

<style scoped>
.delete-confirmation p {
  margin: var(--ua-spacing-md) 0;
  color: var(--ua-text-primary);
}

.delete-confirmation strong {
  color: rgb(var(--v-theme-primary));
}

.warning-text {
  color: rgb(var(--v-theme-warning));
  font-size: var(--ua-font-size-sm);
}

.assigned-users-table {
  margin: var(--ua-spacing-md) 0;
  border: 1px solid rgb(var(--v-border-color));
  border-radius: var(--ua-border-radius);
}

.reassignment-form {
  margin-top: var(--ua-spacing-lg);
  padding: var(--ua-spacing-lg);
  background-color: rgb(var(--v-theme-surface-variant));
  border: 3px solid rgb(var(--v-theme-error));
  border-radius: var(--ua-border-radius);
  display: flex;
  flex-direction: column;
  gap: var(--ua-spacing-md);
}

.reassignment-form__heading {
  margin: 0 !important;
  font-weight: var(--ua-font-weight-bold);
  font-size: var(--ua-font-size-base);
  color: var(--ua-text-primary);
}
</style>
