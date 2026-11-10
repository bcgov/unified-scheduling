import type { UserTrainingRequest, UserTrainingResponse } from '@/api-access/generated/models';
import UserTrainingModal from '@/modules/myteam/components/UserTrainingModal.vue';
import { createTestApp } from '@/__tests__/helpers/createTestApp';
import { server } from '@/__tests__/mocks/server';
import { flushPromises, mount } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';

const userId = '95f91fd1-1111-2222-3333-9c0aeb4ca44b';
const training: UserTrainingResponse = {
  id: 123,
  userId,
  trainingId: 10,
  version: 1,
  trainingCode: 'CPR',
  trainingCategoryName: 'Medical',
  awardedOn: '2026-07-20T22:00:00Z',
  endingOn: '2026-07-21T01:00:00Z',
  allDay: false,
  expiryDate: '2026-12-31T23:59:59Z',
  noticeState: 'None',
  notes: '  keep me trimmed  ',
  createdOn: '2026-01-20T00:00:00Z',
  updatedOn: null,
};

const mountModal = async (
  mode: 'create' | 'edit' | 'renew' = 'create',
  record = training,
  timeZone: string | null = 'America/Vancouver',
) => {
  const { mountPlugins } = await createTestApp();
  return mount(UserTrainingModal, {
    props: {
      userId,
      timeZone: timeZone ?? undefined,
      mode,
      trainingOptions: [{ code: 10, description: 'CPR' }],
      training: mode === 'create' ? null : record,
    },
    global: {
      plugins: mountPlugins,
      stubs: {
        UaModal: { template: '<div><slot name="alerts" /><slot /><slot name="actions" /></div>' },
      },
    },
  });
};

type ModalWrapper = Awaited<ReturnType<typeof mountModal>>;
const save = async (wrapper: ModalWrapper) => {
  await wrapper
    .findAll('button')
    .find((button) => button.text() === 'Save')!
    .trigger('click');
  await flushPromises();
};

const fillTraining = async (wrapper: ModalWrapper, startTime = '15:00', endTime = '18:00') => {
  wrapper.findComponent({ name: 'UaSelect' }).vm.$emit('update:modelValue', 10);
  await wrapper.get('input#user-training-awarded-on').setValue('2026-07-20');
  await wrapper.get('input#user-training-ending-on').setValue('2026-07-20');
  await wrapper.get('input#user-training-awarded-time').setValue(startTime);
  await wrapper.get('input#user-training-ending-time').setValue(endTime);
  await flushPromises();
};

