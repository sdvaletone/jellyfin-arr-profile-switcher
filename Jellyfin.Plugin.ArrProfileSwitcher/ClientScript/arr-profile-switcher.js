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

    function getStatus(itemId) {
        if (statusCache[itemId]) {
            return statusCache[itemId];
        }

        var url = ApiClient.getUrl('ArrProfileSwitcher/Status', { itemId: itemId });
        var promise = ApiClient.ajax({ type: 'GET', url: url }).then(function (resp) {
            return typeof resp === 'string' ? JSON.parse(resp) : resp;
        }).catch(function () {
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
        showToast('Updating quality profile…');
        var url = ApiClient.getUrl('ArrProfileSwitcher/Upgrade');
        return ApiClient.ajax({
            type: 'POST',
            url: url,
            data: JSON.stringify({ ItemId: itemId, OptionId: optionId }),
            contentType: 'application/json'
        }).then(function (response) {
            var data = typeof response === 'string' ? JSON.parse(response) : response;
            showToast((data && data.Message) ? data.Message : 'Quality profile updated');
            invalidateStatus(itemId);
        }).catch(function () {
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
        btn.innerHTML =
            '<span class="actionsheetMenuItemIcon listItemIcon listItemIcon-transparent material-icons ' +
                (option.IsCurrent ? 'check_circle' : 'hd') + '" aria-hidden="true"></span>' +
            '<div class="listItemBody actionsheetListItemBody">' +
                '<div class="listItemBodyText actionSheetItemText"></div>' +
            '</div>';
        // Label via textContent (never innerHTML) so the admin-configured label can
        // never inject markup even though it's admin-supplied, not user-supplied.
        btn.querySelector('.actionSheetItemText').textContent = label;

        if (option.IsCurrent) {
            btn.disabled = true;
        } else {
            btn.addEventListener('click', function () {
                closeDialog(btn);
                applyOption(itemId, option.OptionId);
            });
        }

        return btn;
    }

    function injectIntoActionSheet(sheet) {
        if (!pendingItemId) {
            return;
        }

        if (sheet.querySelector('.btnArrProfileOption')) {
            return;
        }

        var itemId = pendingItemId;
        getStatus(itemId).then(function (status) {
            if (!status || !status.Tracked || !status.Options || !status.Options.length) {
                return;
            }

            if (sheet.querySelector('.btnArrProfileOption')) {
                return; // re-check after the async status call to avoid a double-inject
            }

            var scroller = sheet.querySelector('.actionSheetScroller') || sheet;
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
                watchForActionSheet();
            }
        } catch (err) {
            return;
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
                        '<span class="material-icons detailButton-icon hd" aria-hidden="true"></span>' +
                        '<span class="detailButton-icon-text"></span>' +
                    '</div>';
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
