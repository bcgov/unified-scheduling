<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import * as zod from 'zod';
import { postApiUsers } from '@/api-access/generated/users/users';
import { PostApiUsersBody } from '@/api-access/generated/users/users.zod';
import type { CreateUserRequest, UserResponse } from '@/api-access/generated/models';
import Select from '@/shared/components/Select.vue';
import { mapProblemDetailsValidationErrors, validationMessages } from '@/shared/validation/validationErrors';
import { useLocationsStore } from '@/stores/LocationsStore';
import { usePositionsStore } from '@/stores/PositionsStore';

const props = defineProps<{
  modelValue: boolean;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void;
  (e: 'created', user: UserResponse | null): void;
}>();

const locationsStore = useLocationsStore();
const positionsStore = usePositionsStore();

const rankOptions = positionsStore.getSelectOptions();
const homeLocationOptions = computed(() => locationsStore.getSelectOptions());
const genderOptions = [
  { code: 0, description: 'Male' },
  { code: 1, description: 'Female' },
  { code: 2, description: 'Other' },
];

const isLoading = ref(false);
const apiErrorMessage = ref('');
const formErrors = ref<Record<string, string>>({});

type CreateUserFormData = CreateUserRequest & {
  gender: number | null;
};

const createInitialFormData = (): CreateUserFormData => {
  return {
    firstName: '',
    lastName: '',
    email: '',
    idirId: null,
    isEnabled: true,
    idirName: '',
    gender: null as number | null,
    rank: '',
    badgeNumber: '',
    homeLocationId: null,
  } as CreateUserFormData;
};

const formData = ref(createInitialFormData());

// Reset form when modal opens
watch(
  () => props.modelValue,
  (newValue) => {
    if (newValue) {
      resetForm();
    }
  },
);

const resetForm = () => {
  formData.value = createInitialFormData();
  formErrors.value = {};
  apiErrorMessage.value = '';
};

const createUserFormSchema = PostApiUsersBody.extend({
  firstName: PostApiUsersBody.shape.firstName.trim().min(1, validationMessages.required),
  lastName: PostApiUsersBody.shape.lastName.trim().min(1, validationMessages.required),
  email: PostApiUsersBody.shape.email.trim().min(1, validationMessages.required).email(validationMessages.invalidEmail),
  idirName: PostApiUsersBody.shape.idirName.trim().min(1, validationMessages.required),
  rank: PostApiUsersBody.shape.rank.trim().min(1, validationMessages.required),
  badgeNumber: PostApiUsersBody.shape.badgeNumber.trim().min(1, validationMessages.required),
  homeLocationId: PostApiUsersBody.shape.homeLocationId.refine((value) => value !== null, {
    message: validationMessages.required,
  }),
  gender: zod.number({ error: validationMessages.required }),
});

const getFieldErrors = (error: zod.ZodError): Record<string, string> => {
  const errors: Record<string, string> = {};

  for (const issue of error.issues) {
    const fieldName = issue.path[0];
    if (typeof fieldName === 'string' && !errors[fieldName]) {
      errors[fieldName] = issue.message;
    }
  }

  return errors;
};

const validateForm = (): CreateUserRequest | null => {
  formErrors.value = {};

  const validationResult = createUserFormSchema.safeParse(formData.value);

  if (!validationResult.success) {
    formErrors.value = getFieldErrors(validationResult.error);
    return null;
  }

  const payload: Partial<CreateUserFormData> = { ...validationResult.data };
  delete payload.gender;
  return payload as CreateUserRequest;
};

const applyServerValidationErrors = (rawError: unknown): boolean => {
  const mappedErrors = mapProblemDetailsValidationErrors(rawError);
  if (!mappedErrors) {
    return false;
  }

  formErrors.value = mappedErrors;
  return true;
};

const handleClose = () => {
  if (!isLoading.value) {
    emit('update:modelValue', false);
  }
};

const handleSave = async () => {
  const payload = validateForm();

  if (!payload) {
    return;
  }

  isLoading.value = true;
  apiErrorMessage.value = '';

  try {
    const { data, error } = await postApiUsers(payload);

    if (error.value) {
      if (applyServerValidationErrors(error.value)) {
        return;
      }

      apiErrorMessage.value = error.value.message || 'Failed to create user';
      return;
    }

    // Emit created event with the new user data
    emit('created', data.value);

    // Close modal on success
    emit('update:modelValue', false);
  } catch (error: unknown) {
    apiErrorMessage.value = error instanceof Error ? error.message : 'An unexpected error occurred';
  } finally {
    isLoading.value = false;
  }
};
</script>

