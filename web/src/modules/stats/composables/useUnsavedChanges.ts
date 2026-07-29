import { onMounted, onUnmounted, ref } from 'vue';

/**
 * Tracks whether the user has made unsaved changes and provides helpers
 * to guard destructive actions (navigation, week change, etc.).
 */
export function useUnsavedChanges() {
  const isDirty = ref(false);

  /** Mark state as dirty (user made a meaningful edit). */
  function markDirty(): void {
    isDirty.value = true;
  }

  /** Mark state as clean (after a successful load or save). */
  function markClean(): void {
    isDirty.value = false;
  }

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

  return { isDirty, markDirty, markClean, confirmIfDirty };
}
