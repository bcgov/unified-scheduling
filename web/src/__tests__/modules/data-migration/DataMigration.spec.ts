import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { http, HttpResponse } from 'msw';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { Permissions, type FeatureFlagsResponse } from '@/api-access/generated/models';
import { GetApiAuthUserResponse } from '@/api-access/generated/auth/auth.zod';
import DataMigration from '@/modules/data-migration/DataMigration.vue';
import { registerModule } from '@/modules/data-migration/DataMigrationModule';
import type { MigrationRun } from '@/modules/data-migration/dataMigrationApi';
import { useAuthStore } from '@/stores/auth';
import { useConfigStore } from '@/stores/config';
import { useNavigationStore } from '@/stores/NavigationStore';
import { useNotificationsStore } from '@/stores/notifications';
import { createTestApp } from '../../helpers/createTestApp';
import { server } from '../../mocks/server';
import type { RouteRecordRaw } from 'vue-router';

const permission = Permissions.DataMigrationOperate;
const flags = { DataMigration: { enabled: true } } as FeatureFlagsResponse;
const runId = '00000000-0000-0000-0000-000000000001';
let wrapper: VueWrapper | undefined;
let runs: MigrationRun[];
let reads: number;
let posts: number;
let postedDryRuns: boolean[];
let readFailure: boolean;
let postStatus: number;
let publishPostRun: boolean;
let releasePost: (() => void) | undefined;

beforeEach(() => {
  runs = [];
  reads = 0;
  posts = 0;
  postedDryRuns = [];
  readFailure = false;
  postStatus = 202;
  publishPostRun = true;
  vi.stubGlobal(
    'confirm',
    vi.fn(() => true),
  );
  server.use(
    http.get('*/api/data-migration/SS/runs', ({ request }) => {
      reads++;
      if (readFailure) return HttpResponse.error();
      const offset = Number(new URL(request.url).searchParams.get('offset'));
      return HttpResponse.json({ items: runs.slice(offset, offset + 200), total: runs.length });
    }),
    http.post('*/api/data-migration/SS/jobs/:job/runs', async ({ request, params }) => {
      posts++;
      const body = (await request.json()) as { dryRun: boolean };
      postedDryRuns.push(body.dryRun);
      expect(body).toEqual({ dryRun: expect.any(Boolean) });
      expect(request.credentials).toBe('same-origin');
      expect(request.headers.get('Content-Type')).toBe('application/json');
      if (releasePost) await new Promise<void>((resolve) => (releasePost = resolve));
      if (postStatus === 0) return HttpResponse.error();
      if (postStatus !== 202) return HttpResponse.json({ title: 'Refused' }, { status: postStatus });
      if (publishPostRun) runs = [{ id: runId, job: String(params.job), status: 'Running' }];
      return HttpResponse.json({ runId }, { status: 202 });
    }),
  );
});

