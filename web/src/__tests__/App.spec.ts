import { describe, it, expect } from 'vitest'

import { mount } from '@vue/test-utils'
import App from '../App.vue'
import { createTestApp } from './helpers/createTestApp'

describe('App', async () => {
  const app = await createTestApp()

  it('mounts renders properly', () => {
    const wrapper = mount(App, {
      global: {
        plugins: [app.router],
      },
    })
    expect(wrapper.text()).toContain('Unified')
  })
})
