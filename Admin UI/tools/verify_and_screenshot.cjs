const puppeteer = require('puppeteer');
const { spawn } = require('child_process');

async function main() {
  console.log('--- Step 1: Obtain admin JWT token ---');
  let token = null;
  try {
    const res = await fetch('http://localhost:8080/api/v1/identity/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: 'admin', pinOrPassword: 'admin123' })
    });
    const data = await res.json();
    token = data.token;
    console.log('Successfully acquired admin token.');
  } catch (err) {
    console.error('Warning: could not get token from identity service:', err.message);
  }

  console.log('--- Step 2: Launch preview server on port 4321 ---');
  const server = spawn('npx', ['astro', 'preview', '--port', '4321'], {
    shell: true,
    stdio: 'pipe'
  });

  server.stdout.on('data', (d) => console.log('[Server stdout]:', d.toString().trim()));
  server.stderr.on('data', (d) => console.log('[Server stderr]:', d.toString().trim()));

  // Wait for server to start
  console.log('Waiting for server readiness...');
  let ready = false;
  for (let i = 0; i < 30; i++) {
    try {
      const ping = await fetch('http://localhost:4321/movies');
      if (ping.ok || ping.status === 200) {
        ready = true;
        break;
      }
    } catch (e) {}
    await new Promise(r => setTimeout(r, 500));
  }

  if (!ready) {
    console.error('Server failed to start in 15 seconds.');
    server.kill();
    process.exit(1);
  }
  console.log('Preview server ready at http://localhost:4321');

  try {
    console.log('--- Step 3: Launch Puppeteer ---');
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

    console.log('Navigating to http://localhost:4321/movies...');
    await page.goto('http://localhost:4321/movies', { waitUntil: 'domcontentloaded' });
    await new Promise(r => setTimeout(r, 2500));

    // Capture main catalog screenshot
    await page.screenshot({ path: 'movies_catalog.png' });
    console.log('Saved main screenshot: movies_catalog.png');

    // Test Add Movie Modal
    console.log('Testing Add Movie Modal...');
    const addBtn = await page.$('button[class*="variant-primary"]');
    if (addBtn) {
      await addBtn.click();
      await new Promise(r => setTimeout(r, 800));
      await page.screenshot({ path: 'add_movie_modal.png' });
      console.log('Saved modal screenshot: add_movie_modal.png');

      // Click Cancel
      const cancelBtn = await page.evaluateHandle(() => {
        const btns = Array.from(document.querySelectorAll('button'));
        return btns.find(b => b.textContent.includes('Cancel'));
      });
      if (cancelBtn) await cancelBtn.click();
      await new Promise(r => setTimeout(r, 500));
    }

    // Test View Details
    console.log('Testing selection and View Details...');
    const firstRadio = await page.$('input[type="radio"]');
    if (firstRadio) {
      await firstRadio.click();
      await new Promise(r => setTimeout(r, 500));

      const detailsBtn = await page.evaluateHandle(() => {
        const btns = Array.from(document.querySelectorAll('button'));
        return btns.find(b => b.textContent.includes('View Details'));
      });
      if (detailsBtn) {
        await detailsBtn.click();
        await new Promise(r => setTimeout(r, 800));
        await page.screenshot({ path: 'movie_details_modal.png' });
        console.log('Saved details screenshot: movie_details_modal.png');
      }
    }

    await browser.close();
    console.log('Puppeteer testing complete.');
  } finally {
    console.log('--- Step 4: Cleanup & terminate server process ---');
    // On Windows, kill process tree
    if (process.platform === 'win32' && server.pid) {
      const { execSync } = require('child_process');
      try {
        execSync(`taskkill /F /T /PID ${server.pid}`);
      } catch (e) {}
    } else {
      server.kill();
    }
    console.log('Preview server terminated cleanly.');
  }
}

main().catch(err => {
  console.error('Error during verification:', err);
  process.exit(1);
});
