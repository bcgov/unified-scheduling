<script setup lang="ts">
import { type RoleDto, type UserRoleResponseDto } from '@/api-access/generated/models';
import { postApiUsersIdRoles } from '@/api-access/generated/users/users';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import type { SelectOption } from '@/types/select';
import { getTodayDateInputValue, isDateInputBefore, toApiDateString, toDateInputValue } from '@/utils/date';
import { computed, ref, watch } from 'vue';

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
const formErrors = ref({
  role: '',
  effectiveDate: '',
  expiryDate: '',
});

const selectedRoleId = ref<number | null>(props.assignment?.roleId ?? null);
const effectiveDate = ref(toDateInputValue(props.assignment?.effectiveDate) ?? getTodayDateInputValue());
const expiryDate = ref(toDateInputValue(props.assignment?.expiryDate) ?? '');

const formError = computed(() => Object.values(formErrors.value).find((message) => message.length > 0) ?? '');

function clearFormErrors(): void {
  formErrors.value = {
    role: '',
    effectiveDate: '',
    expiryDate: '',
  };
}

watch(
  () => props.assignment,
  (assignment) => {
    selectedRoleId.value = assignment?.roleId ?? null;
    effectiveDate.value = toDateInputValue(assignment?.effectiveDate) ?? getTodayDateInputValue();
    expiryDate.value = toDateInputValue(assignment?.expiryDate) ?? '';
    apiError.value = '';
    clearFormErrors();
  },
  { immediate: true },
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

function validateForm(): boolean {
  clearFormErrors();

  if (!selectedRoleId.value) {
    formErrors.value.role = 'Role is required.';
    return false;
  }

  if (!effectiveDate.value) {
    formErrors.value.effectiveDate = 'Effective date is required.';
    return false;
  }

  if (expiryDate.value && isDateInputBefore(expiryDate.value, effectiveDate.value)) {
    formErrors.value.expiryDate = 'Expiry date cannot be earlier than effective date.';
    return false;
  }

  return true;
}

const handleSave = async () => {
  if (!validateForm()) {
    return;
  }

  isSaving.value = true;
  apiError.value = '';

  try {
    const { error } = await postApiUsersIdRoles(props.userId, {
      roleId: selectedRoleId.value!,
      effectiveDate: toApiDateString(effectiveDate.value),
      expiryDate: expiryDate.value ? toApiDateString(expiryDate.value) : null,
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
      <UaAlert v-if="formError" type="error" @close="clearFormErrors()">
        {{ formError }}
      </UaAlert>
    </template>

    <UaFormGrid>
      <label class="ua-form-label" for="assign-role">Role</label>
      <UaSelect
        id="assign-role"
        v-model="selectedRoleId"
        label="Role"
        :items="roleOptions"
        :disabled="isEditMode"
        :error-messages="formErrors.role"
      />

      <UaTextField
        id="effective-date"
        v-model="effectiveDate"
        type="date"
        label="Effective Date"
        :error-messages="formErrors.effectiveDate"
      />

      <UaTextField
        id="expiry-date"
        v-model="expiryDate"
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
