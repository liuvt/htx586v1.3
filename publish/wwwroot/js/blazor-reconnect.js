(() => {
    const STALE_TAB_RELOAD_MS = 5 * 60 * 1000;
    const HARD_RELOAD_GUARD_MS = 15 * 1000;
    const reconnectModalId = 'components-reconnect-modal';

    let reconnectState = 'starting';
    let hadConnectionLoss = false;
    let hiddenAt = document.hidden ? Date.now() : null;
    let recoveryInFlight = false;
    let reloadRequested = false;

    const requestReload = reason => {
        if (reloadRequested)
            return false;

        const now = Date.now();
        try {
            const lastReloadAt = Number(sessionStorage.getItem('htx586:last-hard-reload-at') ?? '0');
            if (lastReloadAt > 0 && now - lastReloadAt < HARD_RELOAD_GUARD_MS)
                return false;

            sessionStorage.setItem('htx586:last-hard-reload-at', String(now));
            sessionStorage.setItem('htx586:last-reload-reason', reason ?? 'unknown');
        } catch {
            // sessionStorage có thể bị chặn ở một số chế độ riêng tư.
        }

        reloadRequested = true;
        location.reload();
        return true;
    };

    const tryReconnect = async reason => {
        if (recoveryInFlight || reloadRequested)
            return;

        recoveryInFlight = true;
        try {
            const reconnected = await Blazor.reconnect();
            if (!reconnected && navigator.onLine)
                requestReload(reason ?? 'reconnect-returned-false');
        } catch {
            if (navigator.onLine)
                requestReload(reason ?? 'reconnect-threw');
        } finally {
            recoveryInFlight = false;
        }
    };

    const handleReconnectStateChanged = async event => {
        const state = event.detail?.state;
        if (!state)
            return;

        reconnectState = state;

        if (state === 'show' || state === 'retrying' || state === 'paused') {
            hadConnectionLoss = true;
            return;
        }

        if (state === 'hide') {
            // Sau khi circuit từng mất kết nối, tải mới trang để loại bỏ mọi trạng thái
            // UI có thể đã treo (_saving/_editing/dialog...) và lấy dữ liệu mới từ server.
            if (hadConnectionLoss) {
                requestReload('reconnected-after-disconnect');
                return;
            }

            reconnectState = 'connected';
            return;
        }

        if (state === 'failed') {
            if (navigator.onLine)
                await tryReconnect('reconnect-failed');
            return;
        }

        if (state === 'rejected') {
            // Server còn sống nhưng circuit cũ không còn tồn tại.
            requestReload('reconnect-rejected');
        }
    };

    const handleVisibilityChange = () => {
        if (document.hidden) {
            hiddenAt = Date.now();
            return;
        }

        const now = Date.now();
        const hiddenDuration = hiddenAt ? now - hiddenAt : 0;
        hiddenAt = null;

        // Nếu tab đã ngủ/treo quá lâu, luôn khởi tạo circuit mới. Đây là cách an toàn
        // nhất để tránh giữ trạng thái form cũ sau khi trình duyệt throttle tab nền.
        if (hiddenDuration >= STALE_TAB_RELOAD_MS) {
            if (navigator.onLine)
                requestReload('stale-background-tab');
            return;
        }

        // .NET 9 tự reconnect ngay khi quay lại tab. Chỉ gọi Blazor.reconnect()
        // thủ công khi framework đã báo failed để tránh tạo hai lần reconnect song song.
        if (reconnectState === 'failed')
            void tryReconnect('tab-visible-after-failed-reconnect');
    };

    const handlePageShow = event => {
        // Trang được khôi phục từ back/forward cache có thể chứa DOM và JS state cũ
        // nhưng circuit Blazor phía server đã hết hạn.
        if (event.persisted)
            requestReload('page-restored-from-bfcache');
    };

    const handleOnline = () => {
        if (reconnectState === 'failed') {
            void tryReconnect('browser-online-after-failed-reconnect');
            return;
        }

        if (reconnectState === 'start-failed')
            requestReload('browser-online-after-start-failed');
    };

    const start = async () => {
        const modal = document.getElementById(reconnectModalId);
        if (modal)
            modal.addEventListener('components-reconnect-state-changed', handleReconnectStateChanged);

        document.addEventListener('visibilitychange', handleVisibilityChange);
        window.addEventListener('pageshow', handlePageShow);
        window.addEventListener('online', handleOnline);

        try {
            await Blazor.start({
                circuit: {
                    reconnectionOptions: {
                        maxRetries: 8,
                        retryIntervalMilliseconds: Array.prototype.at.bind([
                            0,
                            500,
                            1000,
                            2000,
                            5000,
                            10000,
                            15000,
                            30000
                        ])
                    },
                    configureSignalR: builder => {
                        builder.withServerTimeout(60000);
                        builder.withKeepAliveInterval(15000);
                    }
                }
            });

            reconnectState = 'connected';
        } catch {
            reconnectState = 'start-failed';

            // Nếu ngay cả bước khởi tạo circuit cũng lỗi, chỉ tải lại khi browser đang
            // online. Guard 15 giây phía trên ngăn vòng lặp reload khi server/proxy lỗi.
            if (navigator.onLine)
                requestReload('blazor-start-failed');
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start, { once: true });
    } else {
        void start();
    }
})();
