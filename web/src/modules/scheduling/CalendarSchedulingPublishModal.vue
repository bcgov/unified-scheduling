<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import type { SchedulePublishPreviewResponse, SchedulePublishRequest } from '@/api-access/generated/models';
import {
  postApiSchedulingPublish,
  postApiSchedulingPublishPreview,
} from '@/api-access/generated/schedule-publish/schedule-publish';
import { PostApiSchedulingPublishPreviewResponse } from '@/api-access/generated/schedule-publish/schedule-publish.zod';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaModal from '@/shared/components/UaModal.vue';

const props = defineProps<{
  request: SchedulePublishRequest;
}>();

const emit = defineEmits<{
  (event: 'close'): void;
  (event: 'published', count: number): void;
}>();

const preview = ref<SchedulePublishPreviewResponse>();
const isLoading = ref(false);
const isPublishing = ref(false);
const errorMessage = ref('');
const hasNoCandidates = computed(
  () => preview.value?.candidates.shiftEntryCount === 0 && preview.value.candidates.assignmentEntryCount === 0,
);

onMounted(loadPreview);

async function loadPreview() {
  isLoading.value = true;
  errorMessage.value = '';

  try {
    const { data, error, execute } = postApiSchedulingPublishPreview(props.request, {
      options: { immediate: false },
    });
    await execute();

    if (error.value || !data.value) {
      errorMessage.value = error.value?.message || 'Failed to preview this schedule publication.';
      return;
    }

    preview.value = data.value;
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? error.message : 'Failed to preview this schedule publication.';
  } finally {
    isLoading.value = false;
  }
}

async function publishSchedule() {
  if (!preview.value?.canPublish) {
    return;
  }

  isPublishing.value = true;
  errorMessage.value = '';

  try {
    const { data, error, statusCode, execute } = postApiSchedulingPublish(props.request, {
      options: { immediate: false },
    });
    await execute();

    if (statusCode.value === 409) {
      const blockingPreview = PostApiSchedulingPublishPreviewResponse.safeParse(data.value);
      if (blockingPreview.success) {
        preview.value = blockingPreview.data;
        errorMessage.value = 'The schedule changed. Review the updated blockers before publishing.';
        return;
      }
    }

    if (error.value || !data.value) {
      errorMessage.value = error.value?.message || 'Failed to publish this schedule.';
      return;
    }

    const publishedCount = data.value.publishedShiftEntryCount + data.value.publishedAssignmentEntryCount;
    emit('published', publishedCount);
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? error.message : 'Failed to publish this schedule.';
  } finally {
    isPublishing.value = false;
  }
}

function toAvatarText(displayName: string) {
  return displayName
    .split(/\s+/)
    .filter(Boolean)
    .map((part) => part.charAt(0))
    .join('')
    .slice(0, 2)
    .toUpperCase();
}
</script>

