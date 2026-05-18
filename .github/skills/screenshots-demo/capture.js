/**
 * Copilot Blazor Template — Screenshot & Demo Video Capture
 * 
 * Run: node .github/skills/screenshots-demo/capture.js
 * 
 * Expects:
 *   - App running at http://localhost:5177
 *   - Playwright installed: npm install playwright --prefix /tmp/pw-runner
 *   - Chromium installed: npx playwright install chromium
 */

const REPO_ROOT = require('path').resolve(__dirname, '..', '..', '..');
const SCREENSHOTS_DIR = `${REPO_ROOT}/docs/screenshots`;
const DEMO_DIR = `${REPO_ROOT}/docs/demo`;
const BASE = process.env.APP_URL || 'http://localhost:5177';

let chromium;
try {
  ({ chromium } = require('playwright'));
} catch {
  ({ chromium } = require('/tmp/pw-runner/node_modules/playwright'));
}

const fs = require('fs');
const os = require('os');
const path = require('path');

async function showBanner(page, text, duration = 2000) {
  await page.evaluate((t) => {
    let b = document.getElementById('demo-banner');
    if (!b) {
      b = document.createElement('div');
      b.id = 'demo-banner';
      b.style.cssText = 'position:fixed;top:0;left:0;right:0;padding:12px;background:rgba(0,0,0,0.85);color:white;text-align:center;font-size:18px;font-weight:bold;z-index:99999;font-family:system-ui';
      document.body.appendChild(b);
    }
    b.textContent = t;
  }, text);
  await page.waitForTimeout(duration);
  await page.evaluate(() => { const b = document.getElementById('demo-banner'); if (b) b.remove(); });
}

async function login(page, email, password) {
  await page.goto(`${BASE}/Account/Login`);
  await page.waitForLoadState('networkidle');
  await page.fill('input[name="Input.Email"]', email);
  await page.fill('input[name="Input.Password"]', password);
  await page.click('button[type="submit"]');
  await page.waitForURL('**/dashboard**', { timeout: 15000 }).catch(() => {});
  await page.waitForLoadState('networkidle');
}

async function captureScreenshots() {
  console.log('=== Capturing Screenshots ===');
  fs.mkdirSync(SCREENSHOTS_DIR, { recursive: true });

  const browser = await chromium.launch({ headless: true });
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 720 } });
  const page = await ctx.newPage();

  // Landing
  await page.goto(BASE);
  await page.waitForLoadState('networkidle');
  await page.screenshot({ path: `${SCREENSHOTS_DIR}/landing.png`, fullPage: true });
  console.log('  ✓ landing.png');

  // Login page
  await page.goto(`${BASE}/Account/Login`);
  await page.waitForLoadState('networkidle');
  await page.screenshot({ path: `${SCREENSHOTS_DIR}/login.png`, fullPage: true });
  console.log('  ✓ login.png');

  // Login as admin → Dashboard
  await login(page, 'admin@template.local', 'Admin123!');
  await page.waitForTimeout(2000); // InteractiveServer connect
  await page.screenshot({ path: `${SCREENSHOTS_DIR}/dashboard.png`, fullPage: true });
  console.log('  ✓ dashboard.png');

  // Admin page
  await page.goto(`${BASE}/admin`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(2000); // InteractiveServer connect
  await page.screenshot({ path: `${SCREENSHOTS_DIR}/admin.png`, fullPage: true });
  console.log('  ✓ admin.png');

  await browser.close();
  console.log('Screenshots complete.\n');
}

async function recordDemo() {
  console.log('=== Recording Demo Video ===');
  fs.mkdirSync(DEMO_DIR, { recursive: true });
  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'demo-'));

  const browser = await chromium.launch({ headless: true });
  const ctx = await browser.newContext({
    viewport: { width: 1280, height: 720 },
    recordVideo: { dir: tmpDir, size: { width: 1280, height: 720 } }
  });
  const page = await ctx.newPage();

  // Title
  await page.goto(BASE);
  await page.waitForLoadState('networkidle');
  await showBanner(page, '🚀 Copilot Blazor Template — Demo', 3000);

  // Landing
  await showBanner(page, 'Landing Page — Public, no auth required');
  await page.waitForTimeout(1500);

  // Login page
  await showBanner(page, 'Navigating to Login...');
  await page.goto(`${BASE}/Account/Login`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(1500);

  // Login
  await showBanner(page, 'Logging in as Admin (admin@template.local)');
  await page.fill('input[name="Input.Email"]', 'admin@template.local');
  await page.fill('input[name="Input.Password"]', 'Admin123!');
  await page.waitForTimeout(1000);
  await page.click('button[type="submit"]');
  await page.waitForURL('**/dashboard**', { timeout: 15000 }).catch(() => {});
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(1000);

  // Dashboard
  await showBanner(page, 'Dashboard — Authenticated users only');
  await page.waitForTimeout(2000);

  // Admin
  await showBanner(page, 'Navigating to Admin Panel...');
  await page.goto(`${BASE}/admin`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(1000);
  await showBanner(page, 'Admin Panel — Admin role required');
  await page.waitForTimeout(2000);

  // Logout
  await showBanner(page, 'Logging out...');
  const logoutForm = await page.$('form[action*="Logout"]');
  if (logoutForm) {
    const btn = await logoutForm.$('button');
    if (btn) await btn.click();
  } else {
    await page.goto(`${BASE}/Account/Logout`);
  }
  await page.waitForTimeout(2000);

  // Closing
  await page.goto(BASE);
  await page.waitForLoadState('networkidle');
  await showBanner(page, '✅ Built with GitHub Copilot', 3000);

  // Save video (MUST close page+context to flush)
  await page.close();
  await ctx.close();
  await browser.close();

  // Wait for file write, then copy
  await new Promise(r => setTimeout(r, 1500));
  const files = fs.readdirSync(tmpDir);
  const video = files.find(f => f.endsWith('.webm'));
  if (video) {
    const dest = `${DEMO_DIR}/copilot-blazor-template-demo.webm`;
    fs.copyFileSync(path.join(tmpDir, video), dest);
    console.log(`  ✓ ${dest}`);
  } else {
    console.error('  ✗ No video file found in', tmpDir, '— files:', files);
    process.exit(1);
  }
  fs.rmSync(tmpDir, { recursive: true, force: true });
  console.log('Demo recording complete.\n');
}

async function main() {
  // Verify app is running
  const http = require('http');
  await new Promise((resolve, reject) => {
    http.get(BASE, (res) => { res.resume(); resolve(); })
        .on('error', () => reject(new Error(`App not running at ${BASE}. Start it first.`)));
  });

  await captureScreenshots();
  await recordDemo();
  console.log('✅ All done! Screenshots in docs/screenshots/, demo in docs/demo/');
}

main().catch(e => { console.error('ERROR:', e.message); process.exit(1); });
