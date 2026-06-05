const DATE_ONLY_REGEX = /^\d{4}-\d{2}-\d{2}$/;

export function formatDateInput(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');

  return `${year}-${month}-${day}`;
}

export function toDateInputValue(value?: string | null): string | null {
  if (!value) {
    return null;
  }

  const dateOnly = value.split('T')[0];
  if (DATE_ONLY_REGEX.test(dateOnly)) {
    return dateOnly;
  }

  const parsedDate = new Date(value);
  if (Number.isNaN(parsedDate.getTime())) {
    return null;
  }

  return formatDateInput(parsedDate);
}

export function getTodayDateInputValue(): string {
  return formatDateInput(new Date());
}

export function toApiDate(value: string): string {
  return new Date(`${value}T00:00:00.000Z`).toISOString();
}
