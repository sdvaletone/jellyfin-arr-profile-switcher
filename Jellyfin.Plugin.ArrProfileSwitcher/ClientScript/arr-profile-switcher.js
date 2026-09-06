// Arr Profile Switcher -- quality-profile picker (three-dot menu + detail button)
// Loaded via File Transformation injection into Jellyfin's index.html (see
// Services/TransformationPatches.cs). This script is served anonymously and is
// trusted for NOTHING -- every check (what's tracked, which options exist, whether a
// request is allowed) is enforced server-side by ArrProfileSwitcherController; this
// file only decides what to render and forwards the user's choice.
//
// Menu/detail-page DOM structure and injection technique adapted from the
// WhisperSubs plugin's own client script (already live on this server), which in
// turn documents its own action-sheet / detail-button selectors as version/theme
// dependent -- the three-dot menu entry is the reliable path; the detail button is a
// best-effort bonus.
(function () {
    'use strict';

    var pendingItemId = null;
    var menuObserver = null;
    var statusCache = {};

    // ApiClient.ajax() on this Jellyfin version resolves with the raw fetch() Response
    // for a plain GET/POST rather than an auto-parsed body (confirmed live: the browser
    // console showed {type: basic, status: 200, ok: true, ...} coming out of .ajax(),
    // not the JSON payload) -- unlike ApiClient.getJSON(), which does parse. Handle
    // both shapes so this keeps working regardless of which one a given server version
    // returns, instead of silently treating an unparsed Response as falsy data.
    function parseAjaxResponse(resp) {
        if (resp && typeof resp.json === 'function' && typeof resp.ok === 'boolean') {
            return resp.json();
        }

        if (typeof resp === 'string') {
            return Promise.resolve(resp ? JSON.parse(resp) : null);
        }

        return Promise.resolve(resp);
    }

    function getStatus(itemId) {
        if (statusCache[itemId]) {
            return statusCache[itemId];
        }

        var url = ApiClient.getUrl('ArrProfileSwitcher/Status', { itemId: itemId });
        console.debug('[ArrProfileSwitcher] Status request for ' + itemId + ' -> ' + url);
        var promise = ApiClient.ajax({ type: 'GET', url: url }).then(parseAjaxResponse).then(function (data) {
            console.debug('[ArrProfileSwitcher] Status response for ' + itemId + ':', data);
            return data;
        }).catch(function (err) {
            console.error('[ArrProfileSwitcher] Status request failed for ' + itemId + ':', err);
            return null;
        });

        statusCache[itemId] = promise;
        // Short-lived: expire so a change just applied is reflected next time the
        // menu/button is opened, without re-querying on every DOM mutation.
        setTimeout(function () { delete statusCache[itemId]; }, 15000);
        return promise;
    }

    function invalidateStatus(itemId) {
        delete statusCache[itemId];
    }

    function showToast(message) {
        try {
            require(['toast'], function (toast) { toast(message); });
        } catch (e) {
            console.log('[ArrProfileSwitcher] ' + message);
        }
    }

    function closeDialog(el) {
        var dialog = el.closest('dialog');
        if (dialog && dialog.close) {
            dialog.close();
            return;
        }

        var sheet = el.closest('.actionSheet');
        if (sheet) {
            var cancel = sheet.querySelector('.btnCloseActionSheet');
            if (cancel) {
                cancel.click();
            }
        }
    }

    function applyOption(itemId, optionId) {
        console.debug('[ArrProfileSwitcher] applyOption called', { itemId: itemId, optionId: optionId });
        showToast('Updating quality profile…');
        var url = ApiClient.getUrl('ArrProfileSwitcher/Upgrade');
        var body = JSON.stringify({ ItemId: itemId, OptionId: optionId });
        console.debug('[ArrProfileSwitcher] Upgrade request -> ' + url, body);
        return ApiClient.ajax({
            type: 'POST',
            url: url,
            data: body,
            contentType: 'application/json'
        }).then(parseAjaxResponse).then(function (data) {
            console.debug('[ArrProfileSwitcher] Upgrade response', data);
            showToast((data && data.Message) ? data.Message : 'Quality profile updated');
            invalidateStatus(itemId);
        }).catch(function (err) {
            console.error('[ArrProfileSwitcher] Upgrade request failed', err);
            showToast('Could not update the quality profile');
        });
    }

    function createOptionMenuItem(itemId, option) {
        var btn = document.createElement('button');
        btn.setAttribute('is', 'emby-button');
        btn.type = 'button';
        btn.className = 'listItem listItem-button actionSheetMenuItem btnArrProfileOption';
        btn.setAttribute('data-option-id', option.OptionId);

        var label = option.Label + (option.IsCurrent ? ' (current)' : '');
        var iconName = option.IsCurrent ? 'check_circle' : 'hd';
        btn.innerHTML =
            '<span class="actionsheetMenuItemIcon listItemIcon listItemIcon-transparent material-icons" aria-hidden="true"></span>' +
            '<div class="listItemBody actionsheetListItemBody">' +
                '<div class="listItemBodyText actionSheetItemText"></div>' +
            '</div>';
        // Material Icons renders from the span's text content (a font ligature), not a
        // CSS class -- set via textContent, not baked into the class list above.
        btn.querySelector('.actionsheetMenuItemIcon').textContent = iconName;
        // Label via textContent (never innerHTML) so the admin-configured label can
        // never inject markup even though it's admin-supplied, not user-supplied.
        btn.querySelector('.actionSheetItemText').textContent = label;

        if (option.IsCurrent) {
            btn.disabled = true;
        } else {
            btn.addEventListener('click', function () {
                console.debug('[ArrProfileSwitcher] option button clicked', option.Label);
                closeDialog(btn);
                applyOption(itemId, option.OptionId);
            });
        }

        return btn;
    }

    function injectIntoActionSheet(sheet) {
        if (!pendingItemId) {
            console.debug('[ArrProfileSwitcher] injection skipped: no pending item id');
            return;
        }

        if (sheet.querySelector('.btnArrProfileOption')) {
            console.debug('[ArrProfileSwitcher] injection skipped: already injected');
            return;
        }

        var itemId = pendingItemId;
        getStatus(itemId).then(function (status) {
            try {
                // A second menu can be opened (on a different item) before this Status
                // request resolves -- if pendingItemId has since moved on, re-querying
                // '.actionSheet' below would find that NEWER sheet and inject THIS item's
                // (now-stale) options into it under the wrong item id entirely. Bail out
                // unless this response still belongs to the item currently being watched.
                if (itemId !== pendingItemId) {
                    console.debug('[ArrProfileSwitcher] injection skipped: superseded by a newer menu (' + itemId + ' -> ' + pendingItemId + ')');
                    return;
                }

                // Jellyfin's action sheet render can complete (or the sheet can be torn
                // down/replaced rather than just mutated) while this Status request was
                // still in flight -- re-query fresh instead of trusting the `sheet`
                // reference captured back when the MutationObserver first fired, and bail
                // out cleanly if it's gone rather than inserting into a detached node.
                var freshSheet = document.querySelector('.actionSheet') || sheet;
                if (!freshSheet || !document.body.contains(freshSheet)) {
                    console.debug('[ArrProfileSwitcher] injection skipped: actionSheet no longer in the DOM for ' + itemId);
                    return;
                }

                if (!status) {
                    console.debug('[ArrProfileSwitcher] injection skipped: Status request for ' + itemId + ' failed or returned nothing (see prior error above)');
                    return;
                }

                if (!status.Tracked) {
                    console.debug('[ArrProfileSwitcher] injection skipped: ' + itemId + ' not tracked (' + status.Message + ')');
                    return;
                }

                if (!status.Options || !status.Options.length) {
                    console.debug('[ArrProfileSwitcher] injection skipped: ' + itemId + ' tracked but no configured options');
                    return;
                }

                if (freshSheet.querySelector('.btnArrProfileOption')) {
                    console.debug('[ArrProfileSwitcher] injection skipped: already injected (post-async check)');
                    return; // re-check after the async status call to avoid a double-inject
                }

                var scroller = freshSheet.querySelector('.actionSheetScroller') || freshSheet;
                var cancelDiv = scroller.querySelector('.buttons');

                var frag = document.createDocumentFragment();
                for (var i = 0; i < status.Options.length; i++) {
                    frag.appendChild(createOptionMenuItem(itemId, status.Options[i]));
                }

                if (cancelDiv) {
                    scroller.insertBefore(frag, cancelDiv);
                } else {
                    scroller.appendChild(frag);
                }

                console.debug('[ArrProfileSwitcher] injected ' + status.Options.length + ' option(s) for ' + itemId);
            } catch (err) {
                console.error('[ArrProfileSwitcher] injectIntoActionSheet error for ' + itemId, err);
            }
        });
    }

    function watchForActionSheet() {
        if (menuObserver) {
            menuObserver.disconnect();
        }

        menuObserver = new MutationObserver(function (mutations) {
            for (var i = 0; i < mutations.length; i++) {
                var added = mutations[i].addedNodes;
                for (var j = 0; j < added.length; j++) {
                    var node = added[j];
                    if (node.nodeType !== 1) {
                        continue;
                    }

                    var sheet = null;
                    if (node.classList && node.classList.contains('actionSheet')) {
                        sheet = node;
                    } else if (node.querySelector) {
                        sheet = node.querySelector('.actionSheet');
                    }

                    if (sheet) {
                        menuObserver.disconnect();
                        menuObserver = null;
                        injectIntoActionSheet(sheet);
                        return;
                    }
                }
            }
        });

        menuObserver.observe(document.body, { childList: true, subtree: true });

        setTimeout(function () {
            if (menuObserver) {
                menuObserver.disconnect();
                menuObserver = null;
            }
        }, 3000);
    }

    // Capture clicks on three-dot menu triggers everywhere (capture phase so this
    // runs before Jellyfin opens the action sheet).
    document.addEventListener('click', function (e) {
        try {
            if (!e.target || e.target.nodeType !== 1) {
                return;
            }

            var trigger = e.target.closest('.btnMoreCommands, [data-action="menu"]');
            if (!trigger) {
                return;
            }

            // Reset on every trigger click rather than only on success -- otherwise a
            // failed extraction here (no [data-id] ancestor, no id= in the hash) would
            // silently fall through to whatever pendingItemId a PREVIOUS, unrelated click
            // happened to leave behind, and the menu would inject options for the wrong
            // item instead of visibly doing nothing.
            pendingItemId = null;

            var card = trigger.closest('[data-id]');
            if (card) {
                pendingItemId = card.getAttribute('data-id');
            } else {
                var hash = window.location.hash || '';
                var q = hash.indexOf('?');
                if (q !== -1) {
                    var params = new URLSearchParams(hash.substring(q + 1));
                    pendingItemId = params.get('id');
                }
            }

            if (pendingItemId) {
                console.debug('[ArrProfileSwitcher] menu trigger clicked, item id = ' + pendingItemId);
                watchForActionSheet();
            } else {
                console.debug('[ArrProfileSwitcher] menu trigger clicked but no item id found (no [data-id] ancestor and no id= in the URL hash)');
            }
        } catch (err) {
            console.error('[ArrProfileSwitcher] click handler error', err);
        }
    }, true);

    // Detail-page button: best-effort bonus surface. Reuses Jellyfin's own actionsheet
    // module for the option list instead of a bespoke dropdown; if that module isn't
    // available in a given Jellyfin version, this fails silently -- the three-dot menu
    // entry above is the reliable path either way.
    function showOptionsActionSheet(itemId, anchorEl, options) {
        try {
            require(['actionsheet'], function (actionsheet) {
                var menuItems = options.map(function (o) {
                    return {
                        id: o.OptionId,
                        name: o.Label + (o.IsCurrent ? ' (current)' : ''),
                        selected: o.IsCurrent,
                        icon: o.IsCurrent ? 'check_circle' : 'hd'
                    };
                });

                actionsheet.show({
                    items: menuItems,
                    positionTo: anchorEl,
                    callback: function (id) {
                        var chosen = null;
                        for (var i = 0; i < options.length; i++) {
                            if (String(options[i].OptionId) === String(id)) {
                                chosen = options[i];
                                break;
                            }
                        }

                        if (chosen && !chosen.IsCurrent) {
                            applyOption(itemId, id);
                        }
                    }
                });
            });
        } catch (err) {
            console.debug('[ArrProfileSwitcher] actionsheet module unavailable', err);
        }
    }

    function injectDetailButton() {
        try {
            var page = document.querySelector('.libraryPage:not(.hide), .itemDetailPage:not(.hide), .detailPage:not(.hide)');
            if (!page) {
                return;
            }

            var hash = window.location.hash || '';
            var m = hash.match(/[?&]id=([^&]+)/);
            if (!m) {
                return;
            }

            var itemId = decodeURIComponent(m[1]);

            var row = page.querySelector('.mainDetailButtons, .detailButtons, .itemActionsBottom, .detailButtonsContainer');
            if (!row) {
                return;
            }

            if (row.querySelector('.btnArrProfileSwitcherDetail')) {
                return;
            }

            getStatus(itemId).then(function (status) {
                if (!status || !status.Tracked || !status.Options || !status.Options.length) {
                    return;
                }

                if (row.querySelector('.btnArrProfileSwitcherDetail')) {
                    return;
                }

                var btn = document.createElement('button');
                btn.setAttribute('is', 'emby-button');
                btn.type = 'button';
                btn.className = 'button-flat detailButton emby-button btnArrProfileSwitcherDetail';
                btn.title = 'Change quality profile';
                btn.innerHTML =
                    '<div class="detailButton-content">' +
                        '<span class="material-icons detailButton-icon" aria-hidden="true"></span>' +
                        '<span class="detailButton-icon-text"></span>' +
                    '</div>';
                // Material Icons renders from the span's text content (a font ligature),
                // not a CSS class.
                btn.querySelector('.detailButton-icon').textContent = 'hd';
                btn.querySelector('.detailButton-icon-text').textContent = status.CurrentProfileName || 'Quality';

                btn.addEventListener('click', function (e) {
                    e.preventDefault();
                    getStatus(itemId).then(function (freshStatus) {
                        if (freshStatus && freshStatus.Options && freshStatus.Options.length) {
                            showOptionsActionSheet(itemId, btn, freshStatus.Options);
                        }
                    });
                });

                row.appendChild(btn);
            });
        } catch (err) {
            console.debug('[ArrProfileSwitcher] injectDetailButton error', err);
        }
    }

    // Jellyfin rebuilds the detail DOM on each SPA navigation, so re-run on nav + render.
    var detailInjectTimer = null;
    function scheduleDetailInject() {
        // Cheap early-exit: this fires on every DOM mutation via the body observer, so
        // on non-detail pages (library grids, home, search) do almost nothing.
        if ((window.location.hash || '').indexOf('id=') === -1) {
            return;
        }

        if (detailInjectTimer) {
            clearTimeout(detailInjectTimer);
        }

        detailInjectTimer = setTimeout(injectDetailButton, 150);
    }

    window.addEventListener('hashchange', scheduleDetailInject);
    window.addEventListener('popstate', scheduleDetailInject);
    var detailObserver = new MutationObserver(scheduleDetailInject);
    detailObserver.observe(document.body, { childList: true, subtree: true });
    scheduleDetailInject();

    console.log('[ArrProfileSwitcher] client script loaded');
})();
