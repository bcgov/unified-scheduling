export interface UserNameParts {
  firstName?: string | null;
  lastName?: string | null;
  idirName?: string | null;
}

export function formatUserName(user: UserNameParts) {
  const fullName = [user.firstName, user.lastName].filter(Boolean).join(' ').trim();
  return fullName || user.idirName?.trim() || '';
}
