/**
 * Generates a CSV file from tabular data and triggers a browser download.
 */
export function exportToCsv(filename: string, headers: string[], rows: string[][]) {
  const escapeCsvField = (field: string): string => {
    if (field.includes(',') || field.includes('"') || field.includes('\n')) {
      return `"${field.replace(/"/g, '""')}"`;
    }
    return field;
  };

  const csvLines = [headers.map(escapeCsvField).join(','), ...rows.map((row) => row.map(escapeCsvField).join(','))];
  const csvContent = csvLines.join('\n');

  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}
