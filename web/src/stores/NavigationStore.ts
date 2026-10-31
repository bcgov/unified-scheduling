import { computed, shallowRef } from 'vue';
import { defineStore } from 'pinia';

export interface NavigationLink {
  name: string;
  path: string;
  class?: string;
}

export const useNavigationStore = defineStore('navigation', () => {
  const links = shallowRef<NavigationLink[]>([]);
  const adminPaths = ['/roles-permissions', '/training', '/reports', '/audit'];
  const adminLinks = computed(() => adminPaths.flatMap((path) => links.value.filter((link) => link.path === path)));
  const mainLinks = computed(() => links.value.filter((link) => !adminPaths.includes(link.path)));

  const registerLink = (link: NavigationLink) => {
    links.value = [...links.value, link];
  };

  return { links, mainLinks, adminLinks, registerLink };
});
