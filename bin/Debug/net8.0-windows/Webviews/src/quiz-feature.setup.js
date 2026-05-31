function rebuildAnswerDropdown(options, selectedValue) {
    answerModeOptions = Array.isArray(options) && options.length
        ? options.map(x => ({
            value: parseInt(x.value, 10),
            text: x.text || ""
        }))
        : [
            { value: 0, text: "Ngôn ngữ gốc" },
            { value: 1, text: "Tiếng Việt" },
            { value: 2, text: "Cả hai" }
        ];

    ddAnswerMenu.innerHTML = "";

    answerModeOptions.forEach(opt => {
        const item = document.createElement("div");
        item.className = "ddItem";
        item.dataset.value = String(opt.value);
        item.textContent = opt.text;
        if (String(opt.value) === String(selectedValue)) {
            item.classList.add("active");
        }
        ddAnswerMenu.appendChild(item);
    });

    const found = answerModeOptions.find(
        x => String(x.value) === String(selectedValue)
    );

    selAnswer.value = String(selectedValue ?? 0);
    ddAnswerText.textContent = found
        ? found.text
        : (answerModeOptions[0]?.text || "Ngôn ngữ gốc");
}

function ddClose() {
    ddAnswer.classList.remove("open");
    ddAnswerMenu.classList.add("hidden");
}

function ddOpen() {
    ddAnswer.classList.add("open");
    ddAnswerMenu.classList.remove("hidden");

    const v = selAnswer.value;
    ddAnswerMenu.querySelectorAll(".ddItem").forEach(it => {
        it.classList.toggle("active", it.dataset.value === v);
    });
}

ddAnswerBtn.addEventListener("click", ev => {
    ev.stopPropagation();
    if (ddAnswerMenu.classList.contains("hidden")) ddOpen();
    else ddClose();
});

ddAnswerMenu.addEventListener("click", ev => {
    const it = ev.target.closest(".ddItem");
    if (!it) return;
    const v = it.dataset.value || "0";
    selAnswer.value = v;
    ddAnswerText.textContent = it.textContent || "";
    ddClose();
});

document.addEventListener("click", () => ddClose());

function showSetup() {
    setupOverlay.classList.remove("hidden");
}

function hideSetup() {
    setupOverlay.classList.add("hidden");
}

function showResult() {
    pageContent.classList.add("blurred");
    footerContent.classList.add("blurred");
    resultOverlay.classList.remove("hidden");
}

function hideResult() {
    pageContent.classList.remove("blurred");
    footerContent.classList.remove("blurred");
    resultOverlay.classList.add("hidden");
}

function showSentenceGradeChoice(payload = {}) {
    pageContent.classList.add("blurred");
    footerContent.classList.add("blurred");
    sentenceGradeSetTitle.textContent = payload.setTitle || setTitle.textContent || "(chưa chọn)";
    sentenceGradeOverlay.classList.remove("hidden");
}

function hideSentenceGradeChoice() {
    pageContent.classList.remove("blurred");
    footerContent.classList.remove("blurred");
    sentenceGradeOverlay.classList.add("hidden");
}

function setSwitch(el, on) {
    if (on) el.classList.add("on");
    else el.classList.remove("on");
}

function getSwitch(el) {
    return el.classList.contains("on");
}

swMulti.addEventListener("click", () => {
    const next = !getSwitch(swMulti);
    if (!next && !getSwitch(swEssay) && !getSwitch(swSentence)) {
        setSwitch(swMulti, true);
        return;
    }
    setSwitch(swMulti, next);
    if (next) {
        setSwitch(swEssay, false);
        setSwitch(swSentence, false);
    }
});

swEssay.addEventListener("click", () => {
    const next = !getSwitch(swEssay);
    if (!next && !getSwitch(swMulti) && !getSwitch(swSentence)) {
        setSwitch(swEssay, true);
        return;
    }
    setSwitch(swEssay, next);
    if (next) {
        setSwitch(swMulti, false);
        setSwitch(swSentence, false);
    }
});

swSentence.addEventListener("click", () => {
    const next = !getSwitch(swSentence);
    if (!next && !getSwitch(swMulti) && !getSwitch(swEssay)) {
        setSwitch(swSentence, true);
        return;
    }
    setSwitch(swSentence, next);
    if (next) {
        setSwitch(swMulti, false);
        setSwitch(swEssay, false);
    }
});

setupClose.addEventListener("click", () => {
    hideSetup();
    post("closeSetup", {});
});

btnStart.addEventListener("click", () => {
    const payload = {
        count: parseInt(inpCount.value || "0", 10),
        answerMode: parseInt(selAnswer.value || "0", 10),
        multi: getSwitch(swMulti),
        essay: getSwitch(swEssay),
        sentence: getSwitch(swSentence)
    };
    hideSetup();
    post("startFromSetup", payload);
});

footerBtnPrimary.addEventListener("click", () => {
    post("submit", {});
});

footerBtnGhost.addEventListener("click", () => {
    post("goHome", {});
});

resClose.addEventListener("click", () => hideResult());
btnExit.addEventListener("click", () => {
    hideResult();
    post("goHome", {});
});
btnViewResult.addEventListener("click", () => {
    hideResult();
    post("viewResult", {});
});

sentenceGradeClose.addEventListener("click", () => hideSentenceGradeChoice());
btnSentenceGradeLocal.addEventListener("click", () => {
    hideSentenceGradeChoice();
    post("sentenceGradeLocal", {});
});
btnSentenceGradeGemini.addEventListener("click", () => {
    hideSentenceGradeChoice();
    post("sentenceGradeGemini", {});
});

function setFooterMode(mode) {
    if (mode === "hidden") {
        footerCard.classList.add("hidden");
        return;
    }

    footerCard.classList.remove("hidden");

    if (mode === "submit") {
        footerTitle.textContent =
            "Tất cả đã xong! Bạn đã sẵn sàng gửi bài kiểm tra?";
        footerBtnPrimary.classList.remove("hidden");
        footerBtnGhost.classList.add("hidden");
        return;
    }

    if (mode === "goHome") {
        footerTitle.textContent =
            "Đã hiển thị kết quả. Bạn muốn quay về trang chủ?";
        footerBtnPrimary.classList.add("hidden");
        footerBtnGhost.classList.remove("hidden");
    }
}

function toast(text, type = "info") {
    if (window.__unifiedToast) {
        window.__unifiedToast(text, type || "info");
        return;
    }

    const t = document.createElement("div");
    t.className = "toast";
    t.textContent = text;
    document.body.appendChild(t);

    setTimeout(() => {
        t.style.opacity = "0";
        t.style.transition = "opacity .2s ease";
    }, 1400);

    setTimeout(() => {
        t.remove();
    }, 1650);
}
