<script setup lang="ts">
import { type RoleDto, type UserRoleResponseDto } from '@/api-access/generated/models';
import { postApiUsersIdRoles } from '@/api-access/generated/users/users';
import { PostApiUsersIdRolesBody } from '@/api-access/generated/users/users.zod';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import type { SelectOption } from '@/types/select';
import { validationMessages } from '@/shared/validation/validationErrors';
import { getTodayDateInputValue, isDateInputBefore, toApiDateString, toDateInputValue } from '@/utils/date';
import { computed, ref, watch } from 'vue';
import * as zod from 'zod';

const props = defineProps<{
  userId: string;
  roles: RoleDto[];
  assignment?: UserRoleResponseDto | null;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'saved'): void;
}>();

const isEditMode = computed(() => !!props.assignment);
const isSaving = ref(false);
const apiError = ref('');
const formErrors = ref<Record<string, string>>({});

type AssignRoleFormData = Partial<zod.infer<typeof PostApiUsersIdRolesBody>>;

const createInitialFormData = (): AssignRoleFormData => ({
  roleId: undefined,
  effectiveDate: getTodayDateInputValue(),
  expiryDate: null,
});

const populateFromAssignment = (a: UserRoleResponseDto): AssignRoleFormData => ({
  roleId: a.roleId ?? undefined,
  effectiveDate: toDateInputValue(a.effectiveDate) ?? getTodayDateInputValue(),
  expiryDate: toDateInputValue(a.expiryDate) ?? null,
});

const formData = ref<AssignRoleFormData>(
  props.assignment ? populateFromAssignment(props.assignment) : createInitialFormData(),
);

const assignRoleSchema = PostApiUsersIdRolesBody.extend({
  roleId: PostApiUsersIdRolesBody.shape.roleId.refine((v) => v !== undefined && v !== null, {
    message: validationMessages.required,
  }),
  effectiveDate: PostApiUsersIdRolesBody.shape.effectiveDate.min(1, validationMessages.required),
}).superRefine((data, ctx) => {
  if (data.expiryDate && data.effectiveDate && isDateInputBefore(data.expiryDate, data.effectiveDate)) {
    ctx.addIssue({
      code: 'custom',
      path: ['expiryDate'],
      message: 'Expiry date cannot be earlier than effective date.',
    });
  }
});

const getFieldErrors = (error: zod.ZodError): Record<string, string> => {
  const errors: Record<string, string> = {};
  for (const issue of error.issues) {
    const fieldName = issue.path[0];
    if (typeof fieldName === 'string' && !errors[fieldName]) {
      if (issue.code === 'invalid_type' || issue.code === 'invalid_value') {
        errors[fieldName] = validationMessages.required;
        continue;
      }
      errors[fieldName] = issue.message;
    }
  }
  return errors;
};

watch(
  () => props.assignment,
  (assignment) => {
    formData.value = assignment ? populateFromAssignment(assignment) : createInitialFormData();
    apiError.value = '';
    formErrors.value = {};
  },
);

const roleOptions = computed<SelectOption[]>(() =>
  props.roles
    .filter((role) => typeof role.id === 'number' && role.name)
    .map((role) => ({
      code: role.id as number,
      description: role.name as string,
    })),
);

const modalTitle = computed(() => (isEditMode.value ? 'Edit Role Assignment' : 'Assign Role'));

function validateForm(): zod.infer<typeof assignRoleSchema> | null {
  formErrors.value = {};
  const result = assignRoleSchema.safeParse({
    ...formData.value,
    expiryDate: formData.value.expiryDate || null,
  });
  if (!result.success) {
    formErrors.value = getFieldErrors(result.error);
    return null;
  }
  return result.data;
}

const handleSave = async () => {
  const payload = validateForm();
  if (!payload) return;

  isSaving.value = true;
  apiError.value = '';

  try {
    const { error } = await postApiUsersIdRoles(props.userId, {
      roleId: payload.roleId,
      effectiveDate: toApiDateString(payload.effectiveDate),
      expiryDate: payload.expiryDate ? toApiDateString(payload.expiryDate) : null,
    });

    if (error.value) {
      apiError.value = error.value.message || 'Failed to save role assignment.';
      return;
    }

    emit('saved');
    emit('close');
  } catch (err: unknown) {
    apiError.value = err instanceof Error ? err.message : 'Failed to save role assignment.';
  } finally {
    isSaving.value = false;
  }
};
</script>

<template>
  <UaModal :title="modalTitle" :loading="isSaving" @close="emit('close')">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">
        {{ apiError }}
      </UaAlert>
    </template>

    <UaFormGrid>
      <label class="ua-form-label" for="assign-role">Role</label>
      <UaSelect
        id="assign-role"
        v-model="formData.roleId"
        label="Role"
        :items="roleOptions"
        :disabled="isEditMode"
        :error-messages="formErrors.roleId"
      />

      <UaTextField
        id="effective-date"
        v-model="formData.effectiveDate"
        type="date"
        label="Effective Date"
        :error-messages="formErrors.effectiveDate"
      />

      <UaTextField
        id="expiry-date"
        v-model="formData.expiryDate as string"
        type="date"
        label="Expiry Date"
        :error-messages="formErrors.expiryDate"
      />
    </UaFormGrid>

    <template #actions>
      <UaBtn variant="outlined" :disabled="isSaving" @click="emit('close')">Cancel</UaBtn>
      <UaBtn :loading="isSaving" @click="handleSave">Save</UaBtn>
    </template>
  </UaModal>
</template>
