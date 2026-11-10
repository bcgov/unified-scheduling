import { afterEach, describe, expect, it, vi } from 'vitest';
import { enableAutoUnmount, mount } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import type { UserTrainingResponse } from '@/api-access/generated/models';
import UserTrainingVersionsModal from '@/modules/myteam/components/UserTrainingVersionsModal.vue';
import CalendarTrainingDetailModal from '@/modules/training/components/CalendarTrainingDetailModal.vue';
import type { TrainingCalendarEvent } from '@/modules/training/contributions/calendarTrainingEventsContribution';
import UaAlert from '@/shared/components/UaAlert.vue';
import UaModal from '@/shared/components/UaModal.vue';
import { useLocationsStore } from '@/stores/LocationsStore';

const userId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const event: TrainingCalendarEvent = {
  id: 'training.user-training.42',
  sourceModule: 'training',
  type: 'training.user-training',
  userId,
  userTrainingId: 42,
  trainingId: 7,
  title: 'Training: FIRST-AID',
  start: '2026-07-14',
  end: '2026-07-15',
  allDay: true,
};
const training: UserTrainingResponse = {
  id: 42,
  userId,
  trainingId: 7,
  version: 2,
  trainingCode: 'FIRST-AID',
  trainingCategoryName: 'Safety',
  awardedOn: '2026-07-14T00:00:00Z',
  endingOn: '2026-07-14T00:00:00Z',
  noticeState: 'None',
  notes: 'Current version',
};

enableAutoUnmount(afterEach);
afterEach(() => {
  document.body.innerHTML = '';
});

async function mountModal(calendarEvent: TrainingCalendarEvent = event) {
  const app = await createTestApp({ loadConfig: false });
  useLocationsStore(app.pinia).entities = [{ id: 1, name: 'Edmonton', timezone: 'America/Edmonton' }];
  const errorHandler = vi.fn();
  const wrapper = mount(CalendarTrainingDetailModal, {
    props: { event: calendarEvent },
    global: {
      plugins: app.mountPlugins,
      config: { errorHandler },
    },
    attachTo: document.body,
  });

  return { wrapper, errorHandler };
}

describe('CalendarTrainingDetailModal', () => {
  it('shows the requested training and only its versions, then forwards close', async () => {
    const previousVersion = { ...training, id: 41, version: 1, notes: 'Previous version' };
    server.use(
      http.get('*/api/trainings/users/:userId', ({ params }) => {
        expect(params.userId).toBe(userId);
        return HttpResponse.json([
          previousVersion,
          training,
          { ...training, id: 43, trainingId: 8, trainingCode: 'CPR' },
        ]);
      }),
    );
    const { wrapper, errorHandler } = await mountModal();

    await vi.waitFor(() => {
      expect(wrapper.findComponent(UserTrainingVersionsModal).exists()).toBe(true);
    });

    const versionsModal = wrapper.getComponent(UserTrainingVersionsModal);
    expect(versionsModal.props('training')).toEqual(training);
    expect(versionsModal.props('trainings')).toEqual([previousVersion, training]);
    expect(document.body.textContent).toContain('Current version');
    expect(wrapper.findComponent(UaAlert).exists()).toBe(false);
    expect(errorHandler).not.toHaveBeenCalled();

    versionsModal.vm.$emit('close');
    expect(wrapper.emitted('close')).toEqual([[]]);
  });

  it('displays timed training in the calendar timezone rather than the employee home timezone', async () => {
    server.use(
      http.get('*/api/trainings/users/:userId', () =>
        HttpResponse.json([
          {
            ...training,
            allDay: false,
            awardedOn: '2026-07-14T16:00:00Z',
            endingOn: '2026-07-14T17:00:00Z',
          },
        ]),
      ),
    );
    const { wrapper } = await mountModal({
      ...event,
      allDay: false,
      locationId: 1,
      timeZoneId: 'America/Vancouver',
    });

    await vi.waitFor(() => expect(wrapper.findComponent(UserTrainingVersionsModal).exists()).toBe(true));

    expect(wrapper.getComponent(UserTrainingVersionsModal).props('timeZone')).toBe('America/Vancouver');
    expect(document.body.textContent).toContain('2026-07-14 9:00 AM');
    expect(document.body.textContent).toContain('2026-07-14 10:00 AM');
  });

  it('shows the error alert after an HTTP 500 JSON response without a rendering exception', async () => {
    server.use(
      http.get('*/api/trainings/users/:userId', () =>
        HttpResponse.json({ detail: 'Service unavailable' }, { status: 500 }),
      ),
    );
    const { wrapper, errorHandler } = await mountModal();

    await vi.waitFor(() => {
      expect(wrapper.getComponent(UaAlert).text()).toContain('Unable to load this training record.');
    });

    expect(wrapper.getComponent(UaAlert).props('type')).toBe('error');
    expect(wrapper.findComponent(UserTrainingVersionsModal).exists()).toBe(false);
    expect(errorHandler).not.toHaveBeenCalled();

    wrapper.getComponent(UaModal).vm.$emit('close');
    expect(wrapper.emitted('close')).toEqual([[]]);
  });

  it.each([
    { description: 'an empty array', body: [] },
    { description: 'an array without the requested record', body: [{ ...training, id: 41 }] },
    { description: 'a malformed non-array response', body: { detail: 'Unexpected response' } },
  ])('shows not-found without a rendering exception for $description', async ({ body }) => {
    server.use(http.get('*/api/trainings/users/:userId', () => HttpResponse.json(body)));
    const { wrapper, errorHandler } = await mountModal();

    await vi.waitFor(() => {
      expect(wrapper.getComponent(UaAlert).text()).toContain('Training record not found.');
    });

    expect(wrapper.findComponent(UserTrainingVersionsModal).exists()).toBe(false);
    expect(errorHandler).not.toHaveBeenCalled();
  });
});
