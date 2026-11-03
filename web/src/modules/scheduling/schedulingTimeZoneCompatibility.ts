import { IANAZone } from 'luxon';

const probeInstant = Date.UTC(2027, 0, 15, 12);
const requiredOffsets = new Map([
  ['America/Vancouver', -420],
  ['America/Edmonton', -360],
  ['America/Dawson_Creek', -420],
  ['America/Creston', -420],
]);

export class SchedulingTimeZoneCompatibilityError extends Error {
  constructor(readonly timeZoneId: string) {
    super(
      'Your browser or operating system does not support the current time-zone rules for this location. Update it before saving. No changes were saved.',
    );
    this.name = 'SchedulingTimeZoneCompatibilityError';
  }
}

type OffsetResolver = (timeZoneId: string, utcInstant: number) => number;

const resolveBrowserOffset: OffsetResolver = (timeZoneId, utcInstant) => IANAZone.create(timeZoneId).offset(utcInstant);

export function assertSchedulingTimeZoneCompatibility(
  timeZoneId: string | null | undefined,
  resolveOffset: OffsetResolver = resolveBrowserOffset,
): void {
  const expectedOffset = timeZoneId ? requiredOffsets.get(timeZoneId) : undefined;
  if (expectedOffset === undefined || !timeZoneId) return;

  // Probe the event's zone at a fixed winter instant, independently of today's date
  // and the browser's own zone. Never substitute offsets for actual conversions.
  try {
    if (resolveOffset(timeZoneId, probeInstant) === expectedOffset) return;
  } catch {
    // An unsupported or unresolvable affected zone must also prevent the write.
  }

  throw new SchedulingTimeZoneCompatibilityError(timeZoneId);
}
