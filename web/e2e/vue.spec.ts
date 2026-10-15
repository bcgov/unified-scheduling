import { expect, test, type Page } from '@playwright/test';

const locationId = 7;
const users = Array.from({ length: 36 }, (_, index) => ({
  id: `00000000-0000-0000-0000-${String(index + 1).padStart(12, '0')}`,
  idirName: `employee${index + 1}`,
  idirId: null,
  isEnabled: true,
  firstName: 'Employee',
  lastName: String(index + 1).padStart(2, '0'),
  email: `employee${index + 1}@example.test`,
  employeeNumber: String(1000 + index),
  gender: null,
  rank: null,
  badgeNumber: null,
  homeLocationId: locationId,
  lastLogin: null,
}));

test.beforeEach(async ({ page }) => {
  await mockApplicationApi(page);
});

test('generates a multi-page seven-day schedule PDF with published content and all columns', async ({
  page,
}, testInfo) => {
  await page.goto(
    '/calendar?mode=print&view=schedule&locationId=7&startDate=2026-08-24&endDate=2026-08-31&period=week',
  );

  await expect(page.locator('#calendar-print-root')).toHaveAttribute('data-render-state', 'complete');
  await expect(page.getByRole('heading', { name: 'August 24, 2026 - August 30, 2026' })).toBeVisible();
  await expect(page.getByText('Employee 01', { exact: true })).toBeVisible();
  await expect(page.getByRole('article', { name: 'Published shift 1', exact: true })).toBeVisible();
  await expect(page.getByRole('article', { name: 'DRAFT SECRET SHIFT', exact: true })).toHaveCount(0);
  await expect(page.getByRole('article', { name: 'CANCELLED SECRET SHIFT', exact: true })).toHaveCount(0);
  await expect(page.getByRole('article', { name: 'UNKNOWN SECRET SHIFT', exact: true })).toHaveCount(0);

  await expect(page.getByRole('columnheader')).toHaveCount(8);
  const columnsFit = await page.locator('.calendar-matrix-grid__table').evaluate((table) => {
    const tableRect = table.getBoundingClientRect();
    const headers = [...table.querySelectorAll<HTMLElement>('[role="columnheader"]')];
    return headers.every((header) => {
      const rect = header.getBoundingClientRect();
      return rect.left >= tableRect.left - 1 && rect.right <= tableRect.right + 1 && rect.width > 0;
    });
  });
  expect(columnsFit).toBe(true);

  await page.emulateMedia({ media: 'print' });
  const pdf = await page.pdf({
    format: 'Letter',
    landscape: true,
    printBackground: true,
    scale: 0.8,
    margin: { top: '0.25in', right: '0.25in', bottom: '0.25in', left: '0.25in' },
  });
  expect(pdf.subarray(0, 5).toString()).toBe('%PDF-');
  expect(countPdfPages(pdf)).toBeGreaterThan(1);
  await testInfo.attach('schedule-view.pdf', { body: pdf, contentType: 'application/pdf' });
});

test('generates a one-day assignment PDF with published assignment details', async ({ page }, testInfo) => {
  await page.goto(
    '/calendar?mode=print&view=assignments&locationId=7&startDate=2026-08-24&endDate=2026-08-25&period=day',
  );

  await expect(page.locator('#calendar-print-root')).toHaveAttribute('data-render-state', 'complete');
  await expect(page.getByRole('heading', { name: 'August 24, 2026 - August 24, 2026' })).toBeVisible();
  await expect(page.getByText('Court Coverage', { exact: true }).first()).toBeVisible();
  await expect(page.getByRole('button', { name: /Published court assignment/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /DRAFT SECRET ASSIGNMENT/ })).toHaveCount(0);

  await page.emulateMedia({ media: 'print' });
  const pdf = await page.pdf({ format: 'Letter', landscape: true, printBackground: true });
  expect(pdf.subarray(0, 5).toString()).toBe('%PDF-');
  expect(countPdfPages(pdf)).toBe(1);
  await testInfo.attach('assignment-view.pdf', { body: pdf, contentType: 'application/pdf' });
});

