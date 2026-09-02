import type { ZodError } from 'zod';

export const validationMessages = {
  required: 'Required',
  tooLong: 'Value is too long',
  invalidEmail: 'Invalid email format',
  invalid: 'Invalid value',
} as const;

const validationCodeToMessage: Record<string, string> = {
  REQUIRED: validationMessages.required,
  TOO_LONG: validationMessages.tooLong,
  INVALID_EMAIL: validationMessages.invalidEmail,
  INVALID: validationMessages.invalid,
};

const toErrorCodeList = (value: unknown): string[] => {
  if (Array.isArray(value)) {
    return value.filter((item): item is string => typeof item === 'string');
  }

  if (typeof value === 'string') {
    return [value];
  }

  return [];
};

export const getValidationMessageFromCode = (errorCode: string): string => {
  return validationCodeToMessage[errorCode] ?? errorCode;
};

export const mapToValidationErrors = (rawError: unknown): Record<string, string> | null => {
  const candidate = rawError as { errors?: Record<string, string[]> };

  if (!candidate?.errors || typeof candidate.errors !== 'object') {
    return null;
  }

  const apiFieldErrors = candidate.errors;
  const mappedErrors: Record<string, string> = {};

  for (const [fieldName, rawCodes] of Object.entries(apiFieldErrors)) {
    const errorCodes = toErrorCodeList(rawCodes);
    if (errorCodes.length === 0) {
      continue;
    }

    const camelCaseFieldName = fieldName.charAt(0).toLowerCase() + fieldName.slice(1);
    mappedErrors[camelCaseFieldName] = getValidationMessageFromCode(errorCodes[0]);
  }

  return Object.keys(mappedErrors).length > 0 ? mappedErrors : null;
};

export function getFieldErrors(error: ZodError): Record<string, string> {
  const errors: Record<string, string> = {};
  for (const issue of error.issues) {
    const fieldName = issue.path[0];
    if (typeof fieldName === 'string' && !errors[fieldName]) {
      errors[fieldName] =
        issue.code === 'invalid_type' || issue.code === 'invalid_value' ? validationMessages.required : issue.message;
    }
  }
  return errors;
}