<template>
  <v-dialog
    :model-value="modelValue"
    @update:model-value="handleClose"
    width="660"
    max-width="calc(100vw - 24px)"
    content-class="create-user-dialog"
    persistent
  >
    <v-card class="create-user-modal">
      <div class="modal-header">
        <span class="modal-title">Add Member</span>
        <v-btn class="close-btn" variant="text" @click="handleClose" :disabled="isLoading">
          <v-icon icon="mdi-close" size="20" class="mr-1" />
          Close
        </v-btn>
      </div>
      <div class="header-strip" />

      <v-card-text class="modal-body">
        <v-alert v-if="apiErrorMessage" type="error" density="compact" class="mb-4">
          {{ apiErrorMessage }}
        </v-alert>

        <div class="form-grid">
          <label class="row-label" for="first-name">First Name</label>
          <v-text-field
            id="first-name"
            v-model="formData.firstName"
            placeholder="First Name"
            hide-details="auto"
            :error-messages="formErrors.firstName"
            :disabled="isLoading"
          />

          <label class="row-label" for="last-name">Last Name</label>
          <v-text-field
            id="last-name"
            v-model="formData.lastName"
            placeholder="Last Name"
            hide-details="auto"
            :error-messages="formErrors.lastName"
            :disabled="isLoading"
          />

          <label class="row-label" for="email">Email</label>
          <v-text-field
            id="email"
            v-model="formData.email"
            type="email"
            placeholder="Email"
            hide-details="auto"
            :error-messages="formErrors.email"
            :disabled="isLoading"
          />

          <label class="row-label" for="idir-name">IDIR Name</label>
          <v-text-field
            id="idir-name"
            v-model="formData.idirName"
            placeholder="IDIR Name"
            hide-details="auto"
            :error-messages="formErrors.idirName"
            :disabled="isLoading"
          />

          <label class="row-label" for="gender">Gender</label>
          <Select
            id="gender"
            v-model="formData.gender"
            label="Gender"
            :items="genderOptions"
            :error-messages="formErrors.gender"
          />

          <label class="row-label" for="rank">Rank</label>
          <Select
            id="rank"
            v-model="formData.rank"
            label="Rank"
            :items="rankOptions"
            :error-messages="formErrors.rank"
          />

          <label class="row-label" for="badge-number">Badge Number</label>
          <v-text-field
            id="badge-number"
            v-model="formData.badgeNumber"
            placeholder="Badge Number"
            hide-details="auto"
            :error-messages="formErrors.badgeNumber"
            :disabled="isLoading"
          />

          <label class="row-label" for="home-location">Home Location</label>
          <Select
            id="home-location"
            v-model="formData.homeLocationId"
            label="Home Location"
            :items="homeLocationOptions"
            :error-messages="formErrors.homeLocationId"
          />

          <span class="row-label">Is enabled</span>
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
        </div>
      </v-card-text>

      <v-card-actions class="modal-actions">
        <v-btn class="action-btn" variant="outlined" @click="handleClose" :disabled="isLoading">Close</v-btn>
        <v-btn class="action-btn" color="primary" variant="flat" @click="handleSave" :loading="isLoading">
          Add Member
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.create-user-modal {
  width: 100%;
  border-radius: 12px;
  overflow: hidden;
  background: #e9e9eb;
  display: flex;
  flex-direction: column;
  max-height: calc(100vh - 48px);
}

:deep(.create-user-dialog) {
  width: min(660px, calc(100vw - 24px));
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.6rem 2.2rem;
  background: #5f8f2c;
  color: #fff;
}

.modal-title {
  font-size: 1.15rem;
  font-weight: 700;
}

.close-btn {
  text-transform: none;
  color: #fff;
  font-size: 1.15rem;
  font-weight: 500;
  letter-spacing: 0;
}

.header-strip {
  background: #d0d0d2;
}

.modal-body {
  padding: 1.4rem;
  overflow-y: auto;
  flex: 1 1 auto;
  min-height: 0;
}

.form-grid {
  display: grid;
  grid-template-columns: 210px 1fr;
  gap: 1rem 1rem;
  margin-top: 1rem;
}

.row-label {
  font-size: 1.15rem;
  font-weight: 700;
  color: #1b2740;
}

.modal-actions {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.8rem;
  padding: 1.75rem 2.35rem 2.15rem;
}

.action-btn {
  font-size: 1.15rem;
  text-transform: none;
  letter-spacing: 0;
}

.action-btn:last-child {
  background: #b6b6b8;
  color: #1f2a44;
}

:deep(.v-field) {
  border-radius: 8px;
  background: #efeff1;
}

:deep(.v-field__input) {
  color: #1f2a44;
}

.toggle-wrapper {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
}

.toggle-label {
  font-size: 1rem;
  color: #1f2a44;
}

.field-error {
  font-size: 0.75rem;
  color: rgb(var(--v-theme-error));
  margin-top: 0.25rem;
}

.enabled-switch {
  margin: 0;
}

:deep(.enabled-switch .v-selection-control) {
  min-height: 32px;
}
</style>
