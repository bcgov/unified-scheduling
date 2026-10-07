import { afterEach, describe, expect, it } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { HttpResponse, http } from 'msw';
import { createVuetify } from 'vuetify';
import * as components from 'vuetify/components';
import * as directives from 'vuetify/directives';
import { createPinia } from 'pinia';
import CalendarSchedulingPublishModal from '@/modules/scheduling/CalendarSchedulingPublishModal.vue';
import { server } from '../../mocks/server';

afterEach(() => {
  document.body.innerHTML = '';
});

describe('CalendarSchedulingPublishModal', () => {
  it('previews and publishes the immutable calendar scope', async () => {
    const vuetify = createVuetify({ components, directives });
    const pinia = createPinia();
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
              { userId: '77e6109e-ae1c-4f6c-96ce-60f15cc13725', displayName: 'Alex Morgan' },
              { userId: 'b5d00ed9-703f-43f9-a81b-59e5cd8909bf', displayName: 'Jordan Lee' },
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

    expect(document.body.textContent).not.toContain('Shift entries');
    expect(document.body.textContent).not.toContain('2 employees do not have');
    expect(document.body.textContent).toContain('Alex Morgan');
    expect(document.body.textContent).toContain('Jordan Lee');
    expect(
      Array.from(document.querySelectorAll('.schedule-publish__employee-avatar')).map((avatar) =>
        avatar.textContent?.trim(),
      ),
    ).toEqual(['AM', 'JL']);
    expect(document.querySelectorAll('.schedule-publish__employee-error')).toHaveLength(2);
    expect(document.body.textContent).toContain('2026-08-24 to 2026-08-30');
    expect(wrapper.findComponent({ name: 'UaSelect' }).exists()).toBe(false);
    expect(wrapper.findComponent({ name: 'UaTextField' }).exists()).toBe(false);

    const publishButton = Array.from(document.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Publish Schedule',
    );
    expect(publishButton).toBeDefined();
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
      warnings: { incompleteEmployees: [], unassignedShifts: [], unassignedAssignments: [] },
      blockers: { conflicts: [], shiftConflicts: [] },
      canPublish: true,
    };

    server.use(
      http.post('*/api/scheduling/publish/preview', () => HttpResponse.json(preview)),
      http.post('*/api/scheduling/publish', () =>
        HttpResponse.json(
          {
            ...preview,
            blockers: {
              shiftConflicts: [],
              conflicts: [
                {
                  id: 'conflict:scheduling:200:training:course-42:resource',
                  firstSourceModule: 'scheduling',
                  firstEventId: '200',
                  secondSourceModule: 'training',
                  secondEventId: 'course-42',
                  resourceId: '77e6109e-ae1c-4f6c-96ce-60f15cc13725',
                  overlapStart: '2026-08-24T17:00:00Z',
                  overlapEnd: '2026-08-24T18:00:00Z',
                },
              ],
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

    const publishButton = Array.from(document.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Publish Schedule',
    );
    publishButton?.dispatchEvent(new Event('click', { bubbles: true }));
    await flushPromises();

    expect(document.body.textContent).toContain('1 unresolved conflict');
    expect(document.body.textContent).toContain('The schedule changed. Review the updated blockers before publishing.');
    expect(publishButton).toHaveProperty('disabled', true);
    expect(wrapper.emitted('published')).toBeUndefined();

    wrapper.unmount();
  });

  it('explains when there are no draft schedule items to publish', async () => {
    const vuetify = createVuetify({ components, directives });
    const pinia = createPinia();
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
          candidates: { shiftEntryCount: 0, assignmentEntryCount: 0, totalCount: 0 },
          warnings: { incompleteEmployees: [], unassignedShifts: [], unassignedAssignments: [] },
          blockers: { conflicts: [], shiftConflicts: [] },
          canPublish: false,
        }),
      ),
    );

    const wrapper = mount(CalendarSchedulingPublishModal, {
      props: { request },
      global: { plugins: [pinia, vuetify] },
      attachTo: document.body,
    });
    await flushPromises();

    expect(document.body.textContent).toContain(
      'No draft schedule items are available to publish for this location and date range.',
    );
    const publishButton = Array.from(document.querySelectorAll('button')).find(
      (button) => button.textContent?.trim() === 'Publish Schedule',
    );
    expect(publishButton).toHaveProperty('disabled', true);

    wrapper.unmount();
  });
});
