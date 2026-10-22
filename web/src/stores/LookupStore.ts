import { computed, shallowRef } from 'vue';
import { defineStore } from 'pinia';
import type { LookupCodeResponse } from '@/api-access/generated/models';
import { LookupCodeTypes } from '@/api-access/generated/models';
import { getApiLookupCodeType } from '@/api-access/generated/lookup/lookup';
import type { SelectOption } from '@/types/select';
import { mapToSelectOptions } from '@/utils/select';

type LookupCodeTypesMap = Partial<Record<LookupCodeTypes, LookupCodeResponse[]>>;
type OrderedLookupCodeResponse = LookupCodeResponse & { displayOrder?: number | null };
/**
 * LookupStore
 *
 * Manages fetching and caching of lookup code lists from the API.
 *
 * ## Loading data
 * Call `load()` with one or more `LookupCodeTypes` before reading options.
 * Already-loaded types are skipped automatically.
 *
 * ```ts
 * const lookupStore = useLookupStore();
 *
 * Single type
 * await lookupStore.load(LookupCodeTypes.PositionTypes);
 *
 * Multiple types at once (fetched in parallel)
 * await lookupStore.load(LookupCodeTypes.PositionTypes, LookupCodeTypes.AnotherType);
 * ```
 *
 * ## Getting select options
 * `getSelectOptions()` returns a sorted `SelectOption[]` ready to bind to a dropdown.
 *
 * ```ts
 * const options = lookupStore.getSelectOptions(LookupCodeTypes.PositionTypes);
 * [{ code: 'JUDGE', description: 'Judge' }, ...]
 * ```
 *
 * ## Getting a description from a code
 * `getDescriptionFromCode()` looks up the description for a known code. Falls back to the
 * raw `code` string if not found, so it is safe to use before `load()` resolves.
 *
 * ```ts
 * const description = lookupStore.getDescriptionFromCode(LookupCodeTypes.PositionTypes, 'JUDGE');
 *  'Judge'
 * ```
 *
 * ## Typical component usage
 *
 * ```vue
 * <script setup lang="ts">
 * import { computed, onMounted } from 'vue';
 * import { useLookupStore } from '@/stores/LookupStore';
 * import { LookupCodeTypes } from '@/api-access/generated/models';
 *
 * const lookupStore = useLookupStore();
 *
 * onMounted(() => { void lookupStore.load(LookupCodeTypes.PositionTypes).catch(() => {
 *   // Handle unavailable options in the component.
 * }); });
 *
 * const positionTypeOptions = computed(() =>
 *   lookupStore.getSelectOptions(LookupCodeTypes.PositionTypes)
 * );
 * </script>
 *
 * <template>
 *   <AppSelect :options="positionTypeOptions" />
 * </template>
 * ```
 */
export const useLookupStore = defineStore('lookup', () => {
  const lookupCodeTypes = shallowRef<LookupCodeTypesMap>({});
  const inFlight = new Map<LookupCodeTypes, Promise<void>>();

  const entityMap = computed(() => {
    const map: Partial<Record<LookupCodeTypes, Record<string, LookupCodeResponse>>> = {};
    for (const codeType in lookupCodeTypes.value) {
      const entries = lookupCodeTypes.value[codeType as LookupCodeTypes] ?? [];
      map[codeType as LookupCodeTypes] = Object.fromEntries(entries.filter((e) => e.code).map((e) => [e.code!, e]));
    }
    return map;
  });

  const load = async (...codeTypes: LookupCodeTypes[]): Promise<void> => {
    await Promise.all(
      codeTypes.map((codeType) => {
        if (lookupCodeTypes.value[codeType]) return Promise.resolve();
        const existing = inFlight.get(codeType);
        if (existing) return existing;

        const request = (async () => {
          const { data, error, execute } = getApiLookupCodeType(codeType, { options: { immediate: false } });
          await execute();
          if (error.value) throw error.value;
          if (!Array.isArray(data.value)) throw new Error(`Failed to load lookup ${codeType}.`);
          if (codeType === LookupCodeTypes.PositionTypes && !isValidPositionTypes(data.value)) {
            throw new Error('Position type display order is unavailable.');
          }
          lookupCodeTypes.value = { ...lookupCodeTypes.value, [codeType]: data.value };
        })().finally(() => inFlight.delete(codeType));
        inFlight.set(codeType, request);
        return request;
      }),
    );
  };

  const getDescriptionFromCode = (codeType: LookupCodeTypes, code: string): string => {
    return entityMap.value[codeType]?.[code]?.description ?? code;
  };

  const getSelectOptions = (codeType: LookupCodeTypes): SelectOption[] => {
    const entries = lookupCodeTypes.value[codeType] ?? [];
    return mapToSelectOptions(
      entries,
      (item) => item.code,
      (item) => item.description,
      (item) => (item as OrderedLookupCodeResponse).displayOrder,
    );
  };

  return { entityMap, load, getSelectOptions, getDescriptionFromCode };
});

function isValidPositionTypes(entries: LookupCodeResponse[]): boolean {
  const codes = new Set<string>();
  return (
    entries.length > 0 &&
    entries.every((entry) => {
      if (!entry || typeof entry.code !== 'string' || !entry.code.trim() || codes.has(entry.code)) return false;
      if (typeof entry.displayOrder !== 'number' || !Number.isInteger(entry.displayOrder) || entry.displayOrder <= 0) {
        return false;
      }
      codes.add(entry.code);
      return true;
    })
  );
}
