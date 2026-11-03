const buildAuthUrl = (path: string) => {
  const baseUrl = new URL(import.meta.env.BASE_URL, window.location.origin);
  return new URL(`api/auth/${path}`, baseUrl);
};

export const toApplicationReturnPath = (returnPath: string) => {
  const baseUrl = new URL(import.meta.env.BASE_URL, window.location.origin);
  const candidate = new URL(returnPath, window.location.origin);

  if (candidate.origin !== window.location.origin) {
    return baseUrl.pathname;
  }

  const basePath = baseUrl.pathname.endsWith('/') ? baseUrl.pathname : `${baseUrl.pathname}/`;
  const candidatePath = candidate.pathname;
  const isAlreadyPrefixed = candidatePath === basePath.slice(0, -1) || candidatePath.startsWith(basePath);
  const pathname =
    basePath === '/' || isAlreadyPrefixed ? candidatePath : `${basePath}${candidatePath.replace(/^\/+/, '')}`;

  return `${pathname}${candidate.search}${candidate.hash}`;
};

export const redirectToLogin = (
  returnPath = `${window.location.pathname}${window.location.search}${window.location.hash}`,
) => {
  const loginUrl = buildAuthUrl('login');
  loginUrl.searchParams.set('redirectUri', toApplicationReturnPath(returnPath));
  window.location.href = loginUrl.toString();
};

export const redirectToLogout = () => {
  window.location.href = buildAuthUrl('logout').toString();
};
