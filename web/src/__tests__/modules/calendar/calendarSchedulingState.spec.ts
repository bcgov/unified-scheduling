import { afterEach, describe, expect, it } from 'vitest';
import {
  calendarSchedulingAssignmentModalEntryId,
  calendarSchedulingAssignmentModalInitialTab,
  calendarSchedulingAssignmentModalMode,
  closeCalendarSchedulingAssignmentModal,
  isCalendarSchedulingAssignmentModalOpen,
  showCalendarSchedulingAssignmentModal,
} from '@/modules/scheduling/calendarSchedulingState';

describe('calendarSchedulingState', () => {
  afterEach(closeCalendarSchedulingAssignmentModal);

  it('opens an existing assignment in view mode and resets the state when closed', () => {
    showCalendarSchedulingAssignmentModal('2026-08-10', {
      mode: 'view',
      initialTab: 'edit',
      assignmentEntryId: 257,
    });

    expect(isCalendarSchedulingAssignmentModalOpen.value).toBe(true);
    expect(calendarSchedulingAssignmentModalMode.value).toBe('view');
    expect(calendarSchedulingAssignmentModalInitialTab.value).toBe('edit');
    expect(calendarSchedulingAssignmentModalEntryId.value).toBe(257);

    closeCalendarSchedulingAssignmentModal();

    expect(calendarSchedulingAssignmentModalMode.value).toBe('create');
    expect(calendarSchedulingAssignmentModalInitialTab.value).toBe('details');
    expect(calendarSchedulingAssignmentModalEntryId.value).toBeUndefined();
  });
});
