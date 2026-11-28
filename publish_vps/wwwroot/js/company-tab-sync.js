(() => {
    const CHANNEL_NAME = 'htx586-company-profile-v1';
    const STORAGE_KEY = 'htx586:company-profile-sync';
    const subscribers = new Map();

    const tabId = (() => {
        try {
            const existing = sessionStorage.getItem('htx586:tab-id');
            if (existing)
                return existing;

            const created = globalThis.crypto?.randomUUID?.() ??
                `${Date.now()}-${Math.random().toString(36).slice(2)}`;
            sessionStorage.setItem('htx586:tab-id', created);
            return created;
        } catch {
            return `${Date.now()}-${Math.random().toString(36).slice(2)}`;
        }
    })();

    const channel = typeof BroadcastChannel !== 'undefined'
        ? new BroadcastChannel(CHANNEL_NAME)
        : null;

    const normalizeCompanyId = companyId => String(companyId ?? '').trim().toLowerCase();

    const notifySubscribers = message => {
        if (!message || message.type !== 'company-changed' || message.sourceTabId === tabId)
            return;

        const companyId = normalizeCompanyId(message.companyId);
        if (!companyId)
            return;

        for (const subscriber of subscribers.values()) {
            if (subscriber.companyId !== companyId)
                continue;

            subscriber.dotNetRef
                .invokeMethodAsync('HandleCompanyChangedInAnotherTabAsync')
                .catch(() => {
                    // Circuit của subscriber có thể vừa đóng; lần dispose sẽ dọn đăng ký.
                });
        }
    };

    channel?.addEventListener('message', event => notifySubscribers(event.data));

    window.addEventListener('storage', event => {
        if (event.key !== STORAGE_KEY || !event.newValue)
            return;

        try {
            notifySubscribers(JSON.parse(event.newValue));
        } catch {
            // Bỏ qua payload không hợp lệ.
        }
    });

    window.htx586CompanyTabSync = {
        register(dotNetRef, companyId) {
            const token = globalThis.crypto?.randomUUID?.() ??
                `${Date.now()}-${Math.random().toString(36).slice(2)}`;

            subscribers.set(token, {
                dotNetRef,
                companyId: normalizeCompanyId(companyId)
            });

            return token;
        },

        unregister(token) {
            if (token)
                subscribers.delete(token);
        },

        publish(companyId) {
            const normalizedCompanyId = normalizeCompanyId(companyId);
            if (!normalizedCompanyId)
                return;

            const message = {
                type: 'company-changed',
                companyId: normalizedCompanyId,
                sourceTabId: tabId,
                at: Date.now()
            };

            if (channel) {
                channel.postMessage(message);
                return;
            }

            // Fallback cho browser không hỗ trợ BroadcastChannel. storage event chỉ
            // phát sang tab khác nên không tạo vòng lặp ở tab hiện tại.
            try {
                localStorage.setItem(STORAGE_KEY, JSON.stringify({
                    ...message,
                    nonce: `${Date.now()}-${Math.random()}`
                }));
            } catch {
                // localStorage có thể bị chặn; khi đó tab hiện tại vẫn hoạt động bình thường.
            }
        }
    };
})();
