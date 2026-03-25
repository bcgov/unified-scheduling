import { defineStore } from 'pinia';
import type { SelectOption } from '@/types/select';

export const usePositionsStore = defineStore('positions', () => {
  const getSelectOptions = (): SelectOption[] => [
    { code: 'Chief Sheriff', description: 'Chief Sheriff' },
    { code: 'Superintendent', description: 'Superintendent' },
    { code: 'Staff Inspector', description: 'Staff Inspector' },
    { code: 'Inspector', description: 'Inspector' },
    { code: 'Staff Sergeant', description: 'Staff Sergeant' },
    { code: 'Sergeant', description: 'Sergeant' },
    { code: 'Deputy Sheriff', description: 'Deputy Sheriff' },
  ];

  return { getSelectOptions };
});
