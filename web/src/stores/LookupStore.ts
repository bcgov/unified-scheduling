import { computed, shallowRef } from 'vue';
import { defineStore } from 'pinia';
import type { LookupCodeResponse } from '@/api-access/generated/models';
import { LookupCodeTypes } from '@/api-access/generated/models';
import { getApiLookupCodeType } from '@/api-access/generated/lookup/lookup';
import type { SelectOption } from '@/types/select';
import { mapToSelectOptions } from '@/utils/select';

type LookupCodeTypesMap = Partial<Record<LookupCodeTypes, LookupCodeResponse[]>>;
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
 * onMounted(() => lookupStore.load(LookupCodeTypes.PositionTypes));
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
      codeTypes
        .filter((codeType) => !lookupCodeTypes.value[codeType])
        .map(async (codeType) => {
          const { data } = await getApiLookupCodeType(codeType);
          lookupCodeTypes.value = { ...lookupCodeTypes.value, [codeType]: data.value ?? [] };
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
    );
  };

  return { load, getSelectOptions, getDescriptionFromCode };
});
