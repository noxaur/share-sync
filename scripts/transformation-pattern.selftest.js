const assert = require('node:assert/strict');
const fs = require('node:fs');

const source = fs.readFileSync('Jellyfin.Plugin.SyncPlayShare/Services/StartupService.cs', 'utf8');
const match = source.match(/RegisterTransformation\([\s\S]*?"([^"]+)",\s*nameof\(TransformationPatches\.IndexHtml\)\)/);

assert.ok(match, 'IndexHtml transformation registration not found');

const pattern = new RegExp(match[1].replace(/\\\\/g, '\\'));

assert.equal(pattern.test('/index.html'), true);
assert.equal(pattern.test('index.html'), true);
assert.equal(pattern.test('/session/login/index.html'), false);

console.log('transformation pattern self-check passed');
