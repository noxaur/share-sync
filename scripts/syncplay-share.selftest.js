const assert = require('node:assert/strict');
const share = require('../Jellyfin.Plugin.SyncPlayShare/Assets/syncplay-share.js');

const url = share.buildShareUrl({
  origin: 'https://media.example.test',
  pathname: '/web/index.html',
}, '6a73d2b1-02fb-4eb8-87f2-4ff8db8f2489');

assert.equal(
  url,
  'https://media.example.test/web/index.html?syncplayShare=6a73d2b1-02fb-4eb8-87f2-4ff8db8f2489',
);

assert.equal(
  share.readShareId(url),
  '6a73d2b1-02fb-4eb8-87f2-4ff8db8f2489',
);

assert.equal(share.shouldLog('Error'), true);
assert.equal(share.shouldLog('Debug'), false);

console.log('syncplay-share self-check passed');
