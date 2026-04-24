<script setup lang="ts">
import { Gender, LookupCodeTypes, type UserRequestDto, type UserResponse } from '@/api-access/generated/models';
import { postApiUsers, putApiUsersId } from '@/api-access/generated/users/users';
import { PostApiUsersBody } from '@/api-access/generated/users/users.zod';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import Select from '@/shared/components/Select.vue';
import { mapToValidationErrors, validationMessages } from '@/shared/validation/validationErrors';
import { useLocationsStore } from '@/stores/LocationsStore';
import { useLookupStore } from '@/stores/LookupStore';
import { mapToSelectOptions } from '@/utils/select';
import { computed, onMounted, ref } from 'vue';
import * as zod from 'zod';

const props = defineProps<{
  /** When provided, the modal operates in edit mode */
  user?: UserResponse | null;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'created', user: UserResponse | null): void;
  (e: 'updated', user: UserResponse | null): void;
}>();

const locationsStore = useLocationsStore();
const lookupStore = useLookupStore();

onMounted(async () => {
  await lookupStore.load(LookupCodeTypes.PositionTypes);
});

const isEditMode = computed(() => !!props.user);

const positionTypeOptions = computed(() => lookupStore.getSelectOptions(LookupCodeTypes.PositionTypes));
const homeLocationOptions = computed(() => locationsStore.getSelectOptions());
const genderOptions = mapToSelectOptions(
  Object.values(Gender),
  (gender) => gender,
  (gender) => gender,
);

const isLoading = ref(false);
const apiErrorMessage = ref('');
const formErrors = ref<Record<string, string>>({});

type UserRequestFormData = Partial<UserRequestDto>;

const createInitialFormData = (): UserRequestFormData => ({});

const populateFromUser = (user: UserResponse): UserRequestFormData => ({
  firstName: user.firstName ?? '',
  lastName: user.lastName ?? '',
  email: user.email ?? '',
  isEnabled: user.isEnabled,
  idirName: user.idirName ?? '',
  gender: user.gender,
  rank: user.rank ?? '',
  badgeNumber: user.badgeNumber ?? '',
  homeLocationId: user.homeLocationId ?? undefined,
});

const formData = ref<UserRequestFormData>(props.user ? populateFromUser(props.user) : createInitialFormData());

const createUserFormSchema = PostApiUsersBody.extend({
  firstName: PostApiUsersBody.shape.firstName.min(1, validationMessages.required),
  lastName: PostApiUsersBody.shape.lastName.min(1, validationMessages.required),
  email: PostApiUsersBody.shape.email.min(1, validationMessages.required).email(validationMessages.invalidEmail),
  idirName: PostApiUsersBody.shape.idirName.min(1, validationMessages.required),
  rank: PostApiUsersBody.shape.rank.min(1, validationMessages.required),
  homeLocationId: PostApiUsersBody.shape.homeLocationId.refine((value) => value !== undefined, {
    message: validationMessages.required,
  }),
  gender: PostApiUsersBody.shape.gender.refine((value) => !!value, {
    message: validationMessages.required,
  }),
});

// In edit mode idirName is not editable so we relax that requirement
const editUserFormSchema = createUserFormSchema.extend({});

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

const validateForm = (): UserRequestDto | null => {
  formErrors.value = {};
  const schema = isEditMode.value ? editUserFormSchema : createUserFormSchema;
  const validationResult = schema.safeParse(formData.value);

  if (!validationResult.success) {
    formErrors.value = getFieldErrors(validationResult.error);
    return null;
  }

  return validationResult.data;
};

const applyServerValidationErrors = (rawError: unknown): boolean => {
  const mappedErrors = mapToValidationErrors(rawError);
  if (!mappedErrors) return false;
  formErrors.value = mappedErrors;
  return true;
};

const handleClose = () => {
  if (!isLoading.value) {
    emit('close');
  }
};

