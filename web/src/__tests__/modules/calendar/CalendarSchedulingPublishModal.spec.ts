import { mdiAlert, mdiAlertCircle, mdiChevronDown, mdiChevronUp } from '@mdi/js';
import { afterEach, describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { HttpResponse, http } from 'msw';
import { createVuetify } from 'vuetify';
import * as components from 'vuetify/components';
import * as directives from 'vuetify/directives';
import { mdi } from 'vuetify/iconsets/mdi-svg';
import { createPinia } from 'pinia';
import CalendarSchedulingPublishModal from '@/modules/scheduling/CalendarSchedulingPublishModal.vue';
import { useLocationsStore } from '@/stores/LocationsStore';
import { server } from '../../mocks/server';
import { createTestApp } from '../../helpers/createTestApp';

afterEach(() => {
  document.body.innerHTML = '';
});

describe('CalendarSchedulingPublishModal', () => {
  it('groups findings by employee ID and presents blocking findings and employees first', async () => {
    const app = await createTestApp({ loadConfig: false });
    app.vuetify.icons.sets.mdi = mdi;
    const request = { locationId: 7, startDate: '2026-08-24', endDate: '2026-08-30' };
    const employee = { userId: '77e6109e-ae1c-4f6c-96ce-60f15cc13725', displayName: 'Jordan Lee' };
    const sameName = { userId: 'b5d00ed9-703f-43f9-a81b-59e5cd8909bf', displayName: 'Jordan Lee' };
    const conflictOnly = { userId: '1c86411c-f701-4d38-a11a-03d9efebd8a1', displayName: 'Zoe Patel' };
    const warningOnly = { userId: '8cb43627-64c2-44e5-ade4-29aaabb549c4', displayName: 'Alex Morgan' };
    const start = '2026-08-24T17:00:00Z';
    const end = '2026-08-24T18:00:00Z';
    const eventDetails = { sourceEntityId: null, timeZoneId: 'America/Vancouver' };
    const authorizedShift = {
      ...eventDetails,
      eventId: '201',
      sourceModule: 'scheduling',
      title: 'Published morning shift',
      start,
      end,
    };
    const restrictedMessage = 'This event has a conflict with an event you do not have permission to view.';
    server.use(
      http.post('*/api/scheduling/publish/preview', () =>
        HttpResponse.json({
          scope: {
            ...request,
            timeZoneId: 'America/Vancouver',
            rangeStartUtc: '2026-08-24T07:00:00Z',
            rangeEndExclusiveUtc: '2026-08-31T07:00:00Z',
          },
          candidates: { shiftEntryCount: 2, assignmentEntryCount: 1, totalCount: 3 },
          affectedEmployees: [warningOnly, conflictOnly, sameName, employee],
          warnings: {
            incompleteEmployees: [
              { ...warningOnly, missingShift: true, missingAssignment: true },
              { ...sameName, missingShift: true, missingAssignment: false },
              { ...employee, missingShift: false, missingAssignment: true },
            ],
            unassignedShifts: [{ eventId: 301, title: 'Open shift' }],
            unassignedAssignments: [],
            conflicts: [
              {
                id: 'published-conflict',
                title: 'Published shift conflict',
                firstSourceModule: 'scheduling',
                firstEventId: '201',
                secondSourceModule: 'training',
                secondEventId: 'course-43',
                resourceId: employee.userId,
                overlapStart: start,
                overlapEnd: end,
                entry: authorizedShift,
              },
            ],
            restrictedWarnings: [
              {
                resourceId: employee.userId,
                eventId: '203',
                sourceModule: 'scheduling',
                message: restrictedMessage,
              },
            ],
            hasRestrictedConflicts: true,
            hasUnattributedRestrictedConflicts: true,
          },
          blockers: {
            conflicts: [
              {
                id: 'assignment-conflict',
                title: 'Assignment and Training',
                firstSourceModule: 'scheduling',
                firstEventId: '200',
                secondSourceModule: 'training',
                secondEventId: 'course-42',
                resourceId: employee.userId,
                overlapStart: start,
                overlapEnd: end,
                entry: {
                  ...eventDetails,
                  eventId: '200',
                  sourceModule: 'scheduling',
                  title: 'Front desk',
                  start: '2026-08-24T16:00:00Z',
                  end,
                },
                overlaps: {
                  ...eventDetails,
                  eventId: 'course-42',
                  sourceModule: 'training',
                  title: 'First aid',
                  start,
                  end: '2026-08-24T19:00:00Z',
                },
              },
            ],
            shiftConflicts: [
              {
                userId: employee.userId,
                message: 'Already has a shift on this start date.',
                firstShift: {
                  shiftEntryId: 10,
                  eventId: 100,
                  title: 'Morning shift',
                  startAtUtc: start,
                  endAtUtc: end,
                },
                secondShift: { shiftEntryId: 11, eventId: 101, title: 'Relief shift', startAtUtc: end, endAtUtc: null },
              },
            ],
            restrictedWarnings: [
              {
                resourceId: conflictOnly.userId,
                eventId: '201',
                sourceModule: 'scheduling',
                message: restrictedMessage,
                authorizedEvent: authorizedShift,
              },
            ],
            hasRestrictedConflicts: true,
            hasUnattributedRestrictedConflicts: true,
          },
          canPublish: false,
        }),
      ),
    );
    const wrapper = mount(CalendarSchedulingPublishModal, {
      props: { request },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();

    const modal = document.querySelector('.ua-modal')!;
    const severityKey = modal.querySelector('[aria-label="Publication severity key"]')!;
    expect(severityKey.textContent).toContain('Error (Blocking)');
    expect(severityKey.textContent).toContain('Warning (Non-blocking)');
    expect(severityKey.textContent).toContain('Must be resolved before publishing this schedule.');
    expect(severityKey.querySelector('.v-icon.text-error path')?.getAttribute('d')).toBe(mdiAlertCircle);
    expect(severityKey.querySelector('.v-icon.text-warning path')?.getAttribute('d')).toBe(mdiAlert);
    expect(Array.from(modal.querySelectorAll('h2'), (heading) => heading.textContent)).toEqual([
      'Employee-specific issues',
      'General schedule issues',
    ]);
    const groups = Array.from(modal.querySelectorAll('.schedule-publish__employee'));
    expect(groups.map((group) => group.querySelector('h3')?.textContent)).toEqual([
      'Jordan Lee',
      'Zoe Patel',
      'Alex Morgan',
      'Jordan Lee',
    ]);
    const findings = Array.from(groups[0]!.querySelectorAll('li'));
    expect(findings.map((finding) => finding.textContent)).toEqual([
      expect.stringContaining('Assignment and Training'),
      expect.stringContaining('Already has a shift on this start date.'),
      expect.stringContaining('Published shift conflict'),
      expect.stringContaining(restrictedMessage),
      expect.stringContaining('Missing an assignment.'),
    ]);
    expect(findings[0]?.textContent).toContain('Front desk · August 24, 2026, 9:00 AM - 11:00 AM');
    expect(findings[0]?.textContent).toContain('First aid · August 24, 2026, 10:00 AM - 12:00 PM');
    expect(findings[0]?.textContent).toContain('Conflict overlap · August 24, 2026, 10:00 AM - 11:00 AM');
    expect(findings[0]?.querySelector('.v-icon.text-error path')?.getAttribute('d')).toBe(mdiAlertCircle);
    expect(findings[1]?.textContent).toContain('Morning shift');
    expect(findings[1]?.textContent).toContain('Relief shift');
    expect(findings[2]?.querySelector('.v-icon.text-warning path')?.getAttribute('d')).toBe(mdiAlert);
    expect(findings[3]?.querySelector('.v-icon.text-warning path')?.getAttribute('d')).toBe(mdiAlert);
    expect(findings[3]?.querySelector('.schedule-publish__detail')).toBeNull();
    expect(groups[1]?.querySelector('.v-icon.text-error path')?.getAttribute('d')).toBe(mdiAlertCircle);
    expect(groups[1]?.textContent).toContain(restrictedMessage);
    expect(groups[1]?.querySelector('.schedule-publish__detail')?.textContent?.trim()).toBe(
      'Published morning shift · August 24, 2026, 10:00 AM - 11:00 AM',
    );
    expect(groups[2]?.textContent).toContain('Missing a shift and an assignment.');
    expect(groups[3]?.textContent).toContain('Missing a shift.');
    const generalFindings = modal.querySelector('.schedule-publish__general-findings')!;
    const generalWarnings = Array.from(generalFindings.querySelectorAll('.ua-alert.bg-warning'));
    expect(generalWarnings.map((warning) => warning.textContent)).toEqual([
      expect.stringContaining('Unresolved conflicts on published schedule items'),
      expect.stringContaining('1 shift is unassigned.'),
    ]);
    expect(generalWarnings[0]?.querySelector('.v-icon path')?.getAttribute('d')).toBe(mdiAlert);
    expect(generalFindings.querySelector('.ua-alert.bg-error')?.textContent).toContain(
      'Unresolved conflicts include events',
    );
    expect(generalFindings.querySelector('.ua-alert.bg-error .v-icon path')?.getAttribute('d')).toBe(mdiAlertCircle);
    expect(modal.querySelector('.schedule-publish__status')?.textContent).toContain('Resolve the blocking errors');
    wrapper.unmount();
  });

  it('toggles each employee independently, keeps publication blocked, and resets on reopen', async () => {
    const app = await createTestApp({ loadConfig: false });
    app.vuetify.icons.sets.mdi = mdi;
    const request = { locationId: 7, startDate: '2026-08-24', endDate: '2026-08-30' };
    const blockedEmployee = { userId: '77e6109e-ae1c-4f6c-96ce-60f15cc13725', displayName: 'Jordan Lee' };
    const warnedEmployee = { userId: 'b5d00ed9-703f-43f9-a81b-59e5cd8909bf', displayName: 'Jordan Lee' };
    const blocker = 'Already has a shift on this start date.';
    server.use(
      http.post('*/api/scheduling/publish/preview', () =>
        HttpResponse.json({
          scope: {
            ...request,
            timeZoneId: 'America/Vancouver',
            rangeStartUtc: '2026-08-24T07:00:00Z',
            rangeEndExclusiveUtc: '2026-08-31T07:00:00Z',
          },
          candidates: { shiftEntryCount: 1, assignmentEntryCount: 1, totalCount: 2 },
          affectedEmployees: [blockedEmployee, warnedEmployee],
          warnings: {
            incompleteEmployees: [{ ...warnedEmployee, missingShift: true, missingAssignment: false }],
            unassignedShifts: [{ eventId: 301, title: 'Open shift' }],
            unassignedAssignments: [],
          },
          blockers: { conflicts: [], shiftConflicts: [{ userId: blockedEmployee.userId, message: blocker }] },
          canPublish: false,
        }),
      ),
    );

    const openModal = () =>
      mount(CalendarSchedulingPublishModal, {
        props: { request },
        global: { plugins: app.mountPlugins },
        attachTo: document.body,
      });
    const wrapper = openModal();
    await flushPromises();

    const modal = document.querySelector('.ua-modal')!;
    const employees = modal.querySelectorAll('.schedule-publish__employee');
    const first = employees[0]!;
    const second = employees[1]!;
    const firstToggle = first.querySelector<HTMLButtonElement>('button[aria-expanded]')!;
    const secondToggle = second.querySelector<HTMLButtonElement>('button[aria-expanded]')!;
    const publishButton = Array.from(modal.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Publish Schedule',
    );
    expect(firstToggle.getAttribute('aria-expanded')).toBe('true');
    expect(secondToggle.getAttribute('aria-expanded')).toBe('true');
    expect(first.textContent).toContain(blocker);
    expect(second.textContent).toContain('Missing a shift.');
    expect(publishButton).toHaveProperty('disabled', true);

    firstToggle.click();
    await flushPromises();
    expect(firstToggle.getAttribute('aria-expanded')).toBe('false');
    expect(firstToggle.querySelector('path')?.getAttribute('d')).toBe(mdiChevronDown);
    expect(first.querySelector('h3')?.textContent).toBe('Jordan Lee');
    expect(first.querySelector('.schedule-publish__employee-avatar')?.textContent?.trim()).toBe('JL');
    expect(first.querySelector('.schedule-publish__employee-findings')).toBeNull();
    expect(secondToggle.getAttribute('aria-expanded')).toBe('true');
    expect(second.textContent).toContain('Missing a shift.');

    secondToggle.click();
    await flushPromises();
    expect(second.querySelector('.schedule-publish__employee-findings')).toBeNull();
    expect(publishButton).toHaveProperty('disabled', true);
    expect(modal.querySelector('.schedule-publish__status')?.textContent).toContain('Resolve the blocking errors');
    expect(modal.querySelector('.schedule-publish__general-findings')?.textContent).toContain('1 shift is unassigned.');

    firstToggle.click();
    await flushPromises();
    expect(firstToggle.getAttribute('aria-expanded')).toBe('true');
    expect(firstToggle.querySelector('path')?.getAttribute('d')).toBe(mdiChevronUp);
    expect(first.textContent).toContain(blocker);
    expect(secondToggle.getAttribute('aria-expanded')).toBe('false');
    expect(second.querySelector('.schedule-publish__employee-findings')).toBeNull();
    wrapper.unmount();

    const reopened = openModal();
    await flushPromises();
    expect(document.querySelectorAll('.schedule-publish__employee button[aria-expanded="true"]')).toHaveLength(2);
    expect(document.body.textContent).toContain('Missing a shift.');
    reopened.unmount();
  });

  it('keeps publication blocked when every conflict detail is restricted', async () => {
    const app = await createTestApp({ loadConfig: false });
    const request = { locationId: 7, startDate: '2026-08-24', endDate: '2026-08-30' };
    server.use(
      http.post('*/api/scheduling/publish/preview', () =>
        HttpResponse.json({
          scope: {
            ...request,
            timeZoneId: 'America/Vancouver',
            rangeStartUtc: '2026-08-24T07:00:00Z',
            rangeEndExclusiveUtc: '2026-08-31T07:00:00Z',
          },
          candidates: { shiftEntryCount: 1, assignmentEntryCount: 1, totalCount: 2 },
          warnings: { incompleteEmployees: [], unassignedShifts: [], unassignedAssignments: [] },
          blockers: { conflicts: [], shiftConflicts: [], restrictedWarnings: [], hasRestrictedConflicts: true },
          canPublish: false,
        }),
      ),
    );
    const wrapper = mount(CalendarSchedulingPublishModal, {
      props: { request },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });
    await flushPromises();
    expect(document.body.textContent).toContain(
      'Unresolved conflicts include events you do not have permission to view.',
    );
    const publishButton = Array.from(document.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Publish Schedule',
    );
    expect(publishButton).toHaveProperty('disabled', true);
    wrapper.unmount();
  });
  it('previews and publishes the immutable calendar scope', async () => {
    const vuetify = createVuetify({ components, directives });
    const pinia = createPinia();
    const locationsStore = useLocationsStore(pinia);
    locationsStore.entities = [
      { id: 7, name: 'Vancouver' },
      { id: 8, name: 'Victoria' },
    ];
    locationsStore.setSelectedLocationId(8);
    const requestBodies: Record<string, unknown>[] = [];

    server.use(
      http.post('*/api/scheduling/publish/preview', async ({ request }) => {
        requestBodies.push((await request.json()) as Record<string, unknown>);
        return HttpResponse.json({
          scope: {
            locationId: 7,
            startDate: '2026-08-24',
            endDate: '2026-08-30',
            timeZoneId: 'America/Vancouver',
            rangeStartUtc: '2026-08-24T07:00:00Z',
            rangeEndExclusiveUtc: '2026-08-31T07:00:00Z',
          },
          candidates: {
            shiftEntryCount: 2,
            assignmentEntryCount: 1,
            totalCount: 3,
          },
          warnings: {
            incompleteEmployees: [
              { userId: '77e6109e-ae1c-4f6c-96ce-60f15cc13725', displayName: 'Alex Morgan', missingShift: true },
              { userId: 'b5d00ed9-703f-43f9-a81b-59e5cd8909bf', displayName: 'Jordan Lee', missingAssignment: true },
            ],
            unassignedShifts: [],
            unassignedAssignments: [],
          },
          blockers: { conflicts: [], shiftConflicts: [] },
          canPublish: true,
        });
      }),
      http.post('*/api/scheduling/publish', async ({ request }) => {
        requestBodies.push((await request.json()) as Record<string, unknown>);
        return HttpResponse.json({
          publishedShiftEntryCount: 2,
          publishedAssignmentEntryCount: 1,
        });
      }),
    );

    const request = { locationId: 7, startDate: '2026-08-24', endDate: '2026-08-30' };
    const wrapper = mount(CalendarSchedulingPublishModal, {
      props: { request },
      global: { plugins: [pinia, vuetify] },
      attachTo: document.body,
    });

    await flushPromises();

    expect(document.querySelector('.schedule-publish__scope dd')?.textContent?.trim()).toBe('Vancouver');
    expect(document.body.textContent).not.toContain('Shift entries');
    expect(document.body.textContent).not.toContain('2 employees do not have');
    expect(document.body.textContent).toContain('Alex Morgan');
    expect(document.body.textContent).toContain('Jordan Lee');
    expect(
      Array.from(document.querySelectorAll('.schedule-publish__employee-avatar')).map((avatar) =>
        avatar.textContent?.trim(),
      ),
    ).toEqual(['AM', 'JL']);
    expect(document.body.textContent).toContain('You can publish this schedule with warnings.');
    expect(document.body.textContent).toContain('2026-08-24 to 2026-08-30');
    expect(wrapper.findComponent({ name: 'UaSelect' }).exists()).toBe(false);
    expect(wrapper.findComponent({ name: 'UaTextField' }).exists()).toBe(false);

    const publishButton = Array.from(document.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Publish Schedule',
    );
    expect(publishButton).toBeDefined();
    publishButton?.dispatchEvent(new Event('click', { bubbles: true }));
    publishButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(requestBodies).toEqual([request, request]);
    expect(wrapper.emitted('published')).toEqual([[3]]);

    wrapper.unmount();
  });

  it('replaces a stale publishable preview with blockers returned by publish', async () => {
    const vuetify = createVuetify({ components, directives });
    const pinia = createPinia();
    const request = { locationId: 7, startDate: '2026-08-24', endDate: '2026-08-30' };
    const currentEmployee = { userId: '77e6109e-ae1c-4f6c-96ce-60f15cc13725', displayName: 'Morgan Chen' };
    const staleEmployee = { userId: 'b5d00ed9-703f-43f9-a81b-59e5cd8909bf', displayName: 'Stale Employee' };
    const conflict = {
      id: 'conflict:scheduling:200:training:course-42:resource',
      firstSourceModule: 'scheduling',
      firstEventId: '200',
      secondSourceModule: 'training',
      secondEventId: 'course-42',
      resourceId: currentEmployee.userId,
      title: 'Assignment and Training',
      overlapStart: '2026-08-24T17:00:00Z',
      overlapEnd: '2026-08-24T18:00:00Z',
    };
    const preview = {
      scope: {
        ...request,
        timeZoneId: 'America/Vancouver',
        rangeStartUtc: '2026-08-24T07:00:00Z',
        rangeEndExclusiveUtc: '2026-08-31T07:00:00Z',
      },
      candidates: {
        shiftEntryCount: 1,
        assignmentEntryCount: 0,
        totalCount: 1,
      },
      affectedEmployees: [staleEmployee],
      warnings: {
        incompleteEmployees: [{ ...staleEmployee, missingShift: true, missingAssignment: false }],
        unassignedShifts: [],
        unassignedAssignments: [],
        conflicts: [{ ...conflict, id: 'stale-conflict', resourceId: staleEmployee.userId, title: 'Stale conflict' }],
      },
      blockers: { conflicts: [], shiftConflicts: [] },
      canPublish: true,
    };

    server.use(
      http.post('*/api/scheduling/publish/preview', () => HttpResponse.json(preview)),
      http.post('*/api/scheduling/publish', () =>
        HttpResponse.json(
          {
            ...preview,
            affectedEmployees: [currentEmployee],
            warnings: {
              ...preview.warnings,
              incompleteEmployees: [],
              conflicts: [{ ...conflict, id: 'new-warning', firstEventId: '300', title: 'New published conflict' }],
              restrictedWarnings: [
                {
                  eventId: '300',
                  sourceModule: 'scheduling',
                  resourceId: currentEmployee.userId,
                  message: 'This event has a conflict with an event you do not have permission to view.',
                  authorizedEvent: {
                    eventId: '300',
                    sourceModule: 'scheduling',
                    title: 'Authorized published shift',
                    start: conflict.overlapStart,
                    end: conflict.overlapEnd,
                    sourceEntityId: null,
                    timeZoneId: 'America/Vancouver',
                  },
                },
              ],
              hasRestrictedConflicts: true,
            },
            blockers: {
              shiftConflicts: [],
              conflicts: [conflict],
            },
            canPublish: false,
          },
          { status: 409 },
        ),
      ),
    );

    const wrapper = mount(CalendarSchedulingPublishModal, {
      props: { request },
      global: { plugins: [pinia, vuetify] },
      attachTo: document.body,
    });
    await flushPromises();

    expect(document.body.textContent).toContain(staleEmployee.displayName);
    expect(document.body.textContent).toContain('Stale conflict');
    const publishButton = Array.from(document.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Publish Schedule',
    );
    expect(publishButton).toHaveProperty('disabled', false);
    publishButton?.dispatchEvent(new Event('click', { bubbles: true }));
    await flushPromises();

    expect(document.body.textContent).toContain(currentEmployee.displayName);
    expect(document.body.textContent).toContain('Assignment and Training');
    expect(document.body.textContent).toContain('New published conflict');
    expect(document.body.textContent).toContain('Authorized published shift');
    expect(document.body.textContent).toContain('Conflict overlap · August 24, 2026, 10:00 AM - 11:00 AM');
    expect(document.body.textContent).not.toContain(staleEmployee.displayName);
    expect(document.body.textContent).not.toContain('Stale conflict');
    expect(document.body.textContent).toContain('The schedule changed. Review the updated publication findings.');
    expect(publishButton).toHaveProperty('disabled', true);
    expect(wrapper.emitted('published')).toBeUndefined();

    wrapper.unmount();
  });

  it('explains when there are no draft schedule items to publish', async () => {
    const vuetify = createVuetify({ components, directives });
    const pinia = createPinia();
    const request = { locationId: 7, startDate: '2026-08-24', endDate: '2026-08-30' };
    let finishPreview!: () => void;
    const pendingPreview = new Promise<void>((resolve) => {
      finishPreview = resolve;
    });
    server.use(
      http.post('*/api/scheduling/publish/preview', async () => {
        await pendingPreview;
        return HttpResponse.json({
          scope: {
            ...request,
            timeZoneId: 'America/Vancouver',
            rangeStartUtc: '2026-08-24T07:00:00Z',
            rangeEndExclusiveUtc: '2026-08-31T07:00:00Z',
          },
          candidates: { shiftEntryCount: 0, assignmentEntryCount: 0, totalCount: 0 },
          warnings: { incompleteEmployees: [], unassignedShifts: [], unassignedAssignments: [] },
          blockers: { conflicts: [], shiftConflicts: [] },
          canPublish: false,
        });
      }),
    );

    const wrapper = mount(CalendarSchedulingPublishModal, {
      props: { request },
      global: { plugins: [pinia, vuetify] },
      attachTo: document.body,
    });
    await flushPromises();
    expect(document.body.textContent).toContain('Checking this schedule');
    expect(document.body.textContent).toContain('Error (Blocking)');
    finishPreview();
    await flushPromises();

    expect(document.body.textContent).toContain(
      'No draft schedule items are available to publish for this location and date range.',
    );
    const information = document.querySelector('.schedule-publish__general-findings .ua-alert.bg-info');
    expect(information?.textContent).toContain('No draft schedule items');
    expect(information?.querySelector('.schedule-publish__sr-only')).toBeNull();
    const publishButton = Array.from(document.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Publish Schedule',
    );
    expect(publishButton).toHaveProperty('disabled', true);

    wrapper.unmount();
  });
});
