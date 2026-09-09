import { describe, expect, it } from 'vitest';
import { buildReportCsvContent } from '@/modules/reports/reportCsvExport';

type ReportRow = {
  user: string;
  notes?: string | null;
  version?: number | null;
};

describe('reportCsvExport', () => {
  it('builds CSV content from columns and rows', () => {
    const csv = buildReportCsvContent<ReportRow>(
      [
        { header: 'User', value: (row) => row.user },
        { header: 'Version', value: (row) => row.version },
        { header: 'Notes', value: (row) => row.notes },
      ],
      [
        { user: 'Doe, Jane', version: 3, notes: 'Ready' },
        { user: 'Smith "Test"', version: null, notes: 'Line 1\nLine 2' },
      ],
    );

    expect(csv).toBe(['User,Version,Notes', '"Doe, Jane",3,Ready', '"Smith ""Test""",,"Line 1\nLine 2"'].join('\r\n'));
  });

  it('renders null and undefined values as empty fields', () => {
    const csv = buildReportCsvContent<ReportRow>(
      [
        { header: 'User', value: (row) => row.user },
        { header: 'Version', value: (row) => row.version },
        { header: 'Notes', value: (row) => row.notes },
      ],
      [{ user: 'User A', version: undefined, notes: null }],
    );

    expect(csv).toBe(['User,Version,Notes', 'User A,,'].join('\r\n'));
  });
});
