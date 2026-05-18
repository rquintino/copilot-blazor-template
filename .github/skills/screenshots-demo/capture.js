/**
 * Copilot Blazor Template — Screenshot & Demo Video Capture
 *
 * Config-driven. Reads docs/screenshots.config.json (or $SCREENSHOTS_CONFIG).
 *
 * Run via scripts/demo.sh (recommended), or directly:
 *   node .github/skills/screenshots-demo/capture.js
 *
 * Expects the app to already be running at config.baseUrl.
 */

const fs = require('fs');
const os = require('os');
const path = require('path');

const REPO_ROOT = path.resolve(__dirname, '..', '..', '..');
const CONFIG_PATH = process.env.SCREENSHOTS_CONFIG || `${REPO_ROOT}/docs/screenshots.config.json`;
const SCREENSHOTS_DIR = `${REPO_ROOT}/docs/screenshots`;
const DEMO_DIR = `${REPO_ROOT}/docs/demo`;

let chromium;
try {
  ({ chromium } = require('playwright'));
} catch {
  ({ chromium } = require('/tmp/pw-runner/node_modules/playwright'));
}

function loadConfig() {
  if (!fs.existsSync(CONFIG_PATH)) {
    throw new Error(`Config not found: ${CONFIG_PATH}`);
  }
  const cfg = JSON.parse(fs.readFileSync(CONFIG_PATH, 'utf8'));
  cfg.baseUrl = process.env.APP_URL || cfg.baseUrl || 'http://localhost:5177';
  cfg.viewport = cfg.viewport || { width: 1280, height: 720 };
  cfg.credentials = cfg.credentials || {};
  cfg.screenshots = cfg.screenshots || [];
  cfg.demo = cfg.demo || null;
  return cfg;
}

function resolveCreds(cfg, ref) {
  if (!ref) return null;
  const c = cfg.credentials[ref];
  if (!c) throw new Error(`Unknown credentials ref: ${ref}`);
  return c;
}

async function login(page, baseUrl, creds) {
  await page.goto(`${baseUrl}/Account/Login`);
  await page.waitForLoadState('networkidle');
  await page.fill('input[name="Input.Email"]', creds.email);
  await page.fill('input[name="Input.Password"]', creds.password);
  await page.click('button[type="submit"]');
  // Wait for any post-login redirect, but don't fail if URL pattern differs across apps.
  await page.waitForLoadState('networkidle').catch(() => {});
  await page.waitForTimeout(1500);
}

async function logout(page, baseUrl) {
  const form = await page.$('form[action*="Logout"]');
  if (form) {
    const btn = await form.$('button');
    if (btn) { await btn.click(); return; }
  }
  await page.goto(`${baseUrl}/Account/Logout`).catch(() => {});
}

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

async function captureScreenshots(cfg) {
  if (cfg.screenshots.length === 0) {
    console.log('=== Screenshots: skipped (none configured) ===\n');
    return;
  }
  console.log(`=== Capturing ${cfg.screenshots.length} screenshots ===`);
  fs.mkdirSync(SCREENSHOTS_DIR, { recursive: true });

  const browser = await chromium.launch({ headless: true });
  const ctx = await browser.newContext({ viewport: cfg.viewport });
  const page = await ctx.newPage();

  let currentAuth = null;
  for (const shot of cfg.screenshots) {
    if (shot.auth !== currentAuth) {
      if (shot.auth) {
        await login(page, cfg.baseUrl, resolveCreds(cfg, shot.auth));
      } else if (currentAuth) {
        await logout(page, cfg.baseUrl);
      }
      currentAuth = shot.auth || null;
    }

    await page.goto(`${cfg.baseUrl}${shot.path}`);
    await page.waitForLoadState('networkidle').catch(() => {});
    if (shot.waitMs) await page.waitForTimeout(shot.waitMs);

    const file = `${SCREENSHOTS_DIR}/${shot.name}.png`;
    await page.screenshot({ path: file, fullPage: true });
    console.log(`  ✓ ${path.relative(REPO_ROOT, file)}`);
  }

  await browser.close();
  console.log('Screenshots complete.\n');
}

async function recordDemo(cfg) {
  if (!cfg.demo || !Array.isArray(cfg.demo.steps) || cfg.demo.steps.length === 0) {
    console.log('=== Demo video: skipped (no demo.steps configured) ===\n');
    return;
  }
  console.log(`=== Recording demo (${cfg.demo.steps.length} steps) ===`);
  fs.mkdirSync(DEMO_DIR, { recursive: true });
  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'demo-'));

  const browser = await chromium.launch({ headless: true });
  const ctx = await browser.newContext({
    viewport: cfg.viewport,
    recordVideo: { dir: tmpDir, size: cfg.viewport },
  });
  const page = await ctx.newPage();

  // Title card
  await page.goto(cfg.baseUrl);
  await page.waitForLoadState('networkidle').catch(() => {});
  if (cfg.demo.title) await showBanner(page, cfg.demo.title, 3000);

  for (const step of cfg.demo.steps) {
    if (step.action === 'login') {
      if (step.banner) await showBanner(page, step.banner, 1500);
      await login(page, cfg.baseUrl, resolveCreds(cfg, step.auth));
    } else if (step.action === 'logout') {
      if (step.banner) await showBanner(page, step.banner, 1000);
      await logout(page, cfg.baseUrl);
    } else {
      if (step.path) {
        if (step.banner) await showBanner(page, step.banner, 1000);
        await page.goto(`${cfg.baseUrl}${step.path}`);
        await page.waitForLoadState('networkidle').catch(() => {});
      } else if (step.banner) {
        await showBanner(page, step.banner, step.waitMs || 2000);
        continue;
      }
    }
    if (step.waitMs) await page.waitForTimeout(step.waitMs);
  }

  // Closing card
  if (cfg.demo.closing) {
    await page.goto(cfg.baseUrl).catch(() => {});
    await page.waitForLoadState('networkidle').catch(() => {});
    await showBanner(page, cfg.demo.closing, 3000);
  }

  await page.close();
  await ctx.close();
  await browser.close();

  await new Promise(r => setTimeout(r, 1500));
  const files = fs.readdirSync(tmpDir);
  const video = files.find(f => f.endsWith('.webm'));
  if (!video) {
    console.error('  ✗ No video file found in', tmpDir, '— files:', files);
    process.exit(1);
  }
  const dest = `${DEMO_DIR}/${cfg.demo.outputFile || 'demo.webm'}`;
  fs.copyFileSync(path.join(tmpDir, video), dest);
  fs.rmSync(tmpDir, { recursive: true, force: true });
  console.log(`  ✓ ${path.relative(REPO_ROOT, dest)}`);
  console.log('Demo recording complete.\n');
}

async function waitForApp(baseUrl) {
  const http = require('http');
  await new Promise((resolve, reject) => {
    http.get(baseUrl, (res) => { res.resume(); resolve(); })
        .on('error', () => reject(new Error(`App not running at ${baseUrl}. Start it first (try scripts/demo.sh).`)));
  });
}

async function main() {
  const cfg = loadConfig();
  console.log(`Config: ${path.relative(REPO_ROOT, CONFIG_PATH)}`);
  console.log(`Target: ${cfg.baseUrl}\n`);
  await waitForApp(cfg.baseUrl);
  await captureScreenshots(cfg);
  await recordDemo(cfg);
  console.log('✅ All done. Screenshots: docs/screenshots/, demo: docs/demo/');
}

main().catch(e => { console.error('ERROR:', e.message); process.exit(1); });
