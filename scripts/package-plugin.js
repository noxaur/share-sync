const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');
const { execFileSync } = require('node:child_process');

const root = path.resolve(__dirname, '..');
const buildYamlPath = path.join(root, 'build.yaml');
const propsPath = path.join(root, 'Directory.Build.props');
let buildYaml = fs.readFileSync(buildYamlPath, 'utf8');
const manifestPath = path.join(root, 'manifest.json');
const distDir = path.join(root, 'dist');
const publishDir = path.join(root, 'artifacts', 'plugin');

const value = (key) => {
  const match = buildYaml.match(new RegExp(`^${key}:\\s*"?([^"\\n]+)"?`, 'm'));
  if (!match) throw new Error(`Missing ${key} in build.yaml`);
  if (match[1].trim() === '>') {
    const block = buildYaml.match(new RegExp(`^${key}:\\s*>\\n((?:  .+\\n?)+)`, 'm'));
    if (!block) throw new Error(`Missing ${key} block in build.yaml`);
    return block[1].split('\n').map((line) => line.trim()).filter(Boolean).join(' ');
  }
  return match[1].trim();
};

const bumpRevision = (current) => {
  const parts = current.split('.').map(Number);
  if (parts.length !== 4 || parts.some((part) => !Number.isInteger(part))) throw new Error(`Bad version ${current}`);
  parts[3] += 1;
  return parts.join('.');
};
const writeVersion = (next) => {
  buildYaml = buildYaml.replace(/^version:\s*"?[^"\n]+"?/m, `version: "${next}"`);
  fs.writeFileSync(buildYamlPath, buildYaml);
  const props = fs.readFileSync(propsPath, 'utf8').replace(
    /<(Version|AssemblyVersion|FileVersion)>[^<]+<\/\1>/g,
    (_, name) => `<${name}>${next}</${name}>`,
  );
  fs.writeFileSync(propsPath, props);
};

if (process.argv.includes('--bump-revision')) writeVersion(bumpRevision(value('version')));

const version = value('version');
const targetAbi = value('targetAbi');
const zipAbi = targetAbi.replace(/\.0$/, '');
const zipName = `Jellyfin.Plugin.SyncPlayShare_${zipAbi}_${version}.zip`;
const zipPath = path.join(distDir, zipName);

fs.rmSync(publishDir, { recursive: true, force: true });
fs.mkdirSync(publishDir, { recursive: true });
fs.mkdirSync(distDir, { recursive: true });

execFileSync('dotnet', [
  'publish',
  path.join(root, 'Jellyfin.Plugin.SyncPlayShare/Jellyfin.Plugin.SyncPlayShare.csproj'),
  '--configuration',
  'Release',
  '--output',
  publishDir,
  '/property:GenerateFullPaths=true',
  '/consoleloggerparameters:NoSummary',
], { stdio: 'inherit' });

for (const file of fs.readdirSync(distDir)) {
  if (file.endsWith('.zip')) fs.rmSync(path.join(distDir, file));
}

const crcTable = Array.from({ length: 256 }, (_, index) => {
  let crc = index;
  for (let bit = 0; bit < 8; bit += 1) crc = crc & 1 ? 0xEDB88320 ^ (crc >>> 1) : crc >>> 1;
  return crc >>> 0;
});
const crc32 = (buffer) => {
  let crc = 0xFFFFFFFF;
  for (const byte of buffer) crc = crcTable[(crc ^ byte) & 0xFF] ^ (crc >>> 8);
  return (crc ^ 0xFFFFFFFF) >>> 0;
};
const u16 = (value) => {
  const buffer = Buffer.alloc(2);
  buffer.writeUInt16LE(value);
  return buffer;
};
const u32 = (value) => {
  const buffer = Buffer.alloc(4);
  buffer.writeUInt32LE(value);
  return buffer;
};
const dosDate = ((2026 - 1980) << 9) | (1 << 5) | 1;
const files = fs.readdirSync(publishDir).sort().map((name) => {
  const raw = fs.readFileSync(path.join(publishDir, name));
  return { name, raw, compressed: zlib.deflateRawSync(raw, { level: 9 }), crc: crc32(raw) };
});
const chunks = [];
const central = [];
let offset = 0;
for (const file of files) {
  const name = Buffer.from(file.name);
  const local = Buffer.concat([
    u32(0x04034B50), u16(20), u16(0), u16(8), u16(0), u16(dosDate),
    u32(file.crc), u32(file.compressed.length), u32(file.raw.length), u16(name.length), u16(0), name,
  ]);
  chunks.push(local, file.compressed);
  central.push(Buffer.concat([
    u32(0x02014B50), u16(20), u16(20), u16(0), u16(8), u16(0), u16(dosDate),
    u32(file.crc), u32(file.compressed.length), u32(file.raw.length), u16(name.length), u16(0), u16(0),
    u16(0), u16(0), u32(0), u32(offset), name,
  ]));
  offset += local.length + file.compressed.length;
}
const centralSize = central.reduce((size, chunk) => size + chunk.length, 0);
fs.writeFileSync(zipPath, Buffer.concat([
  ...chunks,
  ...central,
  u32(0x06054B50), u16(0), u16(0), u16(files.length), u16(files.length), u32(centralSize), u32(offset), u16(0),
]));

const checksum = crypto.createHash('md5').update(fs.readFileSync(zipPath)).digest('hex').toUpperCase();
const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
const plugin = manifest[0];
const existing = plugin.versions.find((item) => item.version === version) || plugin.versions[0] || {};

plugin.versions = [{
  ...existing,
  version,
  changelog: `${value('changelog').replace(/\s+/g, ' ').trim()}.`,
  targetAbi,
  sourceUrl: `https://raw.githubusercontent.com/noxaur/share-sync/main/dist/${zipName}`,
  checksum,
  timestamp: existing.checksum === checksum ? existing.timestamp : new Date().toISOString().replace(/\.\d{3}Z$/, ''),
  dependencies: existing.dependencies || ['5e87cc92-571a-4d8d-8d98-d2d4147f9f90'],
}];

fs.writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
console.log(`packaged ${zipName}`);
