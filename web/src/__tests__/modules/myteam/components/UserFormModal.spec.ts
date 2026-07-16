import { afterEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount } from '@vue/test-utils';
import { HttpResponse, http } from 'msw';
import UserFormModal from '@/modules/myteam/components/UserFormModal.vue';
import {
  getPostApiUsersMockHandler,
  getPostApiUsersResponseMock,
  getPutApiUsersIdMockHandler,
  getPutApiUsersIdResponseMock,
  getPostApiUsersIdUploadPhotoMockHandler,
  getPostApiUsersIdUploadPhotoResponseMock,
} from '@/api-access/generated/users/users.msw';
import { server } from '../../../mocks/server';
import { createTestApp } from '../../../helpers/createTestApp';
import type { UserResponse } from '@/api-access/generated/models';
import { Gender } from '@/api-access/generated/models';

afterEach(() => {
  document.body.innerHTML = '';
  vi.restoreAllMocks();
});

const baseUser: UserResponse = {
  id: 'user-123',
  idirName: 'jdoe',
  idirId: null,
  isEnabled: true,
  firstName: 'Jane',
  lastName: 'Doe',
  email: 'jane.doe@example.com',
  gender: Gender.Female,
  rank: null,
  badgeNumber: null,
  homeLocationId: null,
  lastLogin: null,
  photoUrl: null,
  lastPhotoUpdate: null,
};

const validFormData = {
  firstName: 'Jane',
  lastName: 'Doe',
  email: 'jane.doe@example.com',
  idirName: 'jdoe',
  gender: Gender.Female,
  rank: 'Constable',
  badgeNumber: 'B001',
  homeLocationId: 1,
  isEnabled: true,
};

