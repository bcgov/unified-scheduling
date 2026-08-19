import RRuleEditor from '@/components/recurrence/RRuleEditor.vue';
import { mount } from '@vue/test-utils';
import { beforeAll, describe, expect, it } from 'vitest';
import { createVuetify } from 'vuetify';
import * as components from 'vuetify/components';
import * as directives from 'vuetify/directives';

describe('RRuleEditor', () => {
  let vuetify: ReturnType<typeof createVuetify>;

  beforeAll(async () => {
    vuetify = createVuetify({ components, directives });
  });

  it('emits update and change when a valid form value changes', async () => {
    const wrapper = mount(RRuleEditor, {
      props: { startDate: '2026-07-31' },
      global: { plugins: [vuetify] },
    });

    const intervalInput = wrapper.find('input[type="number"]');
    await intervalInput.setValue('2');

    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toBe('RRULE:FREQ=WEEKLY;INTERVAL=2;BYDAY=FR;COUNT=1');
    expect(wrapper.emitted('change')?.at(-1)?.[0]).toBe('RRULE:FREQ=WEEKLY;INTERVAL=2;BYDAY=FR;COUNT=1');
  });

  it('emits invalid when the incoming rule cannot be represented', () => {
    const wrapper = mount(RRuleEditor, {
      props: {
        modelValue: 'RRULE:FREQ=WEEKLY;BYHOUR=9',
        startDate: '2026-07-31',
      },
      global: { plugins: [vuetify] },
    });

    expect(wrapper.text()).toContain('This recurrence rule uses advanced options that cannot be edited here.');
    expect(wrapper.emitted('invalid')?.[0]?.[0]).toContain('advanced options');
  });

  it('renders a single recurrence summary in read-only mode', () => {
    const wrapper = mount(RRuleEditor, {
      props: {
        modelValue: 'RRULE:FREQ=WEEKLY;INTERVAL=1;BYDAY=MO,WE,FR;COUNT=3',
        readOnly: true,
        startDate: '2026-07-31',
      },
      global: { plugins: [vuetify] },
    });

    expect(wrapper.text()).toBe('Repeats every week, on Monday, Wednesday and Friday, ends after 1 recurrence');
    expect(wrapper.find('input').exists()).toBe(false);
    expect(wrapper.find('[role="button"]').exists()).toBe(false);
  });

  it('preserves an unsupported rule until the user replaces it', async () => {
    const wrapper = mount(RRuleEditor, {
      props: {
        modelValue: 'RRULE:FREQ=WEEKLY;BYHOUR=9',
        startDate: '2026-07-31',
      },
      global: { plugins: [vuetify] },
    });

    expect(wrapper.emitted('update:modelValue')).toBeUndefined();

    await wrapper.find('button').trigger('click');

    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toBe('RRULE:FREQ=WEEKLY;INTERVAL=1;BYDAY=FR;COUNT=1');
  });

  it('defaults to ending on the provided until date and hides the indefinite option', async () => {
    const wrapper = mount(RRuleEditor, {
      props: {
        startDate: '2026-07-31',
        untilDate: '2026-08-15',
      },
      global: { plugins: [vuetify] },
    });

    expect(wrapper.text()).not.toContain('Never');
    expect((wrapper.find('input[type="date"]').element as HTMLInputElement).value).toBe('2026-08-15');

    const intervalInput = wrapper.find('input[type="number"]');
    await intervalInput.setValue('2');

    expect(wrapper.emitted('update:modelValue')?.at(-1)?.[0]).toContain('UNTIL=20260815T235959Z');
  });
});
