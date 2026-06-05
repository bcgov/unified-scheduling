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
const formError = ref('');

const selectedRoleId = ref<number | null>(props.assignment?.roleId ?? null);
const effectiveDate = ref(toDateInputValue(props.assignment?.effectiveDate) ?? getTodayDateInputValue());
const expiryDate = ref(toDateInputValue(props.assignment?.expiryDate) ?? '');

watch(
  () => props.assignment,
  (assignment) => {
    selectedRoleId.value = assignment?.roleId ?? null;
    effectiveDate.value = toDateInputValue(assignment?.effectiveDate) ?? getTodayDateInputValue();
    expiryDate.value = toDateInputValue(assignment?.expiryDate) ?? '';
    apiError.value = '';
    formError.value = '';
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

function toDateInputValue(value?: string | null): string | null {
  if (!value) {
    return null;
  }

  const dateOnlyMatch = value.match(/^(\d{4}-\d{2}-\d{2})/);
  if (dateOnlyMatch) {
    return dateOnlyMatch[1];
  }

  const parsedDate = new Date(value);
  if (Number.isNaN(parsedDate.getTime())) {
    return null;
  }

  const year = parsedDate.getFullYear();
  const month = String(parsedDate.getMonth() + 1).padStart(2, '0');
  const day = String(parsedDate.getDate()).padStart(2, '0');

  return `${year}-${month}-${day}`;
}

function getTodayDateInputValue(): string {
  const today = new Date();
  const year = today.getFullYear();
  const month = String(today.getMonth() + 1).padStart(2, '0');
  const day = String(today.getDate()).padStart(2, '0');

  return `${year}-${month}-${day}`;
}

function toApiDate(value: string): string {
  return new Date(`${value}T00:00:00.000Z`).toISOString();
}

function validateForm(): boolean {
  formError.value = '';

  if (!selectedRoleId.value) {
    formError.value = 'Role is required.';
    return false;
  }

  if (!effectiveDate.value) {
    formError.value = 'Effective date is required.';
    return false;
  }

  if (expiryDate.value && expiryDate.value < effectiveDate.value) {
    formError.value = 'Expiry date cannot be earlier than effective date.';
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
      effectiveDate: toApiDate(effectiveDate.value),
      expiryDate: expiryDate.value ? toApiDate(expiryDate.value) : null,
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
      <UaAlert v-if="formError" type="error" @close="formError = ''">
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
        :error-messages="formError.includes('Role') ? formError : ''"
      />

      <label class="ua-form-label" for="effective-date">Effective Date</label>
      <UaTextField
        id="effective-date"
        v-model="effectiveDate"
        type="date"
        label="Effective Date"
        :error-messages="formError.includes('Effective date') ? formError : ''"
      />

      <label class="ua-form-label" for="expiry-date">Expiry Date</label>
      <UaTextField
        id="expiry-date"
        v-model="expiryDate"
        type="date"
        label="Expiry Date"
        :error-messages="formError.includes('Expiry date') ? formError : ''"
      />
    </UaFormGrid>

    <template #actions>
      <UaBtn variant="outlined" :disabled="isSaving" @click="emit('close')">Cancel</UaBtn>
      <UaBtn :loading="isSaving" @click="handleSave">Save</UaBtn>
    </template>
  </UaModal>
</template>
