import { shallowRef } from 'vue';
import { defineStore } from 'pinia';

export interface NavigationLink {
  name: string;
  path: string;
  class?: string;
}

export const useNavigationStore = defineStore('navigation', () => {
  const links = shallowRef<NavigationLink[]>([]);

  const registerLink = (link: NavigationLink) => {
    links.value.push(link);
  };

  return { links: links, registerLink };
});