<template>
  <UaModal title="Publish Schedule" width="680" :loading="isLoading || isPublishing" @close="emit('close')">
    <template #alerts>
      <UaAlert v-if="errorMessage" type="error" :closable="false">{{ errorMessage }}</UaAlert>
    </template>

    <dl class="schedule-publish__scope">
      <div>
        <dt>Location</dt>
        <dd>{{ request.locationId }}</dd>
      </div>
      <div>
        <dt>Date range</dt>
        <dd>{{ request.startDate }} to {{ request.endDate }}</dd>
      </div>
    </dl>

    <div v-if="isLoading" class="schedule-publish__state">Checking this schedule...</div>

    <div v-else-if="preview" class="schedule-publish">
      <UaAlert v-if="hasNoCandidates" type="info" :closable="false">
        No draft schedule items are available to publish for this location and date range.
      </UaAlert>

      <section v-if="preview.warnings.incompleteEmployees.length" class="schedule-publish__employee-warnings">
        <h3 class="schedule-publish__section-title">Employees requiring attention</h3>
        <div class="schedule-publish__employee-list" role="list">
          <div
            v-for="employee in preview.warnings.incompleteEmployees"
            :key="employee.userId"
            class="schedule-publish__employee"
            role="listitem"
          >
            <span class="schedule-publish__employee-avatar" aria-hidden="true">
              {{ toAvatarText(employee.displayName) }}
            </span>
            <span class="schedule-publish__employee-content">
              <strong class="schedule-publish__employee-name">{{ employee.displayName }}</strong>
              <span class="schedule-publish__employee-error">Missing at least one Shift or Assignment.</span>
            </span>
          </div>
        </div>
      </section>

      <div class="schedule-publish__checks">
        <UaAlert v-if="preview.warnings.unassignedShifts.length" type="warning" :closable="false">
          {{ preview.warnings.unassignedShifts.length }} shifts are unassigned
        </UaAlert>
        <UaAlert v-if="preview.warnings.unassignedAssignments.length" type="warning" :closable="false">
          {{ preview.warnings.unassignedAssignments.length }} assignments are unassigned
        </UaAlert>
        <UaAlert v-if="preview.blockers.conflicts.length" type="error" :closable="false">
          {{ preview.blockers.conflicts.length }} unresolved conflicts
        </UaAlert>
        <UaAlert
          v-for="(blocker, index) in preview.blockers.shiftConflicts"
          :key="`${index}-${blocker.message}`"
          type="error"
          :closable="false"
        >
          {{ blocker.message }}
        </UaAlert>
      </div>
    </div>

    <template #actions>
      <UaBtn variant="outlined" :disabled="isPublishing" @click="emit('close')">Cancel</UaBtn>
      <UaBtn
        color="primary"
        variant="flat"
        :loading="isPublishing"
        :disabled="isLoading || isPublishing || !preview?.canPublish"
        @click="publishSchedule"
      >
        Publish Schedule
      </UaBtn>
    </template>
  </UaModal>
</template>

<style scoped>
.schedule-publish__scope {
  display: grid;
  gap: var(--ua-spacing-sm);
  margin: 0;
}

.schedule-publish__scope div {
  display: grid;
  grid-template-columns: 7rem 1fr;
}

.schedule-publish__scope dt {
  font-weight: var(--ua-font-weight-semibold);
}

.schedule-publish__scope dd {
  margin: 0;
}

.schedule-publish {
  display: grid;
  gap: var(--ua-spacing-lg);
  margin-top: var(--ua-spacing-lg);
}

.schedule-publish__state {
  margin-top: var(--ua-spacing-lg);
}

.schedule-publish__employee-warnings {
  display: grid;
  gap: var(--ua-spacing-sm);
}

.schedule-publish__section-title {
  font-size: var(--ua-font-size-md);
  font-weight: var(--ua-font-weight-semibold);
  margin: 0;
}

.schedule-publish__employee-list {
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius-sm);
  overflow: hidden;
}

.schedule-publish__employee {
  align-items: center;
  background: rgb(var(--v-theme-surface));
  display: flex;
  gap: var(--ua-spacing-sm);
  min-width: 0;
  padding: var(--ua-spacing-sm) var(--ua-spacing-md);
}

.schedule-publish__employee + .schedule-publish__employee {
  border-top: 1px solid var(--ua-border-color);
}

.schedule-publish__employee-avatar {
  align-items: center;
  background: rgb(var(--v-theme-surface-variant));
  border: 1px solid var(--ua-border-color);
  border-radius: 50%;
  color: var(--ua-text-primary);
  display: inline-flex;
  flex: 0 0 2rem;
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-bold);
  height: 2rem;
  justify-content: center;
  width: 2rem;
}

.schedule-publish__employee-content {
  display: grid;
  gap: 0.125rem;
  min-width: 0;
}

.schedule-publish__employee-name {
  color: var(--ua-text-primary);
  font-size: var(--ua-font-size-sm);
  font-weight: var(--ua-font-weight-bold);
  overflow-wrap: anywhere;
}

.schedule-publish__employee-error {
  font-size: var(--ua-font-size-xs);
  overflow-wrap: anywhere;
}

.schedule-publish__checks :deep(.ua-alert:last-child) {
  margin-bottom: 0;
}
</style>
