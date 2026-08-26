const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const root = path.resolve(__dirname, '..');
const sourceDirectory = path.join(root, 'src', 'RhinoMM.Plugin', 'UI', 'Icons', 'Source');
const outputDirectory = path.join(root, 'src', 'RhinoMM.Plugin', 'UI', 'Icons', 'Generated');
const names = ['read', 'place', 'apply', 'refresh', 'rhino', 'step', 'statistics', 'inspector', 'more'];
const variants = [
  { suffix: '', color: '#34495E', sizes: [16, 24, 32, 36, 48] },
  { suffix: '-dark', color: '#D7DEE8', sizes: [24, 36, 48] },
  { suffix: '-inverse', color: '#FFFFFF', sizes: [24, 36, 48] }
];
const panelSizes = [16, 24, 32, 48];

function writePngIco(outputPath, frames) {
  const headerSize = 6;
  const entrySize = 16;
  const header = Buffer.alloc(headerSize + entrySize * frames.length);
  header.writeUInt16LE(0, 0);
  header.writeUInt16LE(1, 2);
  header.writeUInt16LE(frames.length, 4);
  let offset = header.length;
  frames.forEach((frame, index) => {
    const signature = frame.png.subarray(0, 8).toString('hex');
    if (signature !== '89504e470d0a1a0a'
        || frame.png.readUInt32BE(16) !== frame.size
        || frame.png.readUInt32BE(20) !== frame.size)
      throw new Error(`Invalid ${frame.size}px panel PNG frame.`);
    const entry = headerSize + entrySize * index;
    header.writeUInt8(frame.size === 256 ? 0 : frame.size, entry);
    header.writeUInt8(frame.size === 256 ? 0 : frame.size, entry + 1);
    header.writeUInt8(0, entry + 2);
    header.writeUInt8(0, entry + 3);
    header.writeUInt16LE(1, entry + 4);
    header.writeUInt16LE(32, entry + 6);
    header.writeUInt32LE(frame.png.length, entry + 8);
    header.writeUInt32LE(offset, entry + 12);
    offset += frame.png.length;
  });
  fs.writeFileSync(outputPath, Buffer.concat([header, ...frames.map(frame => frame.png)]));
}

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

    const panelSource = fs.readFileSync(path.join(sourceDirectory, 'panel.svg'), 'utf8');
    const panelFrames = [];
    for (const size of panelSizes) {
      await page.setViewportSize({ width: size, height: size });
      await page.setContent(`<style>html,body{margin:0;background:transparent;width:100%;height:100%}svg{display:block;width:100%;height:100%}</style>${panelSource}`);
      const outputPath = path.join(outputDirectory, `panel-${size}.png`);
      await page.screenshot({ path: outputPath, omitBackground: true });
      panelFrames.push({ size, png: fs.readFileSync(outputPath) });
    }
    writePngIco(path.join(outputDirectory, 'panel.ico'), panelFrames);
  } finally {
    await browser.close();
  }
}

main().catch(error => {
  console.error(error);
  process.exitCode = 1;
});
