import { useFetchAPI } from '@/api-access/useFetchAPI';

export const migrationJobs = [
  { job: 'MyTeam', label: 'My Team' },
  { job: 'Training', label: 'Training' },
  { job: 'Scheduling', label: 'Scheduling' },
] as const;
export type MigrationJob = (typeof migrationJobs)[number]['job'];
export type MigrationRun = { id: string; job: string; status: string };
export type MigrationRuns = { items: MigrationRun[]; total: number };
export type MigrationAccepted = { runId: string };

export function getMigrationRuns(offset: number) {
  return useFetchAPI<MigrationRuns>(
    { url: '/api/data-migration/SS/runs', method: 'GET', params: { offset, limit: 200 } },
    { options: { immediate: false }, fetchOptions: { cache: 'no-store', credentials: 'same-origin' } },
  );
}

export function startMigration(job: MigrationJob) {
  return useFetchAPI<MigrationAccepted>(
    {
      url: `/api/data-migration/SS/jobs/${job}/runs`,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: { dryRun: false },
    },
    { options: { immediate: false }, fetchOptions: { credentials: 'same-origin' } },
  );
}
