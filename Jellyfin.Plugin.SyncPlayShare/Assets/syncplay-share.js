(function (root) {
    'use strict';

    var config = Object.assign({
        enabled: true,
        logLevel: 'Info',
        clientConsoleLogging: false,
        copyToastEnabled: true,
        shareButtonLabel: 'Share'
    }, (typeof module !== 'undefined' && module.exports) ? {} : __SYNCPLAY_SHARE_CONFIG__);

    if (root.document && root.SyncPlayShareLoaded) {
        return;
    }

    var prefix = '[SyncPlayShare]';
    // #region agent log
    function debugLog(hypothesisId, location, message, data) {
        fetch('http://127.0.0.1:7309/ingest/ea0abf19-45e6-4f64-82df-f49129b88600', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-Debug-Session-Id': '856118' },
            body: JSON.stringify({
                sessionId: '856118',
                hypothesisId: hypothesisId,
                location: location,
                message: message,
                data: data || {},
                timestamp: Date.now()
            })
        }).catch(function () {});
    }
    debugLog('H1', 'syncplay-share.js:boot', 'Script executing', {
        enabled: config.enabled,
        alreadyLoaded: !!root.SyncPlayShareLoaded,
        href: root.location && root.location.href
    });
    // #endregion
    var pendingKey = 'syncplayShare.pendingGroupId';
    var activeKey = 'syncplayShare.activeGroupId';
    var patchedClients = new WeakSet();
    var observer = null;
    var initialized = false;
    root.SyncPlayShareLoaded = true;

    function logRank(level) {
        return ['Error', 'Info', 'Debug', 'Verbose'].indexOf(level);
    }

    function shouldLog(level) {
        if (level === 'Error') return true;
        if (!config.clientConsoleLogging) return false;
        return logRank(config.logLevel) >= logRank(level);
    }

    function log(level, message, detail) {
        if (!shouldLog(level) || !root.console) return;
        var fn = level === 'Error' ? 'error' : level === 'Info' ? 'info' : 'debug';
        root.console[fn](prefix + ' ' + message, detail || '');
    }

    function buildShareUrl(locationLike, groupId) {
        var url = new URL(locationLike.origin + locationLike.pathname);
        url.searchParams.set('syncplayShare', groupId);
        return url.toString();
    }

    function readShareId(urlText) {
        var url = new URL(urlText);
        return url.searchParams.get('syncplayShare');
    }

    function stripShareParam() {
        var url = new URL(root.location.href);
        url.searchParams.delete('syncplayShare');
        root.history.replaceState(root.history.state, root.document.title, url.pathname + url.search + url.hash);
    }

    function rememberActiveGroup(groupId) {
        if (!groupId) return;
        root.sessionStorage.setItem(activeKey, groupId);
        log('Debug', 'Remembered active group.', groupId);
    }

    function clearActiveGroup() {
        root.sessionStorage.removeItem(activeKey);
        log('Debug', 'Cleared active group.');
    }

    function toast(message, error) {
        if (!config.copyToastEnabled && !error) return;

        var elem = root.document.createElement('div');
        elem.className = 'toast';
        elem.textContent = message;

        var container = root.document.querySelector('.toastContainer');
        if (!container) {
            container = root.document.createElement('div');
            container.className = 'toastContainer';
            root.document.body.appendChild(container);
        }

        container.appendChild(elem);
        root.setTimeout(function () {
            elem.classList.add('toastVisible');
        }, 30);
        root.setTimeout(function () {
            elem.classList.add('toastHide');
            root.setTimeout(function () {
                if (elem.parentNode) elem.parentNode.removeChild(elem);
            }, 300);
        }, 3300);
    }

    function parseResponseJson(response) {
        if (!response) return Promise.resolve(null);
        if (typeof response.json === 'function') {
            var jsonResponse = typeof response.clone === 'function' ? response.clone() : response;
            return jsonResponse.json().catch(function () {
                return null;
            });
        }

        return Promise.resolve(response);
    }

    function patchApiClient(apiClient) {
        if (!apiClient || patchedClients.has(apiClient)) return;
        patchedClients.add(apiClient);

        if (typeof apiClient.joinSyncPlayGroup === 'function') {
            var joinSyncPlayGroup = apiClient.joinSyncPlayGroup;
            apiClient.joinSyncPlayGroup = function (options) {
                if (options && options.GroupId) rememberActiveGroup(options.GroupId);
                return joinSyncPlayGroup.apply(this, arguments);
            };
        }

        if (typeof apiClient.createSyncPlayGroup === 'function') {
            var createSyncPlayGroup = apiClient.createSyncPlayGroup;
            apiClient.createSyncPlayGroup = function () {
                var result = createSyncPlayGroup.apply(this, arguments);
                Promise.resolve(result).then(parseResponseJson).then(function (group) {
                    if (group && group.GroupId) rememberActiveGroup(group.GroupId);
                }).catch(function (error) {
                    log('Error', 'Failed to read created SyncPlay group.', error);
                });
                return result;
            };
        }

        if (typeof apiClient.leaveSyncPlayGroup === 'function') {
            var leaveSyncPlayGroup = apiClient.leaveSyncPlayGroup;
            apiClient.leaveSyncPlayGroup = function () {
                clearActiveGroup();
                return leaveSyncPlayGroup.apply(this, arguments);
            };
        }
    }

    function getApiClient() {
        var apiClient = root.ApiClient;
        if (apiClient) patchApiClient(apiClient);
        return apiClient;
    }

    function isLoginRoute() {
        var text = (root.location.pathname + root.location.hash).toLowerCase();
        return text.indexOf('/login') !== -1 || text.indexOf('login.html') !== -1 || text.indexOf('session-login') !== -1;
    }

    function isAuthenticated(apiClient) {
        return !!(apiClient && typeof apiClient.accessToken === 'function' && apiClient.accessToken());
    }

    function listGroups(apiClient) {
        if (!apiClient || typeof apiClient.getSyncPlayGroups !== 'function') {
            return Promise.reject(new Error('ApiClient.getSyncPlayGroups unavailable'));
        }

        return apiClient.getSyncPlayGroups().then(function (response) {
            if (response && typeof response.json === 'function') {
                return response.json();
            }

            return response;
        });
    }

    function groupIdFromMenu(menu, apiClient) {
        var stored = root.sessionStorage.getItem(activeKey);
        if (stored) return Promise.resolve(stored);

        var title = menu.querySelector('.actionSheetTitle');
        var groupName = title ? title.textContent.trim() : '';
        if (!groupName) return Promise.resolve(null);

        return listGroups(apiClient).then(function (groups) {
            var matches = (groups || []).filter(function (group) {
                return group.GroupName === groupName;
            });

            if (matches.length === 1) {
                rememberActiveGroup(matches[0].GroupId);
                return matches[0].GroupId;
            }

            log('Debug', 'Could not infer unique group from menu title.', { groupName: groupName, matches: matches.length });
            return null;
        });
    }

    function copyText(text) {
        if (root.navigator.clipboard && root.navigator.clipboard.writeText) {
            return root.navigator.clipboard.writeText(text);
        }

        var input = root.document.createElement('textarea');
        input.value = text;
        input.setAttribute('readonly', 'readonly');
        input.style.position = 'fixed';
        input.style.left = '-9999px';
        root.document.body.appendChild(input);
        input.select();

        var ok = root.document.execCommand('copy');
        root.document.body.removeChild(input);
        return ok ? Promise.resolve() : Promise.reject(new Error('execCommand copy failed'));
    }

    function shareFromMenu(menu) {
        var apiClient = getApiClient();
        return groupIdFromMenu(menu, apiClient).then(function (groupId) {
            if (!groupId) {
                toast('No active SyncPlay group found.', true);
                throw new Error('No active SyncPlay group found');
            }

            var shareUrl = buildShareUrl(root.location, groupId);
            return copyText(shareUrl).then(function () {
                toast('SyncPlay share copied.');
                log('Info', 'Share copied.', groupId);
            });
        }).catch(function (error) {
            log('Error', 'Share failed.', error);
            toast('SyncPlay share failed.', true);
        });
    }

    function makeShareButton(settingsButton, menu) {
        var button = root.document.createElement('button');
        button.setAttribute('data-id', 'syncplay-share');
        button.setAttribute('type', 'button');
        button.setAttribute('is', 'emby-button');
        button.className = settingsButton.className + ' syncPlayShareButton';
        button.innerHTML = [
            '<span class="actionsheetMenuItemIcon listItemIcon listItemIcon-transparent material-icons share" aria-hidden="true"></span>',
            '<div class="listItemBody actionsheetListItemBody">',
            '<div class="listItemBodyText actionSheetItemText"></div>',
            '<div class="listItemBodyText secondary"></div>',
            '</div>'
        ].join('');

        button.querySelector('.actionSheetItemText').textContent = config.shareButtonLabel || 'Share';
        button.querySelector('.secondary').textContent = 'Copy SyncPlay share';

        button.addEventListener('click', function (event) {
            event.preventDefault();
            event.stopPropagation();
            shareFromMenu(menu);
        }, true);

        return button;
    }

    function isSettingsButton(button) {
        if (!button) return false;
        if (button.getAttribute('data-id') === 'settings') return true;

        var text = button.querySelector('.actionSheetItemText');
        return text && text.textContent && text.textContent.trim().toLowerCase() === 'settings';
    }

    function ensureShareButton(menu) {
        if (!menu || menu.querySelector('.syncPlayShareButton')) return;

        var settingsButton = menu.querySelector('[data-id="settings"]');
        if (!settingsButton) {
            settingsButton = Array.prototype.find.call(menu.querySelectorAll('.actionSheetMenuItem'), isSettingsButton);
        }

        if (!settingsButton || !settingsButton.parentNode) {
            log('Debug', 'SyncPlay menu found without settings button.', menu);
            // #region agent log
            debugLog('H3', 'syncplay-share.js:ensureShareButton', 'Settings button missing', {
                menuClass: menu.className,
                menuItemCount: menu.querySelectorAll('.actionSheetMenuItem').length,
                dataIds: Array.prototype.map.call(menu.querySelectorAll('[data-id]'), function (el) {
                    return el.getAttribute('data-id');
                })
            });
            // #endregion
            return;
        }

        settingsButton.parentNode.insertBefore(makeShareButton(settingsButton, menu), settingsButton);
        log('Debug', 'Share button inserted.');
        // #region agent log
        debugLog('H3', 'syncplay-share.js:ensureShareButton', 'Share button inserted', {
            menuClass: menu.className
        });
        // #endregion
    }

    function scanMenus() {
        var candidates = root.document.querySelectorAll('.syncPlayGroupMenu, .actionSheet');
        var syncPlayMenus = 0;
        candidates.forEach(function (menu) {
            var markers = menu.querySelectorAll('[data-id="leave-group"], [data-id="halt-playback"], [data-id="resume-playback"]');
            if (markers.length) {
                syncPlayMenus += 1;
                ensureShareButton(menu);
            }
        });
        // #region agent log
        if (candidates.length) {
            debugLog('H2', 'syncplay-share.js:scanMenus', 'Menu scan', {
                candidateCount: candidates.length,
                syncPlayMenuCount: syncPlayMenus,
                hasShareButton: !!root.document.querySelector('.syncPlayShareButton')
            });
        }
        // #endregion
    }

    function joinGroup(groupId) {
        var apiClient = getApiClient();
        if (!isAuthenticated(apiClient)) {
            throw new Error('ApiClient is not authenticated');
        }

        if (!apiClient || typeof apiClient.joinSyncPlayGroup !== 'function') {
            throw new Error('ApiClient.joinSyncPlayGroup unavailable');
        }

        return apiClient.joinSyncPlayGroup({ GroupId: groupId }).then(function () {
            rememberActiveGroup(groupId);
            toast('SyncPlay share joined.');
            log('Info', 'Joined shared SyncPlay group.', groupId);
        });
    }

    function tryPendingJoin() {
        var groupId = root.sessionStorage.getItem(pendingKey);
        if (!groupId) return;

        if (!isAuthenticated(getApiClient())) {
            log('Debug', 'Pending join waiting for authenticated ApiClient.');
            return;
        }

        try {
            Promise.resolve(joinGroup(groupId)).then(function () {
                root.sessionStorage.removeItem(pendingKey);
            }).catch(function (error) {
                root.sessionStorage.removeItem(pendingKey);
                log('Error', 'Pending SyncPlay join failed.', error);
                toast('SyncPlay share join failed.', true);
            });
        } catch (error) {
            log('Debug', 'Pending join waiting for ApiClient.', error);
        }
    }

    function captureShareParam() {
        var groupId = readShareId(root.location.href);
        if (!groupId) return;

        root.sessionStorage.setItem(pendingKey, groupId);
        stripShareParam();
        log('Debug', 'Captured pending share.', groupId);
    }

    function init() {
        if (!config.enabled || !root.document) return;

        captureShareParam();
        if (isLoginRoute()) {
            log('Debug', 'Login route detected; SyncPlay Share waiting.');
            return;
        }

        if (initialized) {
            tryPendingJoin();
            return;
        }

        initialized = true;
        patchApiClient(getApiClient());
        scanMenus();
        tryPendingJoin();

        observer = new MutationObserver(function () {
            patchApiClient(getApiClient());
            scanMenus();
            tryPendingJoin();
        });
        observer.observe(root.document.documentElement, { childList: true, subtree: true });
        log('Info', 'Initialized.');
        // #region agent log
        debugLog('H1', 'syncplay-share.js:init', 'Initialized', {
            authenticated: isAuthenticated(getApiClient()),
            hasShareButton: !!root.document.querySelector('.syncPlayShareButton')
        });
        // #endregion
    }

    root.SyncPlayShare = {
        buildShareUrl: buildShareUrl,
        readShareId: readShareId,
        shouldLog: shouldLog,
        scanMenus: scanMenus
    };

    if (typeof module !== 'undefined') {
        module.exports = root.SyncPlayShare;
    }

    if (root.document) {
        if (root.document.readyState === 'loading') {
            root.document.addEventListener('DOMContentLoaded', init, { once: true });
        } else {
            init();
        }

        root.setInterval(init, 1000);
    }
})(typeof window !== 'undefined' ? window : globalThis);
