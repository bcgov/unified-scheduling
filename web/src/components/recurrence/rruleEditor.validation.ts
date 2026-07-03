import type { RRuleEditorModel, ValidationResult } from './rruleEditor.types';

const isPositiveInteger = (value: unknown): value is number => Number.isInteger(value) && Number(value) > 0;

export function validateRRuleEditorModel(model: RRuleEditorModel): ValidationResult {
  if (!isPositiveInteger(model.interval)) {
    return { valid: false, reason: 'Interval must be a positive whole number.' };
  }

  if (model.frequency === 'WEEKLY' && model.weekdays.length === 0) {
    return { valid: false, reason: 'Select at least one weekday.' };
  }

  if (model.frequency === 'MONTHLY' && model.monthlyMode === 'monthday') {
    if (!isPositiveInteger(model.monthDay) || model.monthDay < 1 || model.monthDay > 31) {
      return { valid: false, reason: 'Day of month must be between 1 and 31.' };
    }
  }

  if (model.frequency === 'MONTHLY' && model.monthlyMode === 'nth-weekday') {
    const setPosition = model.nthWeekday?.setPosition;
    const validSetPositions = [1, 2, 3, 4, -1];

    if (!model.nthWeekday?.weekday || !validSetPositions.includes(setPosition ?? 0)) {
      return { valid: false, reason: 'Select a valid monthly weekday pattern.' };
    }
  }

  if (model.endMode === 'count' && !isPositiveInteger(model.count)) {
    return { valid: false, reason: 'Occurrence count must be a positive whole number.' };
  }

  if (model.endMode === 'until' && !model.until) {
    return { valid: false, reason: 'Select an end date.' };
  }

  return { valid: true };
}
