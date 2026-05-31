function drawDonut(percent) {
    const ctx = donut.getContext("2d");
    const w = donut.width;
    const h = donut.height;

    ctx.clearRect(0, 0, w, h);

    const cx = w / 2;
    const cy = h / 2;
    const r = 44;
    const thick = 10;

    ctx.beginPath();
    ctx.strokeStyle = currentThemeDark
        ? "rgba(255,255,255,.18)"
        : "rgba(15,23,42,.12)";
    ctx.lineWidth = thick;
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.stroke();

    const start = -Math.PI / 2;
    const sweep =
        (Math.max(0, Math.min(100, percent)) / 100) * Math.PI * 2;

    const gradient = ctx.createLinearGradient(cx - r, cy - r, cx + r, cy + r);
    gradient.addColorStop(0, "#3e5cff");
    gradient.addColorStop(0.5, "#5b7aff");
    gradient.addColorStop(1, "#7894ff");

    ctx.beginPath();
    ctx.strokeStyle = gradient;
    ctx.lineWidth = thick;
    ctx.lineCap = "round";
    ctx.arc(cx, cy, r, start, start + sweep);
    ctx.stroke();

    ctx.shadowBlur = 12;
    ctx.shadowColor = "rgba(62, 92, 255, 0.6)";
    ctx.beginPath();
    ctx.strokeStyle = gradient;
    ctx.lineWidth = thick;
    ctx.lineCap = "round";
    ctx.arc(cx, cy, r, start, start + sweep);
    ctx.stroke();
    ctx.shadowBlur = 0;

    const txtGradient = ctx.createLinearGradient(cx - 30, cy - 10, cx + 30, cy + 10);
    txtGradient.addColorStop(0, "#3e5cff");
    txtGradient.addColorStop(1, "#7894ff");

    ctx.fillStyle = txtGradient;
    ctx.font = "bold 26px Segoe UI";
    const txt = percent + "%";
    const m = ctx.measureText(txt);
    ctx.fillText(txt, cx - m.width / 2, cy + 9);
}

function renderQuiz(payload) {
    quizHost.innerHTML = "";
    totalQuestions = payload.total || 0;
    setTitle.textContent = payload.setTitle || "(chưa chọn)";

    payload.questions.forEach(q => {
        const card = document.createElement("div");
        card.className = "card";
        card.id = "q_" + q.index;

        const top = document.createElement("div");
        top.className = "topRow";

        const small = document.createElement("div");
        small.className = "smallLabel";
        small.textContent = q.smallLabel || "";

        const idx = document.createElement("div");
        idx.className = "idx";
        idx.textContent = `${q.index}/${q.total}`;

        top.appendChild(small);
        top.appendChild(idx);

        const qt = document.createElement("div");
        qt.className = "qText";
        qt.textContent = q.questionText || "";
        if (q.useChineseFontForQuestion) {
            qt.style.fontFamily =
                '"DFKai-SB","KaiTi","Microsoft JhengHei","Microsoft JhengHei UI",system-ui';
        }

        const hint = document.createElement("div");
        hint.className = "hint";
        hint.textContent = "Chọn đáp án đúng";

        const grid = document.createElement("div");
        grid.className = "grid";

        const choices = q.choices || [];
        for (let i = 0; i < 4; i++) {
            const btn = document.createElement("div");
            btn.className = "choice";
            btn.dataset.qIndex = q.index;
            btn.dataset.choiceIndex = i;
            btn.textContent = choices[i] || "";

            if (q.useChineseFontForChoices) {
                btn.style.fontFamily =
                    '"DFKai-SB","KaiTi","Microsoft JhengHei","Microsoft JhengHei UI",system-ui';
                btn.style.fontSize = "33px";
            }

            btn.addEventListener("click", () => {
                grid.querySelectorAll(".choice")
                    .forEach(x => x.classList.remove("selected"));
                btn.classList.add("selected");
                post("pick", { qIndex: q.index, choiceIndex: i });
            });

            grid.appendChild(btn);
        }

        const dk = document.createElement("div");
        dk.className = "dontknow";
        dk.textContent = "Bạn không biết?";
        dk.addEventListener("click", () => {
            grid.querySelectorAll(".choice")
                .forEach(x => x.classList.remove("selected"));
            post("dontKnow", { qIndex: q.index });
        });

        card.appendChild(top);
        card.appendChild(qt);
        card.appendChild(hint);
        card.appendChild(grid);
        card.appendChild(dk);

        quizHost.appendChild(card);
    });

    hideSetup();
}

