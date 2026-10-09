<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { getApiUsersId } from '@/api-access/generated/users/users';
import type { UserResponse } from '@/api-access/generated/models/userResponse';
import { formatCalendarEventDate, formatCalendarTime } from '@/utils/date';
import { formatUserName } from '@/utils/user';

export interface AuditFields {
  createdById?: string | null;
  createdOn?: string | null;
  updatedById?: string | null;
  updatedOn?: string | null;
}

const props = defineProps<{
  audit: AuditFields;
  timeZone?: string;
}>();

const latestAudit = computed(() =>
  props.audit.updatedOn
    ? { userId: props.audit.updatedById, date: props.audit.updatedOn }
    : { userId: props.audit.createdById, date: props.audit.createdOn },
);
const auditUser = ref<UserResponse>();
let userRequestSequence = 0;

watch(
  () => latestAudit.value.userId,
  async (userId) => {
    const requestSequence = ++userRequestSequence;
    auditUser.value = undefined;
    if (!userId) {
      return;
    }

    const { data, error, execute } = getApiUsersId(userId, { options: { immediate: false } });
    await execute();
    if (requestSequence === userRequestSequence && !error.value && data.value) {
      auditUser.value = data.value;
    }
  },
  { immediate: true },
);

const userName = computed(() => (auditUser.value ? formatUserName(auditUser.value) : 'Unknown user'));
const dateTime = computed(() => {
  if (!latestAudit.value.date) {
    return '';
  }

  const date = formatCalendarEventDate(latestAudit.value.date, { timeZone: props.timeZone });
  const time = formatCalendarTime(latestAudit.value.date, props.timeZone);
  return date && time ? `${date} at ${time}` : '';
});
</script>

<template>
  <span class="ua-audit-display">
    <span>{{ userName }}</span>
    <span v-if="dateTime"> · {{ dateTime }}</span>
  </span>
</template>

<style scoped>
.ua-audit-display {
  color: var(--ua-text-secondary);
  font-style: italic;
}
</style>