describe('UserTrainingModal', () => {
  let created: UserTrainingRequest[];
  let updated: UserTrainingRequest[];
  let expiryQueries: URL[];

  beforeEach(() => {
    created = [];
    updated = [];
    expiryQueries = [];
    server.use(
      http.post('*/api/training/user-trainings', async ({ request }) => {
        created.push((await request.json()) as UserTrainingRequest);
        return HttpResponse.json(training);
      }),
      http.put('*/api/training/user-trainings/:id', async ({ request }) => {
        updated.push((await request.json()) as UserTrainingRequest);
        return HttpResponse.json(training);
      }),
      http.get('*/api/training/user-trainings/expiry-date', ({ request }) => {
        expiryQueries.push(new URL(request.url));
        return HttpResponse.json({ expiryDate: '2026-12-31T23:59:59Z' });
      }),
    );
  });

  it('creates same-day training from 3 PM to 6 PM with the Vancouver offset', async () => {
    const wrapper = await mountModal();
    await fillTraining(wrapper);
    await save(wrapper);

    expect(created).toEqual([
      expect.objectContaining({
        userId,
        trainingId: 10,
        awardedOn: '2026-07-20T15:00:00.000-07:00',
        endingOn: '2026-07-20T18:00:00.000-07:00',
        allDay: false,
        expiryDate: null,
      }),
    ]);
    expect(expiryQueries.at(-1)?.searchParams.get('AwardedOn')).toBe('2026-07-20T18:00:00.000-07:00');
    expect(expiryQueries.at(-1)?.searchParams.get('UserId')).toBe(userId);
    expect(expiryQueries.at(-1)?.searchParams.get('AllDay')).toBe('false');
    expect(wrapper.emitted('saved')).toBeTruthy();
    expect(wrapper.emitted('close')).toBeTruthy();
  });

  it.each(['2026-07-20', '2026-07-22'])('saves all-day training through %s when times are blank', async (endDate) => {
    const wrapper = await mountModal();
    await fillTraining(wrapper, '', '');
    await wrapper.get('input#user-training-ending-on').setValue(endDate);
    await save(wrapper);

    expect(created[0]).toMatchObject({
      awardedOn: '2026-07-20T00:00:00.000Z',
      endingOn: `${endDate}T00:00:00.000Z`,
      allDay: true,
    });
  });

  it('clears selected times and saves the training as date-only', async () => {
    const wrapper = await mountModal();
    await fillTraining(wrapper);

    for (const id of ['user-training-awarded-time', 'user-training-ending-time']) {
      const field = wrapper.findAllComponents({ name: 'UaTextField' }).find((item) => item.props('id') === id)!;
      expect(field.text()).toContain('Optional');
      await field.get('.v-field__clearable [role="button"]').trigger('click');
      await flushPromises();
      expect((wrapper.get(`input#${id}`).element as HTMLInputElement).value).toBe('');
    }

    await save(wrapper);

    expect(created[0]).toMatchObject({
      awardedOn: '2026-07-20T00:00:00.000Z',
      endingOn: '2026-07-20T00:00:00.000Z',
      allDay: true,
    });
    expect(expiryQueries.at(-1)?.searchParams.get('AwardedOn')).toBe('2026-07-20T00:00:00.000Z');
    expect(expiryQueries.at(-1)?.searchParams.get('AllDay')).toBe('true');
  });

  it('waits for the To date before calculating expiry', async () => {
    const wrapper = await mountModal();
    wrapper.findComponent({ name: 'UaSelect' }).vm.$emit('update:modelValue', 10);
    await wrapper.get('input#user-training-awarded-on').setValue('2026-07-20');
    await flushPromises();

    expect(expiryQueries).toHaveLength(0);

    await wrapper.get('input#user-training-ending-on').setValue('2026-07-22');
    await flushPromises();

    expect(expiryQueries).toHaveLength(1);
    expect(expiryQueries[0]!.searchParams.get('AwardedOn')).toBe('2026-07-22T00:00:00.000Z');
  });

  it('recalculates from changes to To date and time but not From', async () => {
    const wrapper = await mountModal();
    await fillTraining(wrapper);
    const initialQueryCount = expiryQueries.length;

    await wrapper.get('input#user-training-awarded-on').setValue('2026-07-19');
    await wrapper.get('input#user-training-awarded-time').setValue('09:00');
    await flushPromises();
    expect(expiryQueries).toHaveLength(initialQueryCount);

    await wrapper.get('input#user-training-ending-on').setValue('2026-07-22');
    await flushPromises();
    expect(expiryQueries.at(-1)?.searchParams.get('AwardedOn')).toBe('2026-07-22T18:00:00.000-07:00');

    await wrapper.get('input#user-training-ending-time').setValue('19:30');
    await flushPromises();
    expect(expiryQueries.at(-1)?.searchParams.get('AwardedOn')).toBe('2026-07-22T19:30:00.000-07:00');
  });

  it('preserves a manually entered expiry when To changes', async () => {
    const wrapper = await mountModal();
    await fillTraining(wrapper);
    await wrapper.get('input#user-training-expiry-date').setValue('2027-06-01');
    const initialQueryCount = expiryQueries.length;

    await wrapper.get('input#user-training-ending-on').setValue('2026-07-22');
    await flushPromises();

    expect(expiryQueries).toHaveLength(initialQueryCount);
    expect((wrapper.get('input#user-training-expiry-date').element as HTMLInputElement).value).toBe('2027-06-01');
  });

  it('saves a manually selected expiry date at the end of that day in Vancouver', async () => {
    const wrapper = await mountModal();
    await fillTraining(wrapper);
    await wrapper.get('input#user-training-expiry-date').setValue('2027-06-01');
    await save(wrapper);

    expect(created[0]?.expiryDate).toBe('2027-06-01T23:59:59.999-07:00');
  });

  it('does not submit when required training is missing', async () => {
    const wrapper = await mountModal();
    await save(wrapper);

    expect(created).toHaveLength(0);
    expect(updated).toHaveLength(0);
    expect(wrapper.emitted('saved')).toBeFalsy();
  });

  it('shows validation instead of throwing when dates are missing', async () => {
    const wrapper = await mountModal();
    wrapper.findComponent({ name: 'UaSelect' }).vm.$emit('update:modelValue', 10);
    await save(wrapper);

    expect(created).toHaveLength(0);
    expect(wrapper.text()).toContain('From date is required.');
  });

  it('rejects an end time before the start time on the same day', async () => {
    const wrapper = await mountModal();
    await fillTraining(wrapper, '18:00', '15:00');
    await save(wrapper);

    expect(created).toHaveLength(0);
    expect(wrapper.text()).toContain('To date and time must be after From.');
  });

  it.each(['18:00', '00:00'])('allows timed training from %s until midnight the next day', async (startTime) => {
    const wrapper = await mountModal();
    await fillTraining(wrapper, startTime, '00:00');
    await wrapper.get('input#user-training-ending-on').setValue('2026-07-21');
    await save(wrapper);

    expect(created[0]).toMatchObject({
      awardedOn: `2026-07-20T${startTime}:00.000-07:00`,
      endingOn: '2026-07-21T00:00:00.000-07:00',
      allDay: false,
    });
  });

  it.each(['edit', 'renew'] as const)('preserves stored UTC instants in %s mode', async (mode) => {
    const wrapper = await mountModal(mode);
    expect((wrapper.get('input#user-training-awarded-on').element as HTMLInputElement).value).toBe('2026-07-20');
    expect((wrapper.get('input#user-training-ending-on').element as HTMLInputElement).value).toBe('2026-07-20');
    expect((wrapper.get('input#user-training-awarded-time').element as HTMLInputElement).value).toBe('15:00');
    expect((wrapper.get('input#user-training-ending-time').element as HTMLInputElement).value).toBe('18:00');
    await save(wrapper);

    const requests = mode === 'edit' ? updated : created;
    expect(requests).toHaveLength(1);
    expect(requests[0]!.allDay).toBe(false);
    expect(Date.parse(requests[0]!.awardedOn)).toBe(Date.parse(training.awardedOn));
    expect(Date.parse(requests[0]!.endingOn)).toBe(Date.parse(training.endingOn));
    expect(requests[0]!.expiryDate).toBe(training.expiryDate);
    expect(requests[0]!.notes).toBe('keep me trimmed');
    expect(wrapper.emitted('saved')).toBeTruthy();
    expect(wrapper.emitted('close')).toBeTruthy();
  });

  it('uses the winter offset when editing winter training', async () => {
    const wrapper = await mountModal('edit', {
      ...training,
      awardedOn: '2026-01-20T23:00:00Z',
      endingOn: '2026-01-21T02:00:00Z',
    });
    await save(wrapper);

    expect(updated[0]).toMatchObject({
      awardedOn: '2026-01-20T15:00:00.000-08:00',
      endingOn: '2026-01-20T18:00:00.000-08:00',
    });
  });

  it.each(['edit', 'renew'] as const)('preserves Edmonton instants and local dates in %s mode', async (mode) => {
    const wrapper = await mountModal(mode, training, 'America/Edmonton');
    expect((wrapper.get('input#user-training-awarded-time').element as HTMLInputElement).value).toBe('16:00');
    expect((wrapper.get('input#user-training-ending-time').element as HTMLInputElement).value).toBe('19:00');
    await save(wrapper);
    const request = (mode === 'edit' ? updated : created)[0]!;
    expect(request.awardedOn).toBe('2026-07-20T16:00:00.000-06:00');
    expect(Date.parse(request.endingOn)).toBe(Date.parse(training.endingOn!));
    expect(request.expiryDate).toBe(training.expiryDate);
  });

  it('keeps Vancouver on UTC-7 in December 2026', async () => {
    const wrapper = await mountModal('edit', {
      ...training,
      awardedOn: '2026-12-01T22:00:00Z',
      endingOn: '2026-12-02T01:00:00Z',
    });
    expect((wrapper.get('input#user-training-awarded-time').element as HTMLInputElement).value).toBe('15:00');
    await save(wrapper);
    expect(updated[0]?.awardedOn).toBe('2026-12-01T15:00:00.000-07:00');
  });

  it.each(['America/Vancouver', 'America/Edmonton'])(
    'shows the local preview expiry date in %s even on the following UTC date',
    async (timeZone) => {
      server.use(
        http.get('*/api/training/user-trainings/expiry-date', () =>
          HttpResponse.json({ expiryDate: '2026-08-20T05:59:59.999Z' }),
        ),
      );
      const wrapper = await mountModal('create', training, timeZone);
      await fillTraining(wrapper, '', '');
      expect((wrapper.get('input#user-training-expiry-date').element as HTMLInputElement).value).toBe('2026-08-19');
      await save(wrapper);
      expect(created[0]?.expiryDate).toBeNull(); // Persistence calculates the same expiry on the server.
    },
  );

  it('serializes a manually entered expiry at the end of the Edmonton date', async () => {
    const wrapper = await mountModal('create', training, 'America/Edmonton');
    await fillTraining(wrapper);
    await wrapper.get('input#user-training-expiry-date').setValue('2026-08-19');
    await save(wrapper);
    expect(created[0]?.expiryDate).toBe('2026-08-19T23:59:59.999-06:00');
  });

  it.each([null, 'Not/AZone'])(
    'rejects timed training and preview without a valid home timezone (%s)',
    async (timeZone) => {
      const wrapper = await mountModal('create', training, timeZone);
      await fillTraining(wrapper);
      await save(wrapper);
      expect(created).toHaveLength(0);
      expect(expiryQueries).toHaveLength(0);
      expect(wrapper.text()).toContain('home location with a valid timezone');
    },
  );

  it('retains date-only all-day training without a home timezone', async () => {
    const wrapper = await mountModal('create', training, null);
    await fillTraining(wrapper, '', '');
    await save(wrapper);
    expect(created[0]).toMatchObject({ allDay: true, awardedOn: '2026-07-20T00:00:00.000Z', expiryDate: null });
  });

  it('does not reinterpret an existing timed record as all-day when its timezone is missing', async () => {
    const wrapper = await mountModal('edit', training, null);
    await save(wrapper);
    expect(updated).toHaveLength(0);
    expect(wrapper.text()).toContain('home location with a valid timezone');
  });

  it.each([
    ['15:00', '', 'Invalid to time.'],
    ['', '18:00', 'Invalid from time.'],
    ['15:00', '15:00', 'To date and time must be after From.'],
  ])('rejects an invalid timed pair (%s, %s)', async (startTime, endTime, message) => {
    const wrapper = await mountModal();
    await fillTraining(wrapper, startTime, endTime);
    await save(wrapper);

    expect(created).toHaveLength(0);
    expect(wrapper.text()).toContain(message);
  });

  it.each([
    ['edit', '2026-07-20'],
    ['edit', '2026-07-22'],
    ['renew', '2026-07-20'],
    ['renew', '2026-07-22'],
  ] as const)('preserves all-day dates through %s ending %s', async (mode, endDate) => {
    const record = {
      ...training,
      allDay: true,
      awardedOn: '2026-07-20T00:00:00Z',
      endingOn: `${endDate}T00:00:00Z`,
    };
    const wrapper = await mountModal(mode, record);
    expect((wrapper.get('input#user-training-awarded-on').element as HTMLInputElement).value).toBe('2026-07-20');
    expect((wrapper.get('input#user-training-ending-on').element as HTMLInputElement).value).toBe(endDate);
    expect((wrapper.get('input#user-training-awarded-time').element as HTMLInputElement).value).toBe('');
    expect((wrapper.get('input#user-training-ending-time').element as HTMLInputElement).value).toBe('');
    await save(wrapper);

    expect((mode === 'edit' ? updated : created)[0]).toMatchObject({
      allDay: true,
      awardedOn: '2026-07-20T00:00:00.000Z',
      endingOn: `${endDate}T00:00:00.000Z`,
    });
  });

  it('rejects a timed sentinel end that resolves to the start timestamp', async () => {
    const wrapper = await mountModal('edit', { ...training, endingOn: '0001-01-01T00:00:00Z' });
    await save(wrapper);

    expect(updated).toHaveLength(0);
    expect(wrapper.text()).toContain('To date and time must be after From.');
  });
});
