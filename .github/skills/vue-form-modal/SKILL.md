---
name: vue-form-modal
description: "Refactor or create Vue 3 form modal components using a formData object, generated Zod schemas, and consistent save/error handling. Use when asked to clean up, refactor, or build a form modal in web/src/."
applyTo: "web/src/**/*.vue"
---

# Vue Form Modal Pattern

Apply this pattern whenever writing or refactoring a `*Modal.vue` component that manages a form.

## 1. Derive the FormData type from the generated Zod body schema

Import the generated Zod body schema from `@/api-access/generated/<tag>/<tag>.zod.ts` and derive the form type from it.

```ts
import { PostApiWidgetsBody } from '@/api-access/generated/widgets/widgets.zod';

type WidgetFormData = Partial<zod.infer<typeof PostApiWidgetsBody>>;
```

## 2. Extend the schema for stricter UI validation

Do **not** write a schema from scratch. Extend the generated one so the shape stays in sync with the API.

```ts
const widgetSchema = PostApiWidgetsBody.extend({
  name: PostApiWidgetsBody.shape.name.min(1, validationMessages.required),
  // add .superRefine() for cross-field rules
}).superRefine((data, ctx) => {
  if (data.endDate && data.startDate && isDateInputBefore(data.endDate, data.startDate)) {
    ctx.addIssue({ code: 'custom', path: ['endDate'], message: 'End date cannot be before start date.' });
  }
});
```

## 3. Single `formData` ref — no individual field refs

Use one `ref<FormData>` instead of multiple `ref<string>` fields.

```ts
// ✅ do this
const formData = ref<WidgetFormData>(props.widget ? populateFromWidget(props.widget) : createInitialFormData());

// ❌ not this
const name = ref('');
const startDate = ref('');
```

Provide typed helper functions to initialise the object:

```ts
const createInitialFormData = (): WidgetFormData => ({
  name: '',
  startDate: getTodayDateInputValue(),
});

const populateFromWidget = (w: WidgetResponse): WidgetFormData => ({
  name: w.name ?? '',
  startDate: toDateInputValue(w.startDate) ?? getTodayDateInputValue(),
});
```

## 4. Watch for prop-driven re-initialisation (only when needed)

Only add a `watch` if the prop can change while the modal is mounted (e.g. shared modal re-used for different rows). Skip `{ immediate: true }` — initialise `formData` inline instead.

```ts
watch(
  () => props.widget,
  (w) => {
    formData.value = w ? populateFromWidget(w) : createInitialFormData();
    apiError.value = '';
    formErrors.value = {};
  },
);
```

## 5. Modal state — three separate refs

Keep UI state as three named refs (do **not** merge into one object — they serve different concerns):

```ts
const isSaving = ref(false);
const apiError = ref('');
const formErrors = ref<Record<string, string>>({});
```

## 6. `validateForm` returns the typed DTO or `null`

```ts
function validateForm(): WidgetRequestDto | null {
  formErrors.value = {};
  const result = widgetSchema.safeParse(formData.value);
  if (!result.success) {
    formErrors.value = getFieldErrors(result.error);
    return null;
  }
  return result.data;
}
```

Shared `getFieldErrors` helper (copy verbatim):

```ts
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
```

## 7. `handleSave` — create vs edit branch

```ts
const handleSave = async () => {
  const payload = validateForm();
  if (!payload) return;

  isSaving.value = true;
  apiError.value = '';

  try {
    if (isEditMode.value && props.widget?.id) {
      const { error } = await putApiWidgetsId(props.widget.id, payload);
      if (error.value) { apiError.value = error.value.message || 'Failed to update.'; return; }
      emit('updated');
    } else {
      const { error } = await postApiWidgets(payload);
      if (error.value) { apiError.value = error.value.message || 'Failed to create.'; return; }
      emit('created');
    }
    emit('close');
  } catch (err: unknown) {
    apiError.value = err instanceof Error ? err.message : 'An unexpected error occurred';
  } finally {
    isSaving.value = false;
  }
};
```

## 8. Template conventions

- Bind `formData.<field>` directly on `v-model` / `:model-value`.
- Pass `:error-messages="formErrors.<field>"` on every input.
- Use `UaAlert` in `#alerts` slot for `apiError`.
- Use `UaBtn` with `:loading="isSaving"` for the save button.
- `v-model` type mismatch on nullable fields (`string | null`): cast inline with `v-model="formData.comment as string"`.

```html
<template>
  <UaModal :title="modalTitle" :loading="isSaving" @close="emit('close')">
    <template #alerts>
      <UaAlert v-if="apiError" type="error" @close="apiError = ''">{{ apiError }}</UaAlert>
    </template>

    <UaFormGrid>
      <UaTextField
        id="widget-name"
        label="Name"
        :model-value="formData.name"
        :error-messages="formErrors.name"
        :disabled="isSaving"
        @update:model-value="(v: string) => (formData.name = v)"
      />
      <!-- nullable textarea: cast to string -->
      <UaTextarea id="widget-comment" v-model="formData.comment as string" label="Comment (optional)" />
    </UaFormGrid>

    <template #actions>
      <UaBtn variant="outlined" :disabled="isSaving" @click="emit('close')">Cancel</UaBtn>
      <UaBtn color="primary" variant="flat" :loading="isSaving" @click="handleSave">
        {{ isEditMode ? 'Save Changes' : 'Add Widget' }}
      </UaBtn>
    </template>
  </UaModal>
</template>
```

## Reference implementations

- [UserFormModal.vue](../../../web/src/modules/myteam/components/UserFormModal.vue) — full create/edit with server-side validation mapping
- [ActingPositionModal.vue](../../../web/src/modules/myteam/components/ActingPositionModal.vue) — date fields, cross-field validation, watch-based re-init
