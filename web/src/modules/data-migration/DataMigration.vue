<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import UaBtn from '@/shared/components/UaBtn.vue';
import UaPageHeader from '@/shared/components/UaPageHeader.vue';
import { useNotificationsStore } from '@/stores/notifications';
import { useAccessControl } from '@/composables/useAccessControl';
import { canOperateDataMigration } from './DataMigrationModule';
import {
  getMigrationRuns,
  migrationJobs,
  startMigration,
  type MigrationJob,
  type MigrationRun,
} from './dataMigrationApi';

const notifications = useNotificationsStore();
const access = useAccessControl();
const allowed = computed(() => canOperateDataMigration(access));
const known = ref(false);
const runs = ref<MigrationRun[]>([]);
const pending = ref<Partial<Record<MigrationJob, boolean>>>({});
const accepted = ref<Partial<Record<MigrationJob, MigrationRun>>>({});
let disposed = false;
let timer: ReturnType<typeof setTimeout> | undefined;
let refreshing: Promise<boolean> | undefined;

function disabled(job: MigrationJob) {
  return (
    !allowed.value ||
    !known.value ||
    pending.value[job] ||
    accepted.value[job]?.status === 'Running' ||
    runs.value.some((run) => run.job === job && run.status === 'Running')
  );
}

function latest(job: MigrationJob) {
  return accepted.value[job] ?? runs.value.find((run) => run.job === job);
}

function refresh(): Promise<boolean> {
  if (refreshing) return refreshing;
  refreshing = loadRuns().finally(() => {
    refreshing = undefined;
  });
  return refreshing;
}

async function loadRuns() {
  known.value = false;
  if (!allowed.value || disposed) return false;
  try {
    const values: MigrationRun[] = [];
    let total = 0;
    do {
      const request = getMigrationRuns(values.length);
      await request.execute();
      const page = request.data.value;
      if (
        request.error.value ||
        !Array.isArray(page?.items) ||
        !Number.isInteger(page.total) ||
        page.total < 0 ||
        (page.items.length === 0 && values.length < page.total) ||
        page.items.some((run) => !run.id || !run.job || !run.status)
      ) {
        throw new Error('Run state unavailable');
      }
      values.push(...page.items);
      total = page.total;
    } while (values.length < total);
    if (disposed || !allowed.value) return false;
    runs.value = values;
    for (const { job } of migrationJobs) {
      const current = accepted.value[job];
      const updated = current && values.find((run) => run.id === current.id);
      if (updated) accepted.value[job] = updated;
    }
    known.value = true;
    return true;
  } catch {
    if (!disposed)
      notifications.show('Cannot load migration run state. Starts are disabled until refresh succeeds.', 'error');
    return false;
  }
}

async function start(job: MigrationJob, label: string) {
  if (disabled(job)) return;
  if (
    !window.confirm(`Start ${label}? This applies migration writes to the configured target database (not a dry run).`)
  )
    return;
  pending.value[job] = true;
  try {
    // Recheck server state immediately before submitting; the server still arbitrates races.
    if (!(await refresh()) || !allowed.value || disposed) return;
    if (runs.value.some((run) => run.job === job && run.status === 'Running')) return;
    const request = startMigration(job);
    await request.execute();
    if (request.error.value || request.response.value?.status !== 202 || !request.data.value?.runId) {
      const status = request.response.value?.status;
      notifications.show(
        status === 409
          ? 'Migration start refused (409): job busy, paused, disabled, or lineage/preconditions not ready.'
          : status === 401 || status === 403
            ? 'Migration start denied: authentication and DataMigrationOperate permission are required.'
            : !status
              ? 'Migration start network error; outcome unknown. Check run state before retrying.'
              : 'Migration start failed or its outcome is unknown. Check run state before retrying.',
        'error',
      );
    } else {
      const runId = request.data.value.runId;
      accepted.value[job] = { id: runId, job, status: 'Running' };
      notifications.show(`${label} migration accepted. Run ID: ${runId}`, 'success');
    }
  } catch {
    notifications.show('Migration start network error; outcome unknown. Check run state before retrying.', 'error');
  } finally {
    known.value = false;
    await refresh();
    pending.value[job] = false;
  }
}

async function poll() {
  await refresh();
  if (!disposed) timer = setTimeout(() => void poll(), 5000);
}

watch(allowed, (value) => {
  if (!value) {
    known.value = false;
    notifications.show('DataMigration feature and DataMigrationOperate permission are required.', 'error');
  }
});
onMounted(() => {
  if (!allowed.value)
    notifications.show('DataMigration feature and DataMigrationOperate permission are required.', 'error');
  void poll();
});
onUnmounted(() => {
  disposed = true;
  clearTimeout(timer);
});
</script>

<template>
  <UaPageHeader title="Data Migration" />
  <p v-if="!allowed">DataMigration feature and DataMigrationOperate permission are required.</p>
  <template v-else>
    <p>These actions apply writes to the configured target database. They are not dry runs.</p>
    <p v-if="!known">Run state unavailable or refreshing. Starts are disabled.</p>
    <div v-for="{ job, label } in migrationJobs" :key="job" class="mb-4">
      <UaBtn :disabled="!!disabled(job)" @click="start(job, label)">Start {{ label }}</UaBtn>
      <p v-if="latest(job)">Run ID: {{ latest(job)?.id }} — {{ latest(job)?.status }}</p>
    </div>
  </template>
</template>
