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

  it('opens an existing assignment on its edit tab and resets the tab when closed', () => {
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

    expect(calendarSchedulingAssignmentModalInitialTab.value).toBe('details');
  });
});