async function mockApplicationApi(page: Page) {
  await page.route('**/api/**', async (route) => {
    const url = new URL(route.request().url());
    const json = (body: unknown) =>
      route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(body) });

    switch (url.pathname) {
      case '/api/config':
        return json({
          featureFlags: {
            Calendar: { source: 'Calendar', enabled: true, calendarMatrixTest: false },
            Scheduling: { source: 'Scheduling', enabled: true },
            UserManagement: { source: 'UserManagement', enabled: true },
          },
          applicationName: 'Unified Scheduling',
          supportEmail: null,
        });
      case '/api/auth/user':
        return json({
          isAuthenticated: true,
          isRegistered: true,
          name: 'PDF Test User',
          authenticationType: 'test',
          claims: [],
          permissions: ['ShiftsView', 'AssignmentsView'],
          userId: users[0].id,
          homeLocationId: locationId,
        });
      case '/api/location/all':
        return json([
          {
            id: locationId,
            agencyId: 'A7',
            name: 'Seven Detachment',
            justinLocationCode: 'SEVEN',
            parentLocationId: null,
            expiryDate: null,
            regionId: null,
            timezone: 'America/Vancouver',
            concurrencyToken: 1,
          },
        ]);
      case '/api/users':
        return json(users);
      case '/api/calendar/events':
        return json({ moduleId: 'calendar', contributionId: 'calendar.events', events: [] });
      case '/api/scheduling/calendar/events':
        return json({ moduleId: 'scheduling', contributionId: 'scheduling.events', events: schedulingEvents() });
      case '/api/scheduling/assignment-definitions':
        return json([
          {
            id: 101,
            locationId,
            name: 'Court Coverage',
            description: 'Court assignment',
            categoryId: 1,
            categoryName: 'Operations',
            subCategoryId: 2,
            subCategoryName: 'Court',
            color: '#2464a4',
            defaultStartTime: '09:00:00',
            defaultEndTime: '17:00:00',
            defaultCapacity: 1,
            effectiveDateUtc: '2026-01-01T00:00:00Z',
            expiryDateUtc: null,
          },
        ]);
      default:
        throw new Error(`Unexpected API request: ${route.request().method()} ${url.pathname}`);
    }
  });
}

function schedulingEvents() {
  const shifts = users.map((user, index) => ({
    id: `scheduling.shift-entry.${index + 1}`,
    shiftEntryId: index + 1,
    eventId: index + 1,
    userIds: [user.id],
    resourceIds: [user.id],
    type: 'scheduling.shift',
    sourceModule: 'scheduling',
    title: `Published shift ${index + 1}`,
    start: '2026-08-24T16:00:00Z',
    end: '2026-08-25T00:00:00Z',
    eventTypeCode: 'shift',
    statusTypeCode: 'Published',
    locationId,
  }));

  return [
    ...shifts,
    createHiddenEvent('draft', 'DRAFT SECRET SHIFT'),
    createHiddenEvent('cancelled', 'CANCELLED SECRET SHIFT'),
    createHiddenEvent('unexpected', 'UNKNOWN SECRET SHIFT'),
    {
      id: 'scheduling.assignment-entry.501',
      assignmentEntryId: 501,
      assignmentDefinitionId: 101,
      eventId: 501,
      userIds: [users[0].id],
      resourceIds: [users[0].id],
      assignedUserIds: [users[0].id],
      type: 'scheduling.assignment',
      sourceModule: 'scheduling',
      title: 'Published court assignment',
      start: '2026-08-24T17:00:00Z',
      end: '2026-08-24T18:00:00Z',
      eventTypeCode: 'assignment',
      statusTypeCode: 'Active',
      locationId,
      capacity: 1,
      assignedUserCount: 1,
    },
    {
      ...createHiddenEvent('draft', 'DRAFT SECRET ASSIGNMENT'),
      id: 'scheduling.assignment-entry.502',
      assignmentEntryId: 502,
      assignmentDefinitionId: 101,
      type: 'scheduling.assignment',
      eventTypeCode: 'assignment',
    },
  ];
}

function createHiddenEvent(statusTypeCode: string, title: string) {
  return {
    id: `scheduling.shift-entry.${statusTypeCode}`,
    shiftEntryId: 900,
    eventId: 900,
    userIds: [users[0].id],
    resourceIds: [users[0].id],
    type: 'scheduling.shift',
    sourceModule: 'scheduling',
    title,
    start: '2026-08-24T16:00:00Z',
    end: '2026-08-25T00:00:00Z',
    eventTypeCode: 'shift',
    statusTypeCode,
    locationId,
  };
}

function countPdfPages(pdf: Buffer) {
  return pdf.toString('latin1').match(/\/Type\s*\/Page\b/g)?.length ?? 0;
}
