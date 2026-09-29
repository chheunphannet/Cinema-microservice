const puppeteer = require('puppeteer');
const path = require('path');

(async () => {
  const browser = await puppeteer.launch({ headless: 'new', args: ['--no-sandbox'] });
  const page = await browser.newPage();
  await page.setViewport({ width: 1440, height: 900 });

  const screenshotsDir = 'C:\\Users\\chheu\\.gemini\\antigravity-cli\\brain\\82b0cb37-d85c-4171-aaf2-04aa06dfb8fe\\screenshots';
  const fakeToken = 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ0ZXN0IiwiZW1haWwiOiJhZG1pbkBjaW5lbWEuY29tIiwicm9sZSI6InN1cGVyX2FkbWluIiwibmFtZSI6IkFkbWluIFVzZXIiLCJpYXQiOjE2MDAwMDAwMDAsImV4cCI6OTk5OTk5OTk5OX0.fake';

  // Navigate to a real page first so localStorage works
  await page.goto('http://localhost:4321/', { waitUntil: 'domcontentloaded', timeout: 15000 }).catch(() => {});
  await page.evaluate((t) => { try { localStorage.setItem('token', t); } catch(e) {} }, fakeToken);

  const pages = [
    { url: 'http://localhost:4321/pricing', name: 'pricing_new' },
  ];

  for (const p of pages) {
    await page.goto(p.url, { waitUntil: 'networkidle0', timeout: 15000 }).catch(() => page.goto(p.url));
    await new Promise(r => setTimeout(r, 2000));
    await page.screenshot({ path: path.join(screenshotsDir, `${p.name}.png`), fullPage: false });
    console.log(`Screenshot saved: ${p.name}`);
  }

  await browser.close();
  console.log('Done');
})();
