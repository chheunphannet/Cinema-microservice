const puppeteer = require('puppeteer');

(async () => {
  let token = null;
  try {
    const res = await fetch("http://localhost:8080/api/v1/identity/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username: "admin", pinOrPassword: "admin123" })
    });
    const data = await res.json();
    token = data.token;
  } catch (e) {}

  const browser = await puppeteer.launch({
    headless: true,
    defaultViewport: { width: 1440, height: 900 }
  });
  const page = await browser.newPage();
  if (token) {
    await page.evaluateOnNewDocument((t) => {
      localStorage.setItem('token', t);
    }, token);
  }

  await page.goto('http://localhost:4321/movies', { waitUntil: 'networkidle2' });
  await new Promise(r => setTimeout(r, 2000));

  await page.screenshot({ path: 'movies_catalog.png', fullPage: true });
  console.log('Saved screenshot to movies_catalog.png');
  await browser.close();
})();
