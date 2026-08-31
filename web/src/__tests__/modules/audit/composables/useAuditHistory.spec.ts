import { describe, expect, it } from 'vitest';
import { setActivePinia } from 'pinia';
import { flushPromises } from '@vue/test-utils';
import {
  getGetApiAuditHistoryMockHandler,
  getGetApiAuditHistoryResponseMock,
} from '@/api-access/generated/audit/audit.msw';
import { useAuditHistory } from '@/modules/audit/composables/useAuditHistory';
import { createTestApp } from '../../../helpers/createTestApp';
import { server } from '../../../mocks/server';

async function setupAudit() {
  const app = await createTestApp({ permissions: ['AuditRead'] });
  setActivePinia(app.pinia);
  return useAuditHistory();
}

describe('useAuditHistory', () => {
  it('sends a trimmed EntityPK filter when set', async () => {
    let capturedUrl: URL | undefined;
    server.use(
      getGetApiAuditHistoryMockHandler((info) => {
        capturedUrl = new URL(info.request.url);
        return getGetApiAuditHistoryResponseMock({ data: [], totalCount: 0 });
      }),
    );

    const audit = await setupAudit();
    audit.entityType.value = 'Shift';
    audit.entityPk.value = '  ab-123  ';

    await audit.applyFilters();
    await flushPromises();

    expect(capturedUrl?.searchParams.get('EntityPK')).toBe('ab-123');
    expect(capturedUrl?.searchParams.get('EntityType')).toBe('Shift');
  });

  it('omits EntityPK from the request when blank', async () => {
    let capturedUrl: URL | undefined;
    server.use(
      getGetApiAuditHistoryMockHandler((info) => {
        capturedUrl = new URL(info.request.url);
        return getGetApiAuditHistoryResponseMock({ data: [], totalCount: 0 });
      }),
    );

    const audit = await setupAudit();
    audit.entityType.value = 'Shift';
    audit.entityPk.value = '   ';

    await audit.applyFilters();
    await flushPromises();

    expect(capturedUrl?.searchParams.has('EntityPK')).toBe(false);
  });

  it('resets entityPk when filters are cleared', async () => {
    const audit = await setupAudit();
    audit.entityType.value = 'Shift';
    audit.entityPk.value = 'ab-123';

    audit.clearFilters();

    expect(audit.entityPk.value).toBeNull();
  });
});
