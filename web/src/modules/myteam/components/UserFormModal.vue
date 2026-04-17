<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import * as zod from 'zod';
import { postApiUsers, putApiUsersId } from '@/api-access/generated/users/users';
import { PostApiUsersBody } from '@/api-access/generated/users/users.zod';
import { Gender, LookupCodeTypes, type UserRequestDto, type UserResponse } from '@/api-access/generated/models';
import Select from '@/shared/components/Select.vue';
import { mapToValidationErrors, validationMessages } from '@/shared/validation/validationErrors';
import { useLocationsStore } from '@/stores/LocationsStore';
import { useLookupStore } from '@/stores/LookupStore';
import { mapToSelectOptions } from '@/utils/select';

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

const handleDialogVisibility = (isVisible: boolean) => {
  if (!isVisible) {
    handleClose();
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
  <v-dialog
    :model-value="true"
    @update:model-value="handleDialogVisibility"
    width="660"
    max-width="calc(100vw - 24px)"
    content-class="user-form-dialog-content"
    persistent
  >
    <v-card class="user-form-modal">
      <div class="modal-header">
        <span class="modal-title">{{ isEditMode ? 'Edit Member' : 'Add Member' }}</span>
        <v-btn class="close-btn" variant="text" @click="handleClose" :disabled="isLoading">
          <v-icon icon="mdi-close" size="20" class="mr-1" />
          Close
        </v-btn>
      </div>
      <v-alert v-if="apiErrorMessage" type="error" density="compact" class="mb-4">
        Request failed: {{ apiErrorMessage }}
      </v-alert>
      <div class="header-strip" />

      <v-card-text class="modal-body">
        <div class="form-grid">
          <label class="form-field-label" for="first-name">First Name</label>
          <v-text-field
            id="first-name"
            v-model="formData.firstName"
            placeholder="First Name"
            hide-details="auto"
            :error-messages="formErrors.firstName"
            :disabled="isLoading"
          />

          <label class="form-field-label" for="last-name">Last Name</label>
          <v-text-field
            id="last-name"
            v-model="formData.lastName"
            placeholder="Last Name"
            hide-details="auto"
            :error-messages="formErrors.lastName"
            :disabled="isLoading"
          />

          <label class="form-field-label" for="email">Email</label>
          <v-text-field
            id="email"
            v-model="formData.email"
            type="email"
            placeholder="Email"
            hide-details="auto"
            :error-messages="formErrors.email"
            :disabled="isLoading"
          />

          <label class="form-field-label" for="idir-name">IDIR Name</label>
          <v-text-field
            id="idir-name"
            v-model="formData.idirName"
            placeholder="IDIR Name"
            hide-details="auto"
            :error-messages="formErrors.idirName"
            :disabled="isLoading"
          />

          <label class="form-field-label" for="gender">Gender</label>
          <Select
            id="gender"
            v-model="formData.gender"
            label="Gender"
            :items="genderOptions"
            :error-messages="formErrors.gender"
          />

          <label class="form-field-label" for="rank">Rank</label>
          <Select
            id="rank"
            v-model="formData.rank"
            label="Rank"
            :items="positionTypeOptions"
            :error-messages="formErrors.rank"
          />

          <label class="form-field-label" for="badge-number">Badge Number</label>
          <v-text-field
            id="badge-number"
            v-model="formData.badgeNumber"
            placeholder="Badge Number"
            hide-details="auto"
            :error-messages="formErrors.badgeNumber"
            :disabled="isLoading"
          />

          <label class="form-field-label" for="home-location">Home Location</label>
          <Select
            id="home-location"
            v-model="formData.homeLocationId"
            label="Home Location"
            :items="homeLocationOptions"
            :error-messages="formErrors.homeLocationId"
          />

          <span class="form-field-label">Is enabled</span>
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
          {{ isEditMode ? 'Save Changes' : 'Add Member' }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
:deep(.user-form-dialog-content) {
  width: min(660px, calc(100vw - 24px));
}

.user-form-modal {
  width: 100%;
  border-radius: 12px;
  overflow: hidden;
  background: #e9e9eb;
  display: flex;
  flex-direction: column;
  max-height: calc(100vh - 48px);
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

.form-field-label {
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

.enabled-switch {
  margin: 0;
}

:deep(.enabled-switch .v-selection-control) {
  min-height: 32px;
}
</style>
