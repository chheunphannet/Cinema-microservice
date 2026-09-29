const puppeteer = require('puppeteer');

(async () => {
  const url = process.argv[2] || 'file://' + __dirname + '/index.html';
  const outputPath = process.argv[3] || 'screenshot.png';

  console.log(`Taking screenshot of ${url}...`);
  const browser = await puppeteer.launch();
  const page = await browser.newPage();
  
  // Set a standard desktop viewport
  await page.setViewport({ width: 1280, height: 800 });
  
  await page.goto(url, { waitUntil: 'networkidle0' });
  await page.screenshot({ path: outputPath, fullPage: true });
  
  console.log(`Screenshot saved to ${outputPath}`);
  await browser.close();
})();
