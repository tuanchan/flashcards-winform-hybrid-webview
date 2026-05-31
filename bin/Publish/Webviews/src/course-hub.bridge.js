function notifyReady() {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({
            action: "ready",
            data: "{}"
        });
    }
}

function sendToBackend(action, data = {}) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({
            action,
            data: JSON.stringify(data)
        });
    }
}
