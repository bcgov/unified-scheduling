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
  return validationCodeToMessage[errorCode] ?? validationMessages.invalid;
};

export const mapProblemDetailsValidationErrors = (rawError: unknown): Record<string, string> | null => {
  const candidate = rawError as {
    data?: { extensions?: { errors?: unknown } };
    extensions?: { errors?: unknown };
  };

  const extensionErrors = candidate?.data?.extensions?.errors ?? candidate?.extensions?.errors;

  if (!extensionErrors || typeof extensionErrors !== 'object') {
    return null;
  }

  const apiFieldErrors = extensionErrors as Record<string, unknown>;
  const mappedErrors: Record<string, string> = {};

  for (const [fieldName, rawCodes] of Object.entries(apiFieldErrors)) {
    const errorCodes = toErrorCodeList(rawCodes);
    if (errorCodes.length === 0) {
      continue;
    }

    mappedErrors[fieldName] = getValidationMessageFromCode(errorCodes[0]);
  }

  return Object.keys(mappedErrors).length > 0 ? mappedErrors : null;
};
