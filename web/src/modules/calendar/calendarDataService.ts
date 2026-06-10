import type { CalendarRegistry } from './registry/calendarRegistry';
import type { CalendarDataResponse, CalendarQueryContext, CalendarRuntimeContext } from './calendarTypes';

class CalendarDataService {
  private activeAbortController: AbortController | null = null;

  async loadData(
    runtimeContext: CalendarRuntimeContext,
    queryContext: CalendarQueryContext,
    registry: Pick<CalendarRegistry, 'getAvailableModuleContributions'>,
  ): Promise<CalendarDataResponse> {
    this.activeAbortController?.abort();

    const abortController = new AbortController();
    this.activeAbortController = abortController;

    const contributions = registry.getAvailableModuleContributions(runtimeContext, queryContext);
    const entries = await Promise.all(
      contributions.map(async (contribution) => {
        const data = await contribution.load(queryContext, { signal: abortController.signal });
        return [contribution.contributionId, data] as const;
      }),
    );

    if (abortController.signal.aborted) {
      throw new DOMException('Calendar request was aborted.', 'AbortError');
    }

    return {
      contributions: Object.fromEntries(entries),
    };
  }

  cancel() {
    this.activeAbortController?.abort();
    this.activeAbortController = null;
  }
}

export const calendarDataService = new CalendarDataService();
