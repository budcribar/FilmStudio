#!/usr/bin/env node
/**
 * Regression smoke: book → cast → skip voice → estimate → confirm → free sample.
 * Usage: node scripts/smoke-wizard.mjs [baseUrl]
 */
import { chromium } from "playwright";

const base = process.argv[2] || "http://127.0.0.1:8080";

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
const errors = [];
page.on("pageerror", (e) => errors.push(e.message));

try {
  await page.goto(base, { waitUntil: "networkidle" });
  await page.evaluate(() => localStorage.clear());
  await page.goto(`${base}/studio`, { waitUntil: "networkidle" });

  // Alice is 2nd classic card (index 1)
  await page.getByRole("button", { name: /Choose this book/i }).nth(1).click();
  await page.waitForURL(/\/studio\/p_/);

  await page.locator('input[placeholder*="child"]').first().fill("Milo");
  await page.getByRole("button", { name: /Continue to voice/i }).click();
  await page.getByRole("button", { name: /Skip — stock voices/i }).click();
  await page.getByRole("button", { name: /Continue to confirm/i }).click();
  await page.getByRole("button", { name: /Free: generate 1 sample scene/i }).click();
  await page.waitForFunction(
    () => /Free sample|Play|Sample/i.test(document.body.innerText),
    null,
    { timeout: 25000 },
  );

  const text = await page.evaluate(() => document.body.innerText);
  const ok =
    text.includes("Milo") &&
    (/sample/i.test(text) || /Play/i.test(text)) &&
    errors.length === 0;

  console.log(JSON.stringify({ ok, errors, hasMilo: text.includes("Milo") }, null, 2));
  await browser.close();
  process.exit(ok ? 0 : 1);
} catch (e) {
  console.error(e);
  await browser.close();
  process.exit(1);
}
