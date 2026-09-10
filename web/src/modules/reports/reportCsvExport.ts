export type ReportCsvColumn<TRow> = {
  header: string;
  value: (row: TRow) => unknown;
};

export type ExportReportToCsvOptions<TRow> = {
  fileName: string;
  rows: readonly TRow[];
  columns: readonly ReportCsvColumn<TRow>[];
};

export function buildReportCsvContent<TRow>(columns: readonly ReportCsvColumn<TRow>[], rows: readonly TRow[]): string {
  const headers = columns.map((column) => escapeCsvField(column.header));
  const csvRows = rows.map((row) =>
    columns
      .map((column) => normalizeCsvFieldValue(column.value(row)))
      .map((value) => escapeCsvField(value))
      .join(','),
  );

  return [headers.join(','), ...csvRows].join('\r\n');
}

export function exportReportToCsv<TRow>({ fileName, rows, columns }: ExportReportToCsvOptions<TRow>): void {
  const csvContent = buildReportCsvContent(columns, rows);
  const csvContentWithBom = `\uFEFF${csvContent}`;

  const blob = new Blob([csvContentWithBom], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');

  link.href = url;
  link.download = fileName;
  link.click();

  URL.revokeObjectURL(url);
}

function normalizeCsvFieldValue(value: unknown): string {
  if (value === null || value === undefined) {
    return '';
  }

  if (value instanceof Date) {
    return value.toISOString();
  }

  return String(value);
}

function escapeCsvField(value: string): string {
  const safeValue = sanitizeSpreadsheetCell(value);

  if (safeValue.includes(',') || safeValue.includes('"') || safeValue.includes('\n') || safeValue.includes('\r')) {
    return `"${safeValue.replace(/"/g, '""')}"`;
  }

  return safeValue;
}

function sanitizeSpreadsheetCell(value: string): string {
  return /^[=+\-@\t\r\n]/.test(value) ? `'${value}` : value;
}
