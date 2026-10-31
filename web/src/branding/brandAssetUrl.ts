/** Resolve public asset paths against the deployed app base; preserve external image URLs. */
export function resolveBrandAssetUrl(path?: string | null, baseUrl = import.meta.env.BASE_URL): string {
  const asset = path?.trim() ?? '';
  if (!asset || /^(?:https?:|data:|blob:|\/\/)/i.test(asset)) return asset;
  const base = baseUrl.endsWith('/') ? baseUrl : `${baseUrl}/`;
  if (base !== '/' && asset.startsWith(base)) return asset;
  return `${base}${asset.replace(/^\/+/, '')}`;
}