function renderSentenceQuiz(payload) {
    quizHost.innerHTML = "";
    totalQuestions = payload.total || 0;
    setTitle.textContent = payload.setTitle || "(chưa chọn)";
    progressText.textContent = "0 / " + totalQuestions;

    (payload.questions || []).forEach(q => {
        const card = document.createElement("div");
        card.className = "card sentence-card";
        card.id = "q_" + q.index;

        const top = document.createElement("div");
        top.className = "topRow";

        const badge = document.createElement("div");
        badge.className = "gemini-badge";
        badge.innerHTML = '<img src="icon/gemini-color.svg" alt=""> ' + (payload.fromCache ? "Gemini cache" : "Gemini đặt câu");

        const idx = document.createElement("div");
        idx.className = "idx";
        idx.textContent = `${q.index}/${q.total}`;

        top.appendChild(badge);
        top.appendChild(idx);

        const qt = document.createElement("div");
        qt.className = "qText";
        qt.textContent = q.prompt || "Dịch cụm này thành một câu tự nhiên.";

        const hint = document.createElement("div");
        hint.className = "hint";
        hint.textContent = payload.hint || "Nhập nghĩa của câu giao tiếp";

        const input = document.createElement("textarea");
        input.className = "sentenceInput";
        input.placeholder = q.placeholder || "Nhập đáp án...";
        input.dataset.qIndex = q.index;
        input.addEventListener("input", () => {
            post("sentenceAnswer", { qIndex: q.index, text: input.value || "" });
        });

        card.appendChild(top);
        card.appendChild(qt);
        card.appendChild(hint);
        card.appendChild(input);

        quizHost.appendChild(card);
    });

    hideSetup();
}

function renderSentenceLoading(payload) {
    quizHost.innerHTML = "";
    totalQuestions = payload.count || 0;
    setTitle.textContent = payload.setTitle || "(chưa chọn)";
    progressText.textContent = "Gemini đang tạo...";

    const count = Math.max(1, Math.min(totalQuestions || 3, 8));
    for (let i = 1; i <= count; i++) {
        const card = document.createElement("div");
        card.className = "card sentence-card sentence-loading-card";

        card.innerHTML = `
          <div class="topRow">
            <div class="gemini-badge"><img src="icon/gemini-color.svg" alt=""> Gemini đang tạo</div>
            <div class="idx">${i}/${count}</div>
          </div>
          <div class="shimmer-line title"></div>
          <div class="sentenceWords">
            <span class="shimmer-pill"></span>
            <span class="shimmer-pill short"></span>
            <span class="shimmer-pill"></span>
          </div>
          <div class="shimmer-line wide"></div>
          <div class="shimmer-line mid"></div>
          <div class="shimmer-line small"></div>
        `;

        quizHost.appendChild(card);
    }

    hideSetup();
    setFooterMode("hidden");
}

function resetToEmpty() {
    quizHost.innerHTML = "";
    setFooterMode("hidden");
}

function applyReview(items) {
    items.forEach(it => {
        const card = document.getElementById("q_" + it.qIndex);
        if (!card) return;

        const choices = Array.from(card.querySelectorAll(".choice"));
        choices.forEach(c => (c.style.pointerEvents = "none"));
        card.querySelector(".dontknow").style.pointerEvents = "none";

        const correctIndex = it.correctIndex;
        const pickedIndex = it.pickedIndex;

        choices.forEach((c, idx) => {
            c.classList.remove("selected");
            c.classList.remove("correct", "wrong", "neutral");

            if (idx === correctIndex) {
                c.classList.add("correct");
                return;
            }

            if (
                pickedIndex !== null &&
                pickedIndex !== undefined &&
                idx === pickedIndex &&
                idx !== correctIndex
            ) {
                c.classList.add("wrong");
                return;
            }

            c.classList.add("neutral");
        });
    });

    let firstWrong = null;
    for (const it of items) {
        if (
            it.pickedIndex !== null &&
            it.pickedIndex !== undefined &&
            it.pickedIndex !== it.correctIndex
        ) {
            firstWrong = document.getElementById("q_" + it.qIndex);
            break;
        }
        if (it.dontKnow) {
            firstWrong = document.getElementById("q_" + it.qIndex);
            break;
        }
    }

    if (firstWrong)
        firstWrong.scrollIntoView({ behavior: "auto", block: "center" });
}

function applySentenceReview(items) {
    items.forEach(it => {
        const card = document.getElementById("q_" + it.qIndex);
        if (!card) return;

        const input = card.querySelector(".sentenceInput");
        if (input) {
            input.disabled = true;
            input.value = it.userAnswer || "";
        }

        const old = card.querySelector(".sentenceReview");
        if (old) old.remove();

        const box = document.createElement("div");
        box.className = "sentenceReview " + (it.correct ? "correct" : "wrong");
        box.innerHTML =
            `<div>${it.correct ? "Đúng" : "Chưa đúng"}</div>` +
            `<div class="sentenceAnswer"><b>Câu giao tiếp:</b> ${quizEscape(it.sourceSentence || "")}</div>` +
            `<div class="sentenceAnswer"><b>Đáp án đúng:</b> ${quizEscape(it.expectedAnswer || "")}</div>` +
            `<div class="sentenceAnswer">${quizEscape(it.explanation || "")}</div>`;

        card.appendChild(box);
    });

    const firstWrong = items.find(it => !it.correct);
    if (firstWrong) {
        const card = document.getElementById("q_" + firstWrong.qIndex);
        if (card) card.scrollIntoView({ behavior: "auto", block: "center" });
    }
}

function quizEscape(str) {
    return String(str || "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function focusNext(next) {
    if (!next || next <= 0) return;
    const card = document.getElementById("q_" + next);
    if (!card) return;

    const rect = card.getBoundingClientRect();
    const visibleTop = 72;
    const visibleBottom = window.innerHeight - 96;
    if (rect.top >= visibleTop && rect.bottom <= visibleBottom) return;

    card.scrollIntoView({ behavior: "auto", block: "center" });
}
