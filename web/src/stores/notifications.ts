import { defineStore } from 'pinia';
import { ref } from 'vue';

export const useNotificationsStore = defineStore('notifications', () => {
  const visible = ref(false);
  const message = ref('');
  const color = ref<'info' | 'success' | 'error'>('info');
  const pending = ref(false);
  const timeout = ref(-1);

  function show(text: string, type: typeof color.value, isPending = false) {
    message.value = text;
    color.value = type;
    pending.value = isPending;
    timeout.value = !isPending && type === 'success' ? 5000 : -1;
    visible.value = true;
  }

  function dismiss() {
    if (!pending.value) visible.value = false;
  }

  return { visible, message, color, pending, timeout, show, dismiss };
});
