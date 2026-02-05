import { ref } from 'vue'
import { defineStore } from 'pinia'

interface Event {
  id?: string
  title?: string
}

export const useSchedulingStore = defineStore('scheduling', () => {
  const events = ref<Event[]>([])

  const addEvents = (newEvents: Event[]) => {
    events.value.push(...newEvents)
  }

  return { events, addEvents }
})
