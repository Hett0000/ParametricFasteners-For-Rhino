const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const root = path.resolve(__dirname, '..');
const sourceDirectory = path.join(root, 'src', 'RhinoMM.Plugin', 'UI', 'Icons', 'Source');
const outputDirectory = path.join(root, 'src', 'RhinoMM.Plugin', 'UI', 'Icons', 'Generated');
const names = ['read', 'place', 'apply', 'refresh', 'rhino', 'step', 'statistics', 'more'];
const variants = [
  { suffix: '', color: '#34495E', sizes: [16, 24, 32, 36, 48] },
  { suffix: '-dark', color: '#D7DEE8', sizes: [24, 36, 48] },
  { suffix: '-inverse', color: '#FFFFFF', sizes: [24, 36, 48] }
];

async function main() {
  fs.mkdirSync(outputDirectory, { recursive: true });
  const browserCandidates = [
    process.env.PROGRAMFILES && path.join(process.env.PROGRAMFILES, 'Microsoft', 'Edge', 'Application', 'msedge.exe'),
    process.env['PROGRAMFILES(X86)'] && path.join(process.env['PROGRAMFILES(X86)'], 'Microsoft', 'Edge', 'Application', 'msedge.exe')
  ].filter(Boolean);
  const executablePath = browserCandidates.find(candidate => fs.existsSync(candidate));
  if (!executablePath)
    throw new Error('Microsoft Edge is required to regenerate icon PNG assets.');
  const browser = await chromium.launch({ executablePath, headless: true });
  try {
    const page = await browser.newPage();
    for (const name of names) {
      const source = fs.readFileSync(path.join(sourceDirectory, `${name}.svg`), 'utf8');
      for (const variant of variants) {
        for (const size of variant.sizes) {
          const strokeWidth = size === 16 || size === 32 ? '1.5' : '2';
          const svg = source
            .replace('currentColor', variant.color)
            .replace('stroke-width="2"', `stroke-width="${strokeWidth}"`);
          await page.setViewportSize({ width: size, height: size });
          await page.setContent(`<style>html,body{margin:0;background:transparent;width:100%;height:100%}svg{display:block;width:100%;height:100%}</style>${svg}`);
          await page.screenshot({
            path: path.join(outputDirectory, `${name}${variant.suffix}-${size}.png`),
            omitBackground: true
          });
        }
      }
    }
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
