import fs from "node:fs/promises";
import path from "node:path";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const { chromium } = require("C:/Users/eaespinoza/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright");

const baseURL = "http://127.0.0.1:5277";
const edgePath = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
const outputDir = path.resolve("capturas");

const admin = { username: "batadmin", password: "Prueba123!" };

async function login(page) {
  await page.goto(`${baseURL}/App/Login`, { waitUntil: "domcontentloaded" });
  await page.waitForSelector("#loginForm");
  await page.fill("#username", admin.username);
  await page.fill("#password", admin.password);
  await page.click("#loginButton");
  await page.waitForURL("**/App/Dashboard", { timeout: 25000 });
}

async function capture(page, name, url, waitSelector) {
  await page.goto(`${baseURL}${url}`, { waitUntil: "domcontentloaded" });
  if (waitSelector) {
    try {
      await page.waitForSelector(waitSelector, { timeout: 12000 });
    } catch {
      // Continue even when module-specific selector changes.
    }
  }
  await page.waitForTimeout(600);
  await page.screenshot({
    path: path.join(outputDir, name),
    fullPage: true,
  });
}

const browser = await chromium.launch({
  headless: true,
  executablePath: edgePath,
});

try {
  await fs.mkdir(outputDir, { recursive: true });
  const context = await browser.newContext({ viewport: { width: 1536, height: 960 } });
  const page = await context.newPage();

  await login(page);

  await capture(page, "01-dashboard.png", "/App/Dashboard", "#menuGrid");
  await capture(page, "02-configuracion.png", "/App/Configuracion", "#securitySummary");
  await capture(page, "03-rrhh.png", "/App/Rrhh", "#mainNav");
  await capture(page, "04-portal.png", "/App/MiPortal", "#portalContent");
  await capture(page, "05-nomina.png", "/App/Nomina", "#workspaceNav");
  await capture(page, "06-clientes.png", "/App/Clientes", "#clientesApp");
  await capture(page, "07-solicitudes-credito.png", "/App/SolicitudesCredito", "#solicitudesCreditoApp");
  await capture(page, "08-cartera.png", "/App/Cartera", "#carteraApp");
  await capture(page, "09-caja.png", "/App/Caja", "#cajaApp");
  await capture(page, "10-contabilidad.png", "/App/Contabilidad", "#contabilidadApp");

  await page.goto(`${baseURL}/App/Login`, { waitUntil: "domcontentloaded" });
  await page.waitForSelector("#loginForm");
  await page.screenshot({
    path: path.join(outputDir, "00-login.png"),
    fullPage: true,
  });

  console.log(`Capturas guardadas en: ${outputDir}`);
} finally {
  await browser.close();
}
