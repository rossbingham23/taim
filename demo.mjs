/**
 * TAIM live demo — runs a headed Chromium browser and walks through the UI.
 * Run with: node --experimental-vm-modules demo.mjs
 */

import { chromium } from '/home/rossb/.nvm/versions/node/v20.10.0/lib/node_modules/playwright/index.mjs';

const BASE  = 'http://localhost:3000';
const API   = 'http://localhost:5000';
const EMAIL = 'admin@taim.local';
const PASS  = 'taim-admin';

const pause = ms => new Promise(r => setTimeout(r, ms));

async function apiToken() {
  const res = await fetch(`${API}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: EMAIL, password: PASS }),
  });
  const { token } = await res.json();
  return token;
}

(async () => {
  console.log('Launching browser…');
  const browser = await chromium.launch({ headless: false, slowMo: 80 });
  const ctx = await browser.newContext({ viewport: { width: 1400, height: 900 } });
  const page = await ctx.newPage();

  // ── 1. Open the app ──────────────────────────────────────────────────────────
  console.log('Navigating to TAIM…');
  await page.goto(`${BASE}/login`);
  await page.waitForLoadState('networkidle');
  await pause(800);

  // ── 2. Log in ────────────────────────────────────────────────────────────────
  console.log('Logging in…');
  await page.getByPlaceholder('Email').fill(EMAIL);
  await page.getByPlaceholder('Password').fill(PASS);
  await pause(400);
  await page.getByRole('button', { name: /sign in/i }).click();
  await page.waitForURL(`${BASE}/`);
  await page.waitForLoadState('networkidle');
  await pause(1500);

  // ── 3. Task intake: submit a new goal ────────────────────────────────────────
  console.log('Submitting a new goal…');
  const textarea = page.locator('textarea').first();
  await textarea.fill('Launch an artisan ceramics e-commerce brand with a $500 starting budget');
  await page.locator('input[type="number"]').first().fill('500');
  await pause(600);
  await page.getByRole('button', { name: /launch ai team/i }).click();
  await pause(3000);

  // ── 4. View existing active task (if one exists) ──────────────────────────────
  const token = await apiToken();
  const tasks = await (await fetch(`${API}/api/tasks`, {
    headers: { Authorization: `Bearer ${token}` },
  })).json();
  const active = tasks.find(t => t.status === 'active') ?? tasks[0];

  if (active) {
    console.log(`Opening task: ${active.goal.slice(0, 60)}…`);
    await page.goto(`${BASE}/tasks/${active.id}`);
    await page.waitForLoadState('networkidle');
    await pause(3000);
  }

  // ── 5. Approvals ──────────────────────────────────────────────────────────────
  console.log('Navigating to Approvals…');
  await page.getByRole('link', { name: 'Approvals' }).click();
  await page.waitForLoadState('networkidle');
  await pause(2000);

  // ── 6. Reports ───────────────────────────────────────────────────────────────
  console.log('Navigating to Reports…');
  await page.getByRole('link', { name: 'Reports' }).click();
  await page.waitForLoadState('networkidle');
  await pause(2000);

  // ── 7. Settings ──────────────────────────────────────────────────────────────
  console.log('Navigating to Settings…');
  await page.getByRole('link', { name: 'Settings' }).click();
  await page.waitForLoadState('networkidle');
  await pause(2000);

  // ── 8. Back to Goals ─────────────────────────────────────────────────────────
  console.log('Back to Goals…');
  await page.getByRole('link', { name: 'Goals' }).click();
  await page.waitForLoadState('networkidle');
  await pause(2000);

  console.log('\n✅ Demo complete — browser stays open for 30s.');
  await pause(30000);

  await browser.close();
  console.log('Browser closed.');
})();
