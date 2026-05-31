(() => {
  const STACK_ID = "unifiedToastStack";
  const DEFAULT_DURATION = 3600;

  function normalizeType(type) {
    const value = String(type || "info").toLowerCase();
    if (value === "ok" || value === "success") return "success";
    if (value === "warning") return "warn";
    if (value === "danger") return "error";
    if (value === "warn" || value === "error") return value;
    return "info";
  }

  function normalizeOptions(typeOrOptions, maybeOptions) {
    const base = typeof typeOrOptions === "object" && typeOrOptions !== null
      ? { ...typeOrOptions }
      : { ...(maybeOptions || {}), type: typeOrOptions };

    base.type = normalizeType(base.type);
    base.duration = Number.isFinite(Number(base.duration))
      ? Number(base.duration)
      : DEFAULT_DURATION;
    return base;
  }

  function ensureStack() {
    let stack = document.getElementById(STACK_ID);
    if (!stack) {
      stack = document.createElement("div");
      stack.id = STACK_ID;
      stack.className = "unified-toast-stack";
      document.body.appendChild(stack);
    }
    return stack;
  }

  function closeToast(toast) {
    if (!toast || toast.dataset.closing === "1") return;
    toast.dataset.closing = "1";
    toast.classList.remove("show");
    toast.classList.add("hide");
    window.setTimeout(() => toast.remove(), 220);
  }

  function showUnifiedToast(message, typeOrOptions = "info", maybeOptions = {}) {
    const text = String(message ?? "").trim();
    if (!text) return null;

    const options = normalizeOptions(typeOrOptions, maybeOptions);
    const toast = document.createElement("div");
    toast.className = `unified-toast ${options.type}`;
    toast.setAttribute("role", options.type === "error" || options.type === "warn" ? "alert" : "status");

    const body = document.createElement("div");
    body.className = "unified-toast-body";

    if (options.title) {
      const title = document.createElement("div");
      title.className = "unified-toast-title";
      title.textContent = String(options.title);
      body.appendChild(title);
    }

    const messageNode = document.createElement("div");
    messageNode.className = "unified-toast-message";
    messageNode.textContent = text;
    body.appendChild(messageNode);

    const close = document.createElement("button");
    close.type = "button";
    close.className = "unified-toast-close";
    close.setAttribute("aria-label", "Dong thong bao");
    close.textContent = "X";
    close.addEventListener("click", () => closeToast(toast));

    toast.appendChild(body);
    toast.appendChild(close);
    ensureStack().appendChild(toast);

    requestAnimationFrame(() => toast.classList.add("show"));

    if (options.duration > 0) {
      window.setTimeout(() => closeToast(toast), options.duration);
    }

    return {
      element: toast,
      close: () => closeToast(toast)
    };
  }

  window.__unifiedToast = showUnifiedToast;
  window.showToast = showUnifiedToast;
  window.toast = showUnifiedToast;
  window.alert = message => showUnifiedToast(message, {
    type: "warn",
    title: "Thông báo",
    duration: 5200
  });
})();
