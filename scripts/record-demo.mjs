#!/usr/bin/env node
/**
 * Demo video recorder for Copilot Blazor Template.
 * 
 * Usage:
 *   1. Start the app: dotnet run --project src/CopilotBlazorTemplate.Web --urls http://localhost:5177
 *   2. Run: node scripts/record-demo.mjs
 * 
 * Prerequisites: npm install playwright (in project or globally)
 */

const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');
const os = require('os');

const BASE = process.env.APP_URL || 'http://localhost:5177';
const DEMO_DIR = path.join(__dirname, '..', 'docs', 'demo');
const TMP_DIR = fs.mkdtempSync(path.join(os.tmpdir(), 'demo-video-'));

async function showBanner(page, text, duration = 2000) {
  await page.evaluate((t) => {
    const banner = document.createElement('div');
    banner.id = 'demo-banner';
    banner.textContent = t;
    banner.style.cssText = 'position:fixed;top:0;left:0;right:0;padding:12px;background:rgba(0,0,0,0.85);color:white;text-align:center;font-size:18px;font-weight:bold;z-index:99999;font-family:system-ui';
    document.body.appendChild(banner);
  }, text);
  await page.waitForTimeout(duration);
  await page.evaluate(() => {
    const b = document.getElementById('demo-banner');
    if (b) b.remove();
  });
}

async function main() {
  console.log(`Recording demo against ${BASE}...`);
  
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1280, height: 720 },
    recordVideo: { dir: TMP_DIR, size: { width: 1280, height: 720 } }
  });
  const page = await context.newPage();

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

  // Login as admin
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

  // Back to landing
  await page.goto(BASE);
  await page.waitForLoadState('networkidle');
  await showBanner(page, '✅ Built with GitHub Copilot', 3000);

  // Save video
  await page.close();
  await context.close();
  await browser.close();

  // Copy video to docs/demo
  await new Promise(r => setTimeout(r, 1000));
  const files = fs.readdirSync(TMP_DIR);
  const videoFile = files.find(f => f.endsWith('.webm'));
  if (videoFile) {
    fs.mkdirSync(DEMO_DIR, { recursive: true });
    const dest = path.join(DEMO_DIR, 'copilot-blazor-template-demo.webm');
    fs.copyFileSync(path.join(TMP_DIR, videoFile), dest);
    console.log(`✓ Demo video saved: ${dest}`);
  } else {
    console.error('No video file found in', TMP_DIR);
    process.exit(1);
  }

  // Cleanup
  fs.rmSync(TMP_DIR, { recursive: true, force: true });
}

main().catch(e => { console.error(e); process.exit(1); });
