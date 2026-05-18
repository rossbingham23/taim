import { test, expect } from '@playwright/test'

const GOAL = 'Build an AI-powered fitness tracking application for mobile users'

test('user can submit a goal and see their team assemble', async ({ page }) => {

  // ── Step 1: Unauthenticated visit → redirected to login ──────────────────────
  await page.goto('/')
  await expect(page).toHaveURL(/\/login/)
  await expect(page.locator('text=TAIM')).toBeVisible()

  // ── Step 2: Login ─────────────────────────────────────────────────────────────
  await page.fill('input[type=email]', 'admin@taim.local')
  await page.fill('input[type=password]', 'taim-admin')
  await page.click('button[type=submit]')

  await expect(page).toHaveURL('http://localhost:3000/')
  await expect(page.locator('h1:has-text("Submit a Goal")')).toBeVisible()

  // ── Step 3: Goals page shows the submission form ──────────────────────────────
  await expect(page.locator('textarea')).toBeVisible()
  await expect(page.locator('button:has-text("Launch AI Team")')).toBeVisible()

  // ── Step 4: Submit a goal ─────────────────────────────────────────────────────
  await page.fill('textarea', GOAL)
  await page.click('button:has-text("Launch AI Team")')

  // ── Step 5: Navigate to task detail page ─────────────────────────────────────
  await expect(page).toHaveURL(/\/tasks\/[0-9a-f-]{36}$/)
  await expect(page.locator(`text=${GOAL}`)).toBeVisible()

  // ── Step 6: Status starts at bootstrapping ───────────────────────────────────
  // Status badge text is one of: bootstrapping, active, failed:...
  const statusBadge = page.locator('text=/bootstrapping|active/i').first()
  await expect(statusBadge).toBeVisible({ timeout: 15_000 })

  // ── Step 7: Wait for team to assemble — agent cards appear ───────────────────
  // Section heading reads "Team (N)" once agents are registered
  await expect(page.locator('text=/^Team \\(\\d+\\)/')).toBeVisible({ timeout: 180_000 })

  // ── Step 8: Activity console shows log entries ───────────────────────────────
  await expect(page.locator('h2:has-text("Activity")')).toBeVisible()
  // At least one activity row should have appeared (any badge label)
  await expect(page.locator('text=/activated|proposing|KPI|strategy/i').first()).toBeVisible({ timeout: 30_000 })

  // ── Step 9: Navigate to system console via nav ───────────────────────────────
  await page.click('a:has-text("Console")')
  await expect(page).toHaveURL(/\/console/)
  await expect(page.locator('text=System Console')).toBeVisible()

  // At least one event entry visible in the system-wide feed
  const consoleEntries = page.locator('text=/LOG|STATUS|TEAM|REPORT/i')
  await expect(consoleEntries.first()).toBeVisible({ timeout: 10_000 })

  // ── Step 10: Navigate back to Goals, submitted goal appears in recent list ────
  await page.click('a:has-text("Goals")')
  await expect(page).toHaveURL('http://localhost:3000/')
  await expect(page.locator(`text=${GOAL}`).first()).toBeVisible()
})
