export function sanitizeMatrixClassToken(value: string | undefined, fallback: string) {
  const sanitized =
    value
      ?.trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-') ?? '';
  return sanitized.replace(/^-+|-+$/g, '') || fallback;
}

export function resolveMatrixStatusClass(status: string | undefined) {
  const normalizedStatus = sanitizeMatrixClassToken(status, 'active');

  if (normalizedStatus.includes('draft')) {
    return 'draft';
  }

  if (normalizedStatus.includes('cancel')) {
    return 'cancelled';
  }

  return 'active';
}

export function toRgba(value: string, alpha: number) {
  const normalized = value.trim().replace(/^#/, '');

  if (!/^[\da-f]{3}$|^[\da-f]{6}$/i.test(normalized)) {
    return undefined;
  }

  const hex =
    normalized.length === 3
      ? normalized
          .split('')
          .map((digit) => `${digit}${digit}`)
          .join('')
      : normalized;

  const red = Number.parseInt(hex.slice(0, 2), 16);
  const green = Number.parseInt(hex.slice(2, 4), 16);
  const blue = Number.parseInt(hex.slice(4, 6), 16);
  const safeAlpha = Math.max(0, Math.min(1, alpha));

  return `rgba(${red}, ${green}, ${blue}, ${safeAlpha})`;
}
