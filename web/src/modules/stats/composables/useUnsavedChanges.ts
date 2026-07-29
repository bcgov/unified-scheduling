import { computed, onMounted, onUnmounted, ref, type Ref } from 'vue';
import type { DayAssignment } from '../types';

/**
 * Tracks whether the current dayAssignmentsMap has diverged from the
 * last-saved/loaded state and provides helpers to guard destructive actions.
 */
export function useUnsavedChanges(dayAssignmentsMap: Ref<Record<string, DayAssignment[]>>) {
  const savedSnapshot = ref('{}');

  function serialize(map: Record<string, DayAssignment[]>): string {
    // Sort keys for stable comparison, strip local-only `id` field
    const keys = Object.keys(map).sort();
    const stripped = keys.map((k) => [
      k,
      (map[k] ?? []).map(({ id, ...rest }) => rest),
    ]);
    return JSON.stringify(stripped);
  }

  /** Call after a successful load or save to mark current state as "clean". */
  function takeSnapshot(): void {
    savedSnapshot.value = serialize(dayAssignmentsMap.value);
  }

  const isDirty = computed(() => serialize(dayAssignmentsMap.value) !== savedSnapshot.value);

  /**
   * Shows a native confirm dialog when there are unsaved changes.
   * Returns `true` if safe to proceed (no changes or user confirmed discard).
   */
  function confirmIfDirty(message = 'You have unsaved changes that will be lost. Continue?'): boolean {
    if (!isDirty.value) return true;
    return window.confirm(message);
  }

  // ── beforeunload ──────────────────────────────────────────────────────────
  function onBeforeUnload(e: BeforeUnloadEvent): void {
    if (isDirty.value) {
      e.preventDefault();
    }
  }

  onMounted(() => window.addEventListener('beforeunload', onBeforeUnload));
  onUnmounted(() => window.removeEventListener('beforeunload', onBeforeUnload));

  return { isDirty, takeSnapshot, confirmIfDirty };
}
