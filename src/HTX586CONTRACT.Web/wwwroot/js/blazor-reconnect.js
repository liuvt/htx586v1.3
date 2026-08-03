(() => {
    const start = async () => {
        const modal = document.getElementById('components-reconnect-modal');
        let failedRetryStarted = false;

        if (modal) {
            modal.addEventListener('components-reconnect-state-changed', async event => {
                const state = event.detail?.state;

                if (state === 'hide') {
                    failedRetryStarted = false;
                    return;
                }

                if (state === 'failed' && !failedRetryStarted) {
                    failedRetryStarted = true;
                    try {
                        const reconnected = await Blazor.reconnect();
                        if (!reconnected) location.reload();
                    } catch {
                        location.reload();
                    }
                }

                if (state === 'rejected') {
                    location.reload();
                }
            });
        }

        await Blazor.start({
            circuit: {
                configureSignalR: builder => {
                    builder.withServerTimeout(60000);
                    builder.withKeepAliveInterval(15000);
                }
            }
        });
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start, { once: true });
    } else {
        start();
    }
})();
