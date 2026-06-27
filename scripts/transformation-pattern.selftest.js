const assert = require('node:assert/strict');
const fs = require('node:fs');

const source = fs.readFileSync('Jellyfin.Plugin.SyncPlayShare/Services/StartupService.cs', 'utf8');
const patchSource = fs.readFileSync('Jellyfin.Plugin.SyncPlayShare/Helpers/TransformationPatches.cs', 'utf8');
const controllerSource = fs.readFileSync('Jellyfin.Plugin.SyncPlayShare/Controllers/SyncPlayShareController.cs', 'utf8');
const match = source.match(/RegisterTransformation\([\s\S]*?"([^"]+)",\s*"\/SyncPlayShare\/transform\/index-html"\)/);

assert.ok(match, 'IndexHtml transformation registration not found');

const pattern = new RegExp(match[1].replace(/\\\\/g, '\\'));

assert.equal(pattern.test('/index.html'), true);
assert.equal(pattern.test('index.html'), true);
assert.equal(pattern.test('/web/index.html'), true);
assert.equal(pattern.test('C:\\jellyfin\\web\\index.html'), true);
assert.equal(pattern.test('session-login-index-html.7df1620bd3afcef60eb7.chunk.js'), false);
assert.match(patchSource, /IsHtmlDocument\(contents\)/);
assert.match(patchSource, /src=\\"\/SyncPlayShare\/syncplay-share\.js\\"/);
assert.match(patchSource, /SyncPlayChunk/);
assert.match(controllerSource, /transform\/index-html/);
assert.match(controllerSource, /transform\/syncplay-chunk/);
assert.match(source, /transformationEndpoint/);

console.log('transformation pattern self-check passed');
