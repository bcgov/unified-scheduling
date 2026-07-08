<script setup lang="ts">
import { Gender, LookupCodeTypes, type UserRequestDto, type UserResponse } from '@/api-access/generated/models';
import { postApiUsers, postApiUsersIdUploadPhoto, putApiUsersId } from '@/api-access/generated/users/users';
import { PostApiUsersBody } from '@/api-access/generated/users/users.zod';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaFormGrid from '@/shared/components/UaFormGrid.vue';
import UaModal from '@/shared/components/UaModal.vue';
import UaSelect from '@/shared/components/UaSelect.vue';
import UaTextField from '@/shared/components/UaTextField.vue';
import { mapToValidationErrors, validationMessages } from '@/shared/validation/validationErrors';
import { useLocationsStore } from '@/stores/LocationsStore';
import { useLookupStore } from '@/stores/LookupStore';
import { mapToSelectOptions } from '@/utils/select';
import { mdiCamera, mdiClose, mdiContentSave } from '@mdi/js';
import { computed, onMounted, onUnmounted, ref } from 'vue';
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
const homeLocationOptions = locationsStore.selectOptions;
const genderOptions = mapToSelectOptions(
  Object.values(Gender),
  (gender) => gender,
  (gender) => gender,
);

const isLoading = ref(false);
const apiErrorMessage = ref('');

// --- Photo upload ---
const photoFile = ref<File | null>(null);
const localPhotoPreviewUrl = ref<string | null>(null);
const fileInputRef = ref<HTMLInputElement | null>(null);

const photoPreviewUrl = computed(() => localPhotoPreviewUrl.value ?? props.user?.photoUrl ?? null);

const photoInitials = computed(() => {
  const f = (formData.value.firstName?.[0] ?? '').toUpperCase();
  const l = (formData.value.lastName?.[0] ?? '').toUpperCase();
  return f + l || '?';
});

const triggerFileInput = () => fileInputRef.value?.click();

const onFileChange = (e: Event) => {
  const input = e.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) return;

  if (localPhotoPreviewUrl.value) {
    URL.revokeObjectURL(localPhotoPreviewUrl.value);
  }
  photoFile.value = file;
  localPhotoPreviewUrl.value = URL.createObjectURL(file);
};

onUnmounted(() => {
  if (localPhotoPreviewUrl.value) {
    URL.revokeObjectURL(localPhotoPreviewUrl.value);
  }
});
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
    let savedUser: UserResponse | null = null;
    let userId: string | null = null;

    if (isEditMode.value && props.user?.id) {
      // --- Edit mode ---
      const { data, error } = await putApiUsersId(props.user.id, payload);

      if (error.value) {
        if (applyServerValidationErrors(data.value)) return;
        apiErrorMessage.value = error.value.message || 'Failed to update user';
        return;
      }

      savedUser = data.value;
      userId = props.user.id;
    } else {
      // --- Create mode ---
      const { data, error } = await postApiUsers(payload);

      if (error.value) {
        if (applyServerValidationErrors(data.value)) return;
        apiErrorMessage.value = error.value.message || 'Failed to create user';
        return;
      }

      savedUser = data.value;
      userId = data.value?.id ?? null;
    }

    // Upload photo if a file was selected
    if (photoFile.value && userId) {
      const { data: photoData, error: photoError } = await postApiUsersIdUploadPhoto(userId, {
        photo: photoFile.value,
      });
      if (photoError.value) {
        apiErrorMessage.value = photoError.value.detail || 'Failed to upload photo';
        return;
      }
      if (photoData.value) {
        savedUser = photoData.value;
      }
    }

    if (isEditMode.value) {
      emit('updated', savedUser);
    } else {
      emit('created', savedUser);
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

    <!-- Photo upload -->
    <div class="photo-upload-section">
      <div class="photo-preview-wrapper" @click="triggerFileInput" :title="'Click to upload photo'">
        <v-avatar size="120" color="grey-lighten-2">
          <v-img v-if="photoPreviewUrl" :src="photoPreviewUrl" cover alt="Profile photo" />
          <span v-else class="text-h6">{{ photoInitials }}</span>
        </v-avatar>
        <div class="photo-upload-overlay">
          <v-icon :icon="mdiCamera" color="white" size="20" />
        </div>
      </div>
      <span class="photo-hint">{{ photoFile ? photoFile.name : 'Click to upload photo' }}</span>
      <input
        ref="fileInputRef"
        type="file"
        accept="image/jpeg,image/png"
        style="display: none"
        @change="onFileChange"
      />
    </div>

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
      <UaSelect
        id="gender"
        v-model="formData.gender"
        label="Gender"
        :items="genderOptions"
        :error-messages="formErrors.gender"
      />

      <label class="ua-form-label" for="rank">Rank</label>
      <UaSelect
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
        @update:model-value="(v) => (formData.badgeNumber = v as string)"
      />

      <label class="ua-form-label" for="home-location">Home Location</label>
      <UaSelect
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
      <UaBtn variant="outlined" @click="handleClose" :disabled="isLoading" :prepend-icon="mdiClose">Close</UaBtn>
      <UaBtn color="primary" variant="flat" @click="handleSave" :loading="isLoading" :prepend-icon="mdiContentSave">
        {{ isEditMode ? 'Save Changes' : 'Add Member' }}
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

.photo-upload-section {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--ua-spacing-sm);
  padding: var(--ua-spacing-md) 0;
}

.photo-preview-wrapper {
  position: relative;
  cursor: pointer;
  border-radius: 50%;
  display: inline-block;

  &:hover .photo-upload-overlay {
    opacity: 1;
  }
}

.photo-upload-overlay {
  position: absolute;
  inset: 0;
  border-radius: 50%;
  background: rgba(0, 0, 0, 0.45);
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0;
  transition: opacity 0.2s ease;
}

.photo-hint {
  font-size: var(--ua-font-size-sm);
  color: var(--ua-text-secondary);
  max-width: 180px;
  text-align: center;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
