function onHost(msg) {
    const { action, data } = msg;

    if (action === "init") {
        currentThemeDark = !!data.dark;
        document.body.classList.toggle("dark", currentThemeDark);
        setTitle.textContent = data.title || "(chưa chọn)";
        setupSetTitle.textContent = data.title || "(chưa chọn)";
        lblMax.textContent = "Câu hỏi (tối đa " + (data.max ?? 0) + ")";
        drawDonut(0);
    }

    if (action === "theme") {
        currentThemeDark = !!data.dark;
        document.body.classList.toggle("dark", currentThemeDark);
        drawDonut(parseInt(donut.dataset.p || "0", 10));
    }

    if (action === "bindSet") {
        setTitle.textContent = data.title || "(chưa chọn)";
        setupSetTitle.textContent = data.title || "(chưa chọn)";
        lblMax.textContent = "Câu hỏi (tối đa " + (data.max ?? 0) + ")";
        if (data.sourceLanguage) {
            rebuildAnswerDropdown(
                [
                    { value: 0, text: data.sourceLanguage },
                    { value: 1, text: "Tiếng Việt" },
                    { value: 2, text: "Cả hai" }
                ],
                selAnswer.value || "0"
            );
        }
    }

    if (action === "setupDefaults") {
        const max = data.max ?? 0;
        lblMax.textContent = "Câu hỏi (tối đa " + max + ")";
        inpCount.max = max;
        inpCount.min = max > 0 ? 1 : 0;
        inpCount.value = data.count ?? 0;

        rebuildAnswerDropdown(
            data.answerModeOptions || [
                { value: 0, text: data.sourceLanguage || "Ngôn ngữ gốc" },
                { value: 1, text: "Tiếng Việt" },
                { value: 2, text: "Cả hai" }
            ],
            "" + (data.answerMode ?? 0)
        );

        setSwitch(swMulti, !!data.multi);
        setSwitch(swEssay, !!data.essay);
        setSwitch(swSentence, !!data.sentence);
        showSetup();
    }

    if (action === "renderQuiz") {
        renderQuiz(data);
    }

    if (action === "renderSentenceQuiz") {
        renderSentenceQuiz(data);
    }

    if (action === "renderSentenceLoading") {
        renderSentenceLoading(data);
    }

    if (action === "resetToEmpty") {
        resetToEmpty();
    }

    if (action === "progress") {
        progressText.textContent = data.text || "0 / 0";
    }

    if (action === "focusNext") {
        focusNext(data.next);
    }

    if (action === "setFooterMode") {
        setFooterMode(data.mode || "hidden");
    }

    if (action === "showSentenceGradeChoice") {
        showSentenceGradeChoice(data || {});
    }

    if (action === "hideSentenceGradeChoice") {
        hideSentenceGradeChoice();
    }

    if (action === "showResult") {
        resSetTitle.textContent = data.setTitle || "(chưa chọn)";
        resOk.textContent = "Đúng: " + (data.correct ?? 0);
        resBad.textContent = "Sai: " + (data.wrong ?? 0);
        resTime.textContent = data.elapsed
            ? "Thời gian: " + data.elapsed
            : "Thời gian: —";
        const p = data.percent ?? 0;
        donut.dataset.p = "" + p;
        drawDonut(p);
        showResult();
    }

    if (action === "applyReview") {
        applyReview(data.items || []);
    }

    if (action === "applySentenceReview") {
        applySentenceReview(data.items || []);
    }

    if (action === "toast") {
        toast(data.text || "", data.type || "info");
    }
}

if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener("message", e =>
        onHost(e.data)
    );
}
