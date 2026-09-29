const puppeteer = require('puppeteer');

(async () => {
  const browser = await puppeteer.launch({ headless: true, args: ['--no-sandbox'] });
  const page = await browser.newPage();
  await page.setViewport({ width: 1440, height: 900 });

  // 1. Showtimes table page
  await page.goto('http://localhost:4321/showtimes', { waitUntil: 'networkidle2', timeout: 15000 });
  await new Promise(r => setTimeout(r, 2000));
  await page.screenshot({ path: 'screenshots/showtimes_table.png', fullPage: false });
  console.log('✅ Screenshot: showtimes_table.png');

  // 2. Open Add Showtime modal by clicking the "Add Showtime" button
  const addBtn = await page.evaluateHandle(() => {
    const buttons = [...document.querySelectorAll('button')];
    return buttons.find(b => b.textContent.includes('Add Showtime'));
  });
  if (addBtn) {
    await addBtn.click();
    await new Promise(r => setTimeout(r, 1000));
    await page.screenshot({ path: 'screenshots/showtimes_add_modal.png', fullPage: false });
    console.log('✅ Screenshot: showtimes_add_modal.png');

    // Close the modal
    const cancelBtn = await page.evaluateHandle(() => {
      const buttons = [...document.querySelectorAll('button')];
      return buttons.find(b => b.textContent.trim() === 'Cancel');
    });
    if (cancelBtn) await cancelBtn.click();
    await new Promise(r => setTimeout(r, 500));
  } else {
    console.log('⚠️ Add Showtime button not found');
  }

  // 3. Open Bulk CSV Import modal
  const csvBtn = await page.evaluateHandle(() => {
    const buttons = [...document.querySelectorAll('button')];
    return buttons.find(b => b.textContent.includes('Bulk CSV Import'));
  });
  if (csvBtn) {
    await csvBtn.click();
    await new Promise(r => setTimeout(r, 1000));
    await page.screenshot({ path: 'screenshots/showtimes_csv_modal.png', fullPage: false });
    console.log('✅ Screenshot: showtimes_csv_modal.png');
  } else {
    console.log('⚠️ Bulk CSV Import button not found');
  }

  await browser.close();
  console.log('Done! All screenshots saved to screenshots/');
})();