afterEach(() => {
  wrapper?.unmount();
  wrapper = undefined;
  releasePost = undefined;
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

async function setup(enabled = true, permitted = true) {
  return createTestApp({
    featureFlags: enabled ? flags : {},
    permissions: permitted ? [permission] : [],
  });
}

async function render() {
  const app = await setup();
  wrapper = mount(DataMigration, { global: { plugins: app.mountPlugins } });
  await vi.waitFor(() => expect(buttons()[0]?.attributes('disabled')).toBeUndefined());
  return app;
}

function buttons() {
  return wrapper!.findAll('button');
}

describe('data migration guards', () => {
  it('accepts migration permissions in the generated auth response schema', () => {
    const user = GetApiAuthUserResponse.parse({
      isAuthenticated: true,
      isRegistered: true,
      name: null,
      authenticationType: 'TestAuth',
      claims: [],
      permissions: [Permissions.DataMigrationOperate, Permissions.DataMigrationRollback],
      userId: '00000000-0000-4000-8000-000000000001',
      homeLocationId: null,
    });
    expect(user.permissions).toContain(Permissions.DataMigrationOperate);
  });

  it.each([
    [true, false],
    [false, true],
    [false, false],
  ])('does not register or operate with feature=%s and permission=%s', async (enabled, permitted) => {
    const app = await setup(enabled, permitted);
    const routes: RouteRecordRaw[] = [];
    registerModule(routes);
    expect(routes).toHaveLength(0);
    wrapper = mount(DataMigration, { global: { plugins: app.mountPlugins } });
    await flushPromises();
    expect(buttons()).toHaveLength(0);
    expect(reads).toBe(0);
    expect(posts).toBe(0);
    expect(useNotificationsStore(app.pinia).message).toContain('DataMigrationOperate');
  });

  it('registers only an authenticated, unlinked route and rechecks access on navigation', async () => {
    const app = await setup();
    expect(app.router.hasRoute('DataMigration')).toBe(true);
    expect(useNavigationStore(app.pinia).links.some((link) => link.path === '/data-migration')).toBe(false);
    await app.router.push('/data-migration');
    expect(app.router.currentRoute.value.meta.requiresAuth).toBe(true);
    await app.router.push('/dashboard');
    useAuthStore(app.pinia).userInfo!.permissions = [];
    await app.router.push('/data-migration');
    expect(app.router.currentRoute.value.path).toBe('/dashboard');
    useAuthStore(app.pinia).userInfo!.permissions = [permission];
    useConfigStore(app.pinia).config!.featureFlags = {};
    await app.router.push('/data-migration');
    expect(app.router.currentRoute.value.path).toBe('/dashboard');
  });
});

describe('data migration apply controls', () => {
  it('starts a dry run by default, prevents duplicates, and retains accepted run state', async () => {
    await render();
    expect(buttons().map((button) => button.text())).toEqual(['Start My Team', 'Start Training', 'Start Scheduling']);
    expect(posts).toBe(0);
    releasePost = () => {};
    await buttons()[0]!.trigger('click');
    await vi.waitFor(() => expect(posts).toBe(1));
    expect(postedDryRuns).toEqual([true]);
    expect(window.confirm).not.toHaveBeenCalled();
    expect(buttons()[0]!.attributes('disabled')).toBeDefined();
    await buttons()[0]!.trigger('click');
    expect(posts).toBe(1);
    releasePost!();
    await vi.waitFor(() => expect(wrapper!.text()).toContain(runId));
    expect(wrapper!.text()).toContain('Running');
    expect(buttons()[0]!.attributes('disabled')).toBeDefined();
  });

  it('requires explicit confirmation before sending an apply job', async () => {
    await render();
    const checkbox = wrapper!.findAll<HTMLInputElement>('input[type="checkbox"]')[0]!;
    vi.mocked(window.confirm).mockReturnValueOnce(false);

    await checkbox.setValue(true);
    await buttons()[0]!.trigger('click');
    expect(posts).toBe(0);
    expect(window.confirm).toHaveBeenLastCalledWith(expect.stringContaining('commits changes'));

    await buttons()[0]!.trigger('click');
    await vi.waitFor(() => expect(posts).toBe(1));
    expect(postedDryRuns).toEqual([false]);
  });

  it('loads all pages and blocks a Running job even if it is not the latest run', async () => {
    runs = Array.from({ length: 200 }, (_, index) => ({ id: `old-${index}`, job: 'MyTeam', status: 'Completed' }));
    runs.push({ id: runId, job: 'MyTeam', status: 'Running' });
    const app = await setup();
    wrapper = mount(DataMigration, { global: { plugins: app.mountPlugins } });
    await vi.waitFor(() => expect(buttons()[1]?.attributes('disabled')).toBeUndefined());
    expect(reads).toBe(2);
    expect(buttons()[0]!.attributes('disabled')).toBeDefined();
    await buttons()[0]!.trigger('click');
    expect(posts).toBe(0);
  });

  it('rechecks run state before POST and refuses a job started by another operator', async () => {
    await render();
    runs = [{ id: runId, job: 'MyTeam', status: 'Running' }];
    await buttons()[0]!.trigger('click');
    await vi.waitFor(() => expect(wrapper!.text()).toContain(runId));
    expect(posts).toBe(0);
    expect(buttons()[0]!.attributes('disabled')).toBeDefined();
  });

  it('replaces an optimistic accepted run with newer server state during polling', async () => {
    publishPostRun = false;
    const app = await setup();
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    wrapper = mount(DataMigration, { global: { plugins: app.mountPlugins } });
    await vi.waitFor(() => expect(buttons()[0]?.attributes('disabled')).toBeUndefined());
    await buttons()[0]!.trigger('click');
    await vi.waitFor(() => expect(wrapper!.text()).toContain(`Run ID: ${runId} — Running`));
    expect(buttons()[0]!.attributes('disabled')).toBeDefined();

    runs = [{ id: runId, job: 'MyTeam', status: 'Completed' }];
    await vi.advanceTimersByTimeAsync(5000);
    await vi.waitFor(() => expect(wrapper!.text()).toContain(`Run ID: ${runId} — Completed`));
    expect(buttons()[0]!.attributes('disabled')).toBeUndefined();

    const newerId = '00000000-0000-0000-0000-000000000002';
    runs = [
      { id: newerId, job: 'MyTeam', status: 'Running' },
      { id: runId, job: 'MyTeam', status: 'Completed' },
    ];
    await vi.advanceTimersByTimeAsync(5000);
    await vi.waitFor(() => expect(wrapper!.text()).toContain(`Run ID: ${newerId} — Running`));
    expect(wrapper!.text()).not.toContain(`Run ID: ${runId}`);
    expect(buttons()[0]!.attributes('disabled')).toBeDefined();

    runs[0]!.status = 'Completed';
    await vi.advanceTimersByTimeAsync(5000);
    await vi.waitFor(() => expect(wrapper!.text()).toContain(`Run ID: ${newerId} — Completed`));
    expect(buttons()[0]!.attributes('disabled')).toBeUndefined();
  });

  it('fails closed on poll errors, recovers status, and stops polling after unmount', async () => {
    const app = await setup();
    vi.useFakeTimers({ toFake: ['setTimeout', 'clearTimeout'] });
    wrapper = mount(DataMigration, { global: { plugins: app.mountPlugins } });
    await vi.waitFor(() => expect(buttons()[0]?.attributes('disabled')).toBeUndefined());
    readFailure = true;
    await vi.advanceTimersByTimeAsync(5000);
    await vi.waitFor(() => expect(useNotificationsStore(app.pinia).message).toContain('Cannot load'));
    expect(buttons().every((button) => button.attributes('disabled') !== undefined)).toBe(true);
    readFailure = false;
    runs = [{ id: runId, job: 'Training', status: 'Running' }];
    await vi.advanceTimersByTimeAsync(5000);
    await vi.waitFor(() => expect(buttons()[0]?.attributes('disabled')).toBeUndefined());
    expect(buttons()[1]!.attributes('disabled')).toBeDefined();
    wrapper.unmount();
    wrapper = undefined;
    const previousReads = reads;
    await vi.advanceTimersByTimeAsync(15000);
    expect(reads).toBe(previousReads);
  });

  it.each([0, 403, 409, 503])('reports a failed/refused start (%s) rather than silent success', async (status) => {
    postStatus = status;
    const app = await render();
    await buttons()[0]!.trigger('click');
    await vi.waitFor(() => expect(posts).toBe(1));
    await vi.waitFor(() => expect(useNotificationsStore(app.pinia).color).toBe('error'));
    expect(useNotificationsStore(app.pinia).message).toContain(
      status === 409 ? '409' : status === 403 ? 'permission' : status === 0 ? 'network error' : 'failed',
    );
    expect(wrapper!.text()).not.toContain('Run ID:');
  });
});
