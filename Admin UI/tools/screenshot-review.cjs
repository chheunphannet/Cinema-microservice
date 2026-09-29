const puppeteer = require('puppeteer');
const path = require('path');

const TOKEN = process.env.JWT_TOKEN;
const BASE = 'http://localhost:4321';
const OUT_DIR = process.env.OUT_DIR || path.join(__dirname, '..', 'screenshots');

const pages = [
  { name: 'login', url: '/login', skipAuth: true },
  { name: 'dashboard', url: '/' },
  { name: 'movies', url: '/movies' },
  { name: 'showtimes', url: '/showtimes' },
  { name: 'pricing', url: '/pricing' },
  { name: 'inventory', url: '/inventory' },
  { name: 'reservations', url: '/reservations' },
  { name: 'users', url: '/users' },
];

(async () => {
  const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox'],
  });

  for (const pg of pages) {
    console.log(`Screenshotting: ${pg.name} (${pg.url})`);
    const page = await browser.newPage();
    await page.setViewport({ width: 1440, height: 900 });

    if (!pg.skipAuth && TOKEN) {
      // Set token in localStorage before navigating
      await page.goto(BASE + '/login', { waitUntil: 'domcontentloaded' });
      await page.evaluate((t) => { localStorage.setItem('token', t); }, TOKEN);
    }

    await page.goto(BASE + pg.url, { waitUntil: 'networkidle2', timeout: 15000 });
    await new Promise(r => setTimeout(r, 2000)); // Wait for React hydration

    const filepath = path.join(OUT_DIR, `${pg.name}.png`);
    await page.screenshot({ path: filepath, fullPage: false });
    console.log(`  Saved: ${filepath}`);
    await page.close();
  }

  await browser.close();
  console.log('Done!');
})();