const handleSave = async () => {
  const payload = validateForm();
  if (!payload) return;

  isLoading.value = true;
  apiErrorMessage.value = '';

  try {
    if (isEditMode.value && props.user?.id) {
      // --- Edit mode ---
      const { data, error } = await putApiUsersId(props.user.id, payload);

      if (error.value) {
        if (applyServerValidationErrors(data.value)) return;
        apiErrorMessage.value = error.value.message || 'Failed to update user';
        return;
      }

      emit('updated', data.value);
    } else {
      // --- Create mode ---
      const { data, error } = await postApiUsers(payload);

      if (error.value) {
        if (applyServerValidationErrors(data.value)) return;
        apiErrorMessage.value = error.value.message || 'Failed to create user';
        return;
      }

      emit('created', data.value);
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
  <UaModal :title="isEditMode ? 'Edit Member' : 'Add Member'" :loading="isLoading" @close="handleClose">
    <template #alerts>
      <UaAlert v-if="apiErrorMessage" type="error" @close="apiErrorMessage = ''">
        Request failed: {{ apiErrorMessage }}
      </UaAlert>
    </template>

    <UaFormGrid>
      <UaTextField
        id="first-name"
        label="First Name"
        :model-value="formData.firstName"
        :error-messages="formErrors.firstName"
        :disabled="isLoading"
        @update:model-value="(v: string) => (formData.firstName = v)"
      />

      <UaTextField
        id="last-name"
        label="Last Name"
        :model-value="formData.lastName"
        :error-messages="formErrors.lastName"
        :disabled="isLoading"
        @update:model-value="(v: string) => (formData.lastName = v)"
      />

      <UaTextField
        id="email"
        label="Email"
        type="email"
        :model-value="formData.email"
        :error-messages="formErrors.email"
        :disabled="isLoading"
        @update:model-value="(v: string) => (formData.email = v)"
      />

      <UaTextField
        id="idir-name"
        label="IDIR Name"
        :model-value="formData.idirName"
        :error-messages="formErrors.idirName"
        :disabled="isLoading"
        @update:model-value="(v: string) => (formData.idirName = v)"
      />

      <label class="ua-form-label" for="gender">Gender</label>
      <Select
        id="gender"
        v-model="formData.gender"
        label="Gender"
        :items="genderOptions"
        :error-messages="formErrors.gender"
      />

      <label class="ua-form-label" for="rank">Rank</label>
      <Select
        id="rank"
        v-model="formData.rank"
        label="Rank"
        :items="positionTypeOptions"
        :error-messages="formErrors.rank"
      />

      <UaTextField
        id="badge-number"
        label="Badge Number"
        :model-value="formData.badgeNumber"
        :error-messages="formErrors.badgeNumber"
        :disabled="isLoading"
        @update:model-value="(v: string) => (formData.badgeNumber = v)"
      />

      <label class="ua-form-label" for="home-location">Home Location</label>
      <Select
        id="home-location"
        v-model="formData.homeLocationId"
        label="Home Location"
        :items="homeLocationOptions"
        :error-messages="formErrors.homeLocationId"
      />

      <span class="ua-form-label">Is enabled</span>
      <div class="toggle-wrapper">
        <span class="toggle-label">Inactive</span>
        <v-switch
          v-model="formData.isEnabled"
          inset
          color="success"
          hide-details
          class="enabled-switch"
          :disabled="isLoading"
        />
        <span class="toggle-label">Active</span>
      </div>
    </UaFormGrid>

    <template #actions>
      <v-btn variant="outlined" @click="handleClose" :disabled="isLoading">Close</v-btn>
      <v-btn color="primary" variant="flat" @click="handleSave" :loading="isLoading">
        {{ isEditMode ? 'Save Changes' : 'Add Member' }}
      </v-btn>
    </template>
  </UaModal>
</template>

<style scoped>
.ua-form-label {
  font-size: var(--ua-font-size-lg);
  font-weight: var(--ua-font-weight-bold);
  color: var(--ua-text-primary);
}

:deep(.v-field) {
  border-radius: var(--ua-border-radius);
  background: var(--ua-field-bg);
}

:deep(.v-field__input) {
  color: var(--ua-text-primary);
}

.toggle-wrapper {
  display: inline-flex;
  align-items: center;
  gap: var(--ua-spacing-sm);
}

.toggle-label {
  font-size: var(--ua-font-size-base);
  color: var(--ua-text-primary);
}

.enabled-switch {
  margin: 0;
}

:deep(.enabled-switch .v-selection-control) {
  min-height: 32px;
}
</style>