describe('UserFormModal — photo upload', () => {
  it('shows initials avatar placeholder when no photo', async () => {
    const app = await createTestApp();

    const wrapper = mount(UserFormModal, {
      props: { user: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    // photoPreviewUrl should be null (no photo, empty form)
    const vm = wrapper.vm as unknown as { photoPreviewUrl: string | null };
    expect(vm.photoPreviewUrl).toBeNull();

    wrapper.unmount();
  });

  it('shows current photo in edit mode when user has a photoUrl', async () => {
    const app = await createTestApp();
    const userWithPhoto: UserResponse = { ...baseUser, photoUrl: '/api/users/user-123/photo' };

    const wrapper = mount(UserFormModal, {
      props: { user: userWithPhoto },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    // photoPreviewUrl should reflect the user's existing photo URL
    const vm = wrapper.vm as unknown as { photoPreviewUrl: string | null };
    expect(vm.photoPreviewUrl).toBe('/api/users/user-123/photo');

    wrapper.unmount();
  });

  it('calls upload-photo after successful create when a file is selected', async () => {
    const app = await createTestApp();

    const createdUser = getPostApiUsersResponseMock({ id: 'new-user-id', photoUrl: null });
    const updatedUser = getPostApiUsersIdUploadPhotoResponseMock({
      id: 'new-user-id',
      photoUrl: '/api/users/new-user-id/photo',
    });

    let uploadPhotoCallCount = 0;

    server.use(
      getPostApiUsersMockHandler(async () => createdUser),
      getPostApiUsersIdUploadPhotoMockHandler(async () => {
        uploadPhotoCallCount++;
        return updatedUser;
      }),
    );

    const wrapper = mount(UserFormModal, {
      props: { user: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    // Set form data directly on the vm
    const vm = wrapper.vm as unknown as { formData: typeof validFormData; photoFile: File | null };
    Object.assign(vm.formData, validFormData);
    vm.photoFile = new File(['fake-image'], 'avatar.jpg', { type: 'image/jpeg' });

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((b) =>
      b.textContent?.includes('Add Member'),
    );
    expect(saveButton).toBeDefined();
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(uploadPhotoCallCount).toBe(1);
    expect(wrapper.emitted('created')).toBeTruthy();
    const emittedUser = wrapper.emitted('created')?.[0]?.[0] as UserResponse;
    expect(emittedUser?.photoUrl).toBe('/api/users/new-user-id/photo');

    wrapper.unmount();
  });

  it('calls upload-photo after successful update when a file is selected', async () => {
    const app = await createTestApp();

    const updatedMeta = getPutApiUsersIdResponseMock({ id: 'user-123', photoUrl: null });
    const updatedWithPhoto = getPostApiUsersIdUploadPhotoResponseMock({
      id: 'user-123',
      photoUrl: '/api/users/user-123/photo',
    });

    let uploadPhotoCallCount = 0;

    server.use(
      getPutApiUsersIdMockHandler(async () => updatedMeta),
      getPostApiUsersIdUploadPhotoMockHandler(async () => {
        uploadPhotoCallCount++;
        return updatedWithPhoto;
      }),
    );

    const wrapper = mount(UserFormModal, {
      props: { user: { ...baseUser, ...validFormData } },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as { photoFile: File | null };
    vm.photoFile = new File(['fake-image'], 'avatar.jpg', { type: 'image/jpeg' });

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((b) =>
      b.textContent?.includes('Save Changes'),
    );
    expect(saveButton).toBeDefined();
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(uploadPhotoCallCount).toBe(1);
    expect(wrapper.emitted('updated')).toBeTruthy();
    const emittedUser = wrapper.emitted('updated')?.[0]?.[0] as UserResponse;
    expect(emittedUser?.photoUrl).toBe('/api/users/user-123/photo');

    wrapper.unmount();
  });

  it('does not call upload-photo when no file is selected', async () => {
    const app = await createTestApp();

    const createdUser = getPostApiUsersResponseMock({ id: 'no-photo-user', photoUrl: null });
    let uploadPhotoCallCount = 0;

    server.use(
      getPostApiUsersMockHandler(async () => createdUser),
      getPostApiUsersIdUploadPhotoMockHandler(async () => {
        uploadPhotoCallCount++;
        return getPostApiUsersIdUploadPhotoResponseMock();
      }),
    );

    const wrapper = mount(UserFormModal, {
      props: { user: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as { formData: typeof validFormData };
    Object.assign(vm.formData, validFormData);
    // photoFile intentionally left null

    await flushPromises();

    const saveButton = Array.from(document.querySelectorAll('button')).find((b) =>
      b.textContent?.includes('Add Member'),
    );
    saveButton?.dispatchEvent(new Event('click', { bubbles: true }));

    await flushPromises();

    expect(uploadPhotoCallCount).toBe(0);
    expect(wrapper.emitted('created')).toBeTruthy();

    wrapper.unmount();
  });
});

describe('UserFormModal — retry after photo upload failure', () => {
  it('does not create a second user when retrying after a failed photo upload', async () => {
    const app = await createTestApp();

    const createdUser = getPostApiUsersResponseMock({ id: 'retry-user-id', photoUrl: null });
    const uploadedUser = getPostApiUsersIdUploadPhotoResponseMock({
      id: 'retry-user-id',
      photoUrl: '/api/users/retry-user-id/photo',
    });

    let createCallCount = 0;
    let updateCallCount = 0;
    let uploadAttempt = 0;

    server.use(
      getPostApiUsersMockHandler(async () => {
        createCallCount++;
        return createdUser;
      }),
      getPutApiUsersIdMockHandler(async () => {
        updateCallCount++;
        return getPutApiUsersIdResponseMock({ id: 'retry-user-id' });
      }),
      http.post('*/api/users/:id/upload-photo', async () => {
        uploadAttempt++;
        if (uploadAttempt === 1) {
          return HttpResponse.json({ detail: 'The file exceeds the maximum allowed size of 400 KB.' }, { status: 400 });
        }
        return HttpResponse.json(uploadedUser, { status: 200 });
      }),
    );

    const wrapper = mount(UserFormModal, {
      props: { user: null },
      global: { plugins: app.mountPlugins },
      attachTo: document.body,
    });

    await flushPromises();

    const vm = wrapper.vm as unknown as {
      formData: typeof validFormData;
      photoFile: File | null;
      apiErrorMessage: string;
      isEditMode: boolean;
    };
    Object.assign(vm.formData, validFormData);
    vm.photoFile = new File(['fake-image'], 'avatar.jpg', { type: 'image/jpeg' });

    await flushPromises();

    const getSaveButton = () =>
      Array.from(document.querySelectorAll('button')).find(
        (b) => b.textContent?.includes('Add Member') || b.textContent?.includes('Save Changes'),
      );

    // First attempt — create succeeds, photo upload fails
    getSaveButton()?.dispatchEvent(new Event('click', { bubbles: true }));
    await flushPromises();

    expect(createCallCount).toBe(1);
    expect(uploadAttempt).toBe(1);
    expect(vm.apiErrorMessage).toContain('exceeds the maximum allowed size');
    // Modal should not have closed — no 'created' event emitted yet
    expect(wrapper.emitted('created')).toBeFalsy();
    // Component should now be in edit mode against the created user
    expect(vm.isEditMode).toBe(true);

    // Retry — should update the same user, not create a new one
    getSaveButton()?.dispatchEvent(new Event('click', { bubbles: true }));
    await flushPromises();

    expect(createCallCount).toBe(1); // still only called once
    expect(updateCallCount).toBe(1); // retry went through the edit path
    expect(uploadAttempt).toBe(2);
    // The retry is now genuinely an update against the already-created user, so it
    // emits 'updated' rather than a second 'created' — the key guarantee is that no
    // duplicate user was created (createCallCount stayed at 1).
    expect(wrapper.emitted('created')).toBeFalsy();
    expect(wrapper.emitted('updated')).toBeTruthy();

    wrapper.unmount();
  });
});
