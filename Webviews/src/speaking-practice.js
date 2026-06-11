(() => {
    let voices = [];
    let courses = [];
    let sourceMode = "course";
    let selectedLanguage = "";
    let courseFilter = "";
    let messages = [];
    let recognition = null;
    let recordingIndex = -1;
    let audio = null;
    let busy = false;
    let showVietnamese = false;
    let audioSessionId = "";
    let pendingAutoPlayIndex = -1;
    const audioCache = new Map();

    const $ = id => document.getElementById(id);

    function post(action, data = {}) {
        if (typeof sendToBackend === "function") sendToBackend(action, data);
    }

    function setup() {
        $("btnSpeakingBack")?.addEventListener("click", closeSpeakingPractice);
        $("btnSpeakingSettingsHide")?.addEventListener("click", toggleSettings);
        $("btnSpeakingSettingsShow")?.addEventListener("click", toggleSettings);
        $("btnSpeakingGenerate")?.addEventListener("click", generate);
        $("btnSpeakingReset")?.addEventListener("click", resetConversation);
        $("btnSpeakingVietnamese")?.addEventListener("click", toggleVietnamese);
        $("btnSpeakingImport")?.addEventListener("click", openImportPanel);
        $("btnSpeakingCopyPrompt")?.addEventListener("click", copyImportPrompt);
        $("btnSpeakingPasteImport")?.addEventListener("click", pasteImportJson);
        $("btnSpeakingApplyImport")?.addEventListener("click", applyImport);
        $("btnSpeakingCancelImport")?.addEventListener("click", closeImportPanel);
        $("speakingModeCourse")?.addEventListener("click", () => setSourceMode("course"));
        $("speakingModeTopic")?.addEventListener("click", () => setSourceMode("topic"));
        $("speakingVoiceSource")?.addEventListener("change", updateVoiceMode);
        $("speakingLanguage")?.addEventListener("change", e => {
            selectedLanguage = e.target.value || selectedLanguage;
            renderVoiceOptions();
        });
        $("speakingCourseSearch")?.addEventListener("input", e => {
            courseFilter = e.target.value || "";
            renderCourses($("speakingCourseSelect")?.value || selectedSet?.id || "");
        });
        $("speakingCourseSelect")?.addEventListener("change", syncLanguageFromCourse);
        $("speakingPracticeMode")?.addEventListener("change", renderMessages);

        document.addEventListener("keydown", e => {
            if (e.key === "Escape" && document.body.classList.contains("speaking-mode")) {
                closeSpeakingPractice();
            }
        });

        setSourceMode("course");
        updateVoiceMode();
        renderMessages();
    }

    window.openSpeakingPractice = function () {
        if (typeof closeWritingPractice === "function") closeWritingPractice();
        document.body.classList.add("speaking-mode");
        $("speakingScreen")?.classList.add("show");
        $("speakingScreen")?.setAttribute("aria-hidden", "false");
        post("getSpeakingOptions");
    };

    window.closeSpeakingPractice = function () {
        stopRecognition();
        clearSpeakingAudio();
        document.body.classList.remove("speaking-mode");
        $("speakingScreen")?.classList.remove("show");
        $("speakingScreen")?.setAttribute("aria-hidden", "true");
    };

    function toggleSettings() {
        document.querySelector(".speaking-modal")?.classList.toggle("settings-collapsed");
    }

    function setSourceMode(mode) {
        sourceMode = mode === "topic" ? "topic" : "course";
        $("speakingModeCourse")?.classList.toggle("active", sourceMode === "course");
        $("speakingModeTopic")?.classList.toggle("active", sourceMode === "topic");
        document.querySelector(".speaking-modal")?.classList.toggle("topic-mode", sourceMode === "topic");
    }

    function updateVoiceMode() {
        const custom = $("speakingVoiceSource")?.value === "custom";
        document.querySelector(".speaking-modal")?.classList.toggle("custom-voice", custom);
        if (!custom) syncLanguageFromCourse();
    }

    function renderCourses(preferred = "") {
        const q = courseFilter.trim().toLowerCase();
        const list = q
            ? courses.filter(x => String(x.title || "").toLowerCase().includes(q))
            : courses;
        const select = $("speakingCourseSelect");
        if (!select) return;
        select.innerHTML = list.map(x =>
            `<option value="${esc(x.id)}">${esc(x.title)} (${Number(x.count || 0)})</option>`
        ).join("");
        const wanted = preferred || selectedSet?.id || "";
        if (list.some(x => x.id === wanted)) select.value = wanted;
        syncLanguageFromCourse();
    }

    function syncLanguageFromCourse() {
        if ($("speakingVoiceSource")?.value === "custom") return;
        const id = $("speakingCourseSelect")?.value || selectedSet?.id || "";
        const course = courses.find(x => x.id === id);
        const key = String(course?.languageCode || course?.language || "").split("-")[0].toLowerCase();
        selectedLanguage = key || selectedLanguage || "en";
        renderLanguageOptions();
        renderVoiceOptions();
    }

    function renderLanguageOptions() {
        const map = new Map();
        voices.forEach(v => {
            const key = v.languageKey || String(v.languageCode || "").split("-")[0] || v.voice;
            if (!map.has(key)) map.set(key, v.languageName || key);
        });
        const select = $("speakingLanguage");
        if (!select) return;
        if (!map.has(selectedLanguage)) selectedLanguage = map.keys().next().value || "en";
        select.innerHTML = [...map].map(([key, label]) =>
            `<option value="${esc(key)}">${esc(label)}</option>`
        ).join("");
        select.value = selectedLanguage;
    }

    function renderVoiceOptions(preferred = "") {
        const list = voices.filter(v =>
            (v.languageKey || String(v.languageCode || "").split("-")[0]) === selectedLanguage
        );
        const source = list.length ? list : voices;
        const select = $("speakingAiVoice");
        if (!select) return;
        const old = preferred || select.value;
        select.innerHTML = source.map(v =>
            `<option value="${esc(v.voice)}">${esc(v.label || v.voice)}</option>`
        ).join("");
        select.value = source.some(v => v.voice === old)
            ? old
            : (source.find(v => String(v.gender).toLowerCase() === "female")?.voice || source[0]?.voice || "");
    }

    function targetInfo() {
        const custom = $("speakingVoiceSource")?.value === "custom";
        const course = courses.find(x => x.id === ($("speakingCourseSelect")?.value || selectedSet?.id || ""));
        const key = custom
            ? ($("speakingLanguage")?.value || selectedLanguage || "en")
            : (String(course?.languageCode || course?.language || "").split("-")[0].toLowerCase() || selectedLanguage || "en");
        const voice = custom
            ? ($("speakingAiVoice")?.value || "")
            : (voices.find(v => (v.languageKey || "").toLowerCase() === key)?.voice || $("speakingAiVoice")?.value || "");
        const info = voices.find(v => v.voice === voice) ||
            voices.find(v => (v.languageKey || "").toLowerCase() === key);
        return {
            languageKey: key,
            languageName: info?.languageName || course?.language || key,
            languageCode: info?.languageCode || course?.languageCode || key,
            voice
        };
    }

    function generate() {
        if (busy) return;
        const courseId = $("speakingCourseSelect")?.value || selectedSet?.id || "";
        const topic = $("speakingTopicInput")?.value || "";
        if (sourceMode === "course" && !courseId) {
            showToast("Chọn học phần trước khi tạo đối thoại.", "warn");
            return;
        }
        if (sourceMode === "topic" && !topic.trim()) {
            showToast("Nhập chủ đề trước khi tạo đối thoại.", "warn");
            return;
        }
        const difficulty = $("speakingDifficulty")?.value || "basic";
        const count = difficulty === "advanced" ? 14 : difficulty === "hard" ? 10 : 6;
        const target = targetInfo();
        clearSpeakingAudio();
        post("generateSpeakingPractice", {
            mode: sourceMode,
            courseId,
            topic,
            difficulty,
            count,
            targetLanguageKey: target.languageKey,
            targetLanguageName: target.languageName,
            targetLanguageCode: target.languageCode,
            voice: target.voice
        });
    }

    function resetConversation() {
        stopRecognition();
        stopAudio();
        messages.forEach(x => {
            x.transcript = "";
            x.score = null;
        });
        renderMessages();
        setStatus(messages.length ? "Đã xóa kết quả. Bấm micro để luyện lại." : "Chưa có đối thoại.");
    }

    function toggleVietnamese() {
        showVietnamese = !showVietnamese;
        const button = $("btnSpeakingVietnamese");
        button?.setAttribute("aria-pressed", showVietnamese ? "true" : "false");
        button?.setAttribute("title", showVietnamese ? "Ẩn transcript tiếng Việt" : "Hiện transcript tiếng Việt");
        const icon = button?.querySelector("i");
        if (icon) {
            icon.classList.toggle("fa-eye", !showVietnamese);
            icon.classList.toggle("fa-eye-slash", showVietnamese);
        }
        renderMessages();
    }

    function togglePronunciation(index) {
        const item = messages[index];
        if (!item || item.role !== "user") return;
        item.showPronunciation = !item.showPronunciation;
        renderMessages();
    }

    function renderMessages() {
        const list = $("speakingMessageList");
        if (!list) return;
        if (!messages.length) {
            list.innerHTML = `<div class="speaking-empty">Chưa có đối thoại luyện nói.</div>`;
            return;
        }
        const guided = ($("speakingPracticeMode")?.value || "guided") === "guided";
        list.innerHTML = messages.map((item, index) => {
            const user = item.role === "user";
            const expected = user
                ? (guided ? item.text : "Nói câu trả lời tự do")
                : item.text;
            const transcript = item.transcript
                ? (guided ? buildComparisonHtml(item.text, item.transcript, item.languageCode) : esc(item.transcript))
                : "";
            const vietnamese = showVietnamese && item.vietnamese
                ? `<div class="speaking-vietnamese show">${esc(item.vietnamese)}</div>`
                : "";
            const pronunciation = user && item.showPronunciation && item.vietnamesePronunciation
                ? `<div class="speaking-pronunciation show">${esc(item.vietnamesePronunciation)}</div>`
                : "";
            return `
                <article class="speaking-message ${user ? "user" : "ai"} ${user && !guided ? "free" : ""} ${recordingIndex === index ? "active" : ""} ${!user && item.audioStatus === "loading" ? "audio-loading" : ""}" data-speaking-index="${index}">
                    <div class="speaking-bubble">
                        <div class="speaking-message-head">
                            <span class="speaking-speaker">${user ? "User" : "AI"}</span>
                            ${user && item.score != null ? `<span class="speaking-score">${item.score}%</span>` : ""}
                            <div class="speaking-message-actions">
                                ${user ? `
                                    <button class="speaking-icon-btn" type="button" data-speaking-action="pronunciation" data-speaking-index="${index}"
                                        title="${item.showPronunciation ? "Ẩn cách đọc" : "Hiện cách đọc kiểu tiếng Việt"}" aria-label="${item.showPronunciation ? "Ẩn cách đọc" : "Hiện cách đọc kiểu tiếng Việt"}">
                                        <i class="fa-solid fa-lightbulb"></i>
                                    </button>` : ""}
                                <button class="speaking-icon-btn ${recordingIndex === index ? "recording" : ""}" type="button"
                                    data-speaking-action="${user ? "mic" : "play"}" data-speaking-index="${index}"
                                    title="${user ? "Bấm để nói" : "Nghe AI đọc"}" aria-label="${user ? "Bấm để nói" : "Nghe AI đọc"}">
                                    <i class="fa-solid ${user ? "fa-microphone" : "fa-volume-high"}"></i>
                                </button>
                            </div>
                        </div>
                        <div class="speaking-text">${esc(expected)}</div>
                        ${vietnamese}
                        ${pronunciation}
                        <div class="speaking-transcript ${transcript ? "show" : ""}">${transcript}</div>
                    </div>
                </article>`;
        }).join("");

        list.querySelectorAll("[data-speaking-action]").forEach(button => {
            button.addEventListener("click", () => {
                const index = Number(button.dataset.speakingIndex);
                if (button.dataset.speakingAction === "mic") toggleRecognition(index);
                else if (button.dataset.speakingAction === "pronunciation") togglePronunciation(index);
                else playAi(index);
            });
        });
    }

    function openImportPanel() {
        $("speakingImportPanel")?.classList.add("show");
        setTimeout(() => {
            const input = $("speakingImportJson");
            input?.focus();
            if (input?.value.trim()) input.select();
        }, 40);
    }

    function closeImportPanel() {
        $("speakingImportPanel")?.classList.remove("show");
    }

    async function pasteImportJson() {
        const input = $("speakingImportJson");
        if (!input) return;
        try {
            if (!navigator.clipboard?.readText) throw new Error("clipboard unavailable");
            const text = await navigator.clipboard.readText();
            if (!text.trim()) {
                showToast("Clipboard đang trống.", "warn");
                return;
            }
            input.value = text;
            input.focus();
            input.select();
            showToast("Đã dán script từ clipboard.");
        } catch {
            input.focus();
            showToast("Không đọc được clipboard. Bấm vào ô JSON rồi Ctrl+V để dán.", "warn");
        }
    }

    function applyImport() {
        const raw = $("speakingImportJson")?.value.trim() || "";
        if (!raw) {
            showToast("Dán JSON script trước khi import.", "warn");
            return;
        }

        let parsed;
        try {
            parsed = JSON.parse(raw);
        } catch {
            showToast("JSON chưa hợp lệ.", "warn");
            return;
        }

        const source = Array.isArray(parsed) ? { messages: parsed } : parsed;
        const imported = Array.isArray(source?.messages) ? source.messages : [];
        if (!imported.length) {
            showToast("JSON cần có mảng messages.", "warn");
            return;
        }

        const target = targetInfo();
        messages = imported.map((item, index) => ({
            role: normalizeRole(item, index),
            text: firstString(item?.text, item?.message, item?.content),
            vietnamese: firstString(item?.vietnamese, item?.translation, item?.meaning),
            vietnamesePronunciation: firstString(
                item?.vietnamesePronunciation,
                item?.pronunciationVietnamese,
                item?.pronunciation,
                item?.cachDoc
            ),
            languageCode: firstString(source?.languageCode, source?.targetLanguageCode) || target.languageCode,
            voice: firstString(source?.voice, source?.aiVoice) || target.voice,
            transcript: "",
            score: null,
            showPronunciation: false
        })).filter(item => item.text);

        if (!messages.length) {
            showToast("Không tìm thấy message có nội dung.", "warn");
            return;
        }

        clearSpeakingAudio();
        $("speakingTitle").textContent = firstString(source?.title, source?.name) || "Hội thoại luyện nói";
        closeImportPanel();
        renderMessages();
        prepareAllAiAudio();
        setStatus("Đã import script. Đang tạo trước giọng đọc AI...");
        showToast("Đã import script luyện nói.");
    }

    function normalizeRole(item, index) {
        const raw = firstString(item?.role, item?.speaker, item?.side).toLowerCase();
        if (raw === "user" || raw === "right") return "user";
        if (raw === "ai" || raw === "assistant" || raw === "left") return "ai";
        return index % 2 === 0 ? "ai" : "user";
    }

    function firstString(...values) {
        for (const value of values) {
            if (typeof value === "string" && value.trim()) return value.trim();
        }
        return "";
    }

    function buildImportPrompt() {
        const target = targetInfo();
        const course = courses.find(x => x.id === ($("speakingCourseSelect")?.value || selectedSet?.id || ""));
        const difficulty = $("speakingDifficulty")?.value || "basic";
        const count = difficulty === "advanced" ? 14 : difficulty === "hard" ? 10 : 6;
        const topic = $("speakingTopicInput")?.value || course?.title || selectedSet?.title || "giao tiếp đời thường";
        const vocabulary = Array.isArray(course?.vocabulary) ? course.vocabulary.slice(0, 90) : [];

        return [
            "Hãy tạo một JSON thuần để import vào app luyện nói.",
            "Không dùng markdown, không bọc ```json.",
            "",
            `Ngôn ngữ luyện nói: ${target.languageName || "English"} (${target.languageCode || "en-US"})`,
            `Chủ đề/ngữ cảnh: ${topic}`,
            `Mức độ: ${difficulty}`,
            `Số message: đúng ${count} message.`,
            "",
            "Yêu cầu:",
            "- Tạo hội thoại tự nhiên giữa AI và User, bắt đầu bằng AI và luân phiên nghiêm ngặt.",
            "- text phải viết bằng ngôn ngữ luyện nói.",
            "- vietnamese là bản dịch tiếng Việt tự nhiên của text.",
            '- vietnamesePronunciation là cách đọc gần âm bằng chữ tiếng Việt, ví dụ "Hello" thành "hé lô"; đây không phải bản dịch.',
            "- Câu ngắn, dễ nói, phù hợp luyện phát âm.",
            "- Nếu có từ vựng học phần, dùng một số từ phù hợp một cách tự nhiên.",
            "",
            `Từ vựng học phần JSON: ${JSON.stringify(vocabulary, null, 2)}`,
            "",
            "Chỉ trả về đúng schema:",
            "{",
            '  "title": "tiêu đề ngắn",',
            `  "targetLanguageName": "${target.languageName || "English"}",`,
            `  "targetLanguageCode": "${target.languageCode || "en-US"}",`,
            '  "messages": [',
            '    { "role": "ai", "text": "Hello!", "vietnamese": "Xin chào!", "vietnamesePronunciation": "hé lô" },',
            '    { "role": "user", "text": "Hello, nice to meet you.", "vietnamese": "Xin chào, rất vui được gặp bạn.", "vietnamesePronunciation": "hé lô, nai-x tu mít diu" }',
            "  ]",
            "}"
        ].join("\n");
    }

    async function copyImportPrompt() {
        const prompt = buildImportPrompt();
        const ok = typeof copyTextToClipboard === "function"
            ? await copyTextToClipboard(prompt)
            : await copyFallback(prompt);
        showToast(ok ? "Đã chép prompt tạo script luyện nói." : "Không chép được prompt.", ok ? "ok" : "warn");
    }

    async function copyFallback(text) {
        try {
            await navigator.clipboard.writeText(text);
            return true;
        } catch {
            return false;
        }
    }

    function playAi(index) {
        const item = messages[index];
        if (!item || item.role !== "ai") return;
        stopAudio();
        const cachedUrl = audioCache.get(index);
        if (cachedUrl) {
            playAudioUrl(index, cachedUrl);
            return;
        }

        if (item.audioStatus === "loading") {
            pendingAutoPlayIndex = index;
            setStatus("Giọng AI đang được tạo, sẽ tự phát ngay khi sẵn sàng...");
            return;
        }

        item.audioStatus = "loading";
        renderMessages();
        setStatus("Đang tạo lại giọng đọc AI...");
        post("synthesizeSpeakingLine", {
            sessionId: audioSessionId,
            index,
            text: item.text,
            voice: item.voice || targetInfo().voice
        });
    }

    function toggleRecognition(index) {
        if (recordingIndex === index) {
            stopRecognition();
            return;
        }
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        if (!SpeechRecognition) {
            showToast("WebView2 không hỗ trợ nhận diện giọng nói.", "warn");
            return;
        }
        stopRecognition();
        const item = messages[index];
        recognition = new SpeechRecognition();
        recognition.lang = item.languageCode || targetInfo().languageCode || "en-US";
        recognition.interimResults = false;
        recognition.maxAlternatives = 5;
        recognition.continuous = false;
        recordingIndex = index;
        setStatus("Đang nghe... Hãy nói to và rõ.");
        renderMessages();

        recognition.onresult = event => {
            const transcript = chooseBestTranscript(event.results[0], item.text, recognition.lang);
            finishRecognition(index, transcript);
        };
        recognition.onerror = event => {
            const message = event.error === "not-allowed" || event.error === "permission-denied"
                ? "Vui lòng cho phép ứng dụng dùng microphone."
                : event.error === "no-speech"
                    ? "Không phát hiện giọng nói. Hãy thử lại."
                    : `Lỗi nhận diện: ${event.error}`;
            stopRecognition();
            setStatus(message);
        };
        recognition.onend = () => {
            if (recordingIndex === index) {
                recognition = null;
                recordingIndex = -1;
                renderMessages();
            }
        };
        try {
            recognition.start();
        } catch (error) {
            stopRecognition();
            setStatus("Không thể bật microphone.");
        }
    }

    function finishRecognition(index, transcript) {
        const item = messages[index];
        if (!item) return;
        const guided = ($("speakingPracticeMode")?.value || "guided") === "guided";
        item.transcript = transcript || "";
        item.score = guided ? Math.round(similarity(normalize(transcript, item.languageCode), normalize(item.text, item.languageCode)) * 100) : null;
        stopRecognition();
        setStatus(transcript ? "Đã nhận diện xong. Bạn có thể bấm micro để nói lại." : "Không nhận được giọng nói.");
        renderMessages();
        document.querySelector(`[data-speaking-index="${index}"]`)?.scrollIntoView({ behavior: "smooth", block: "center" });
        if (item.score != null && item.score > 60) {
            const nextAiIndex = messages.findIndex((message, messageIndex) =>
                messageIndex > index && message.role === "ai");
            if (nextAiIndex >= 0) {
                setTimeout(() => playAi(nextAiIndex), 260);
            }
        }
    }

    function stopRecognition() {
        const current = recognition;
        recognition = null;
        recordingIndex = -1;
        if (current) {
            current.onend = null;
            try { current.stop(); } catch (_) {}
        }
        renderMessages();
    }

    function chooseBestTranscript(results, target, lang) {
        if (!results?.length) return "";
        const normalizedTarget = normalize(target, lang);
        let best = "";
        let bestRank = -1;
        for (let i = 0; i < results.length; i++) {
            const value = String(results[i].transcript || "").trim();
            const rank = similarity(normalize(value, lang), normalizedTarget) * 1000 + Number(results[i].confidence || 0);
            if (rank > bestRank) {
                bestRank = rank;
                best = value;
            }
        }
        return best;
    }

    function buildComparisonHtml(expected, spoken, lang) {
        const cjk = isCjk(lang);
        const expectedTokens = tokenize(expected, lang);
        const spokenTokens = tokenize(spoken, lang);
        if (!spokenTokens.length) return "";
        return spokenTokens.map((token, i) => {
            const expectedToken = expectedTokens[i] || "";
            const ok = comparable(token, lang) === comparable(expectedToken, lang);
            return `<span class="${ok ? "speaking-word-ok" : "speaking-word-wrong"}">${esc(token)}</span>`;
        }).join(cjk ? "" : " ");
    }

    function tokenize(value, lang) {
        const cleaned = stripPunctuation(value);
        return isCjk(lang) ? [...cleaned.replace(/\s+/g, "")] : cleaned.split(/\s+/).filter(Boolean);
    }

    function comparable(value, lang) {
        return normalize(value, lang);
    }

    function normalize(value, lang) {
        const cleaned = stripPunctuation(value).toLowerCase();
        return isCjk(lang) ? cleaned.replace(/\s+/g, "") : cleaned.replace(/\s+/g, " ").trim();
    }

    function stripPunctuation(value) {
        return String(value || "").normalize("NFKC")
            .replace(/[\u200b-\u200f\ufeff]/g, "")
            .replace(/[.,!?;:'"()[\]{}<>，。！？、；：「」『』（）《》〈〉【】—…·“”‘’\-_\/\\|~`@#$%^&*+=]/g, "")
            .trim();
    }

    function isCjk(lang) {
        const value = String(lang || "").toLowerCase();
        return value.startsWith("zh") || value.startsWith("ja");
    }

    function similarity(a, b) {
        if (!a && !b) return 1;
        if (!a || !b) return 0;
        if (a === b) return 1;
        const x = [...a], y = [...b];
        const rows = Array.from({ length: x.length + 1 }, (_, i) => {
            const row = new Array(y.length + 1).fill(0);
            row[0] = i;
            return row;
        });
        for (let j = 0; j <= y.length; j++) rows[0][j] = j;
        for (let i = 1; i <= x.length; i++) {
            for (let j = 1; j <= y.length; j++) {
                rows[i][j] = x[i - 1] === y[j - 1]
                    ? rows[i - 1][j - 1]
                    : 1 + Math.min(rows[i - 1][j], rows[i][j - 1], rows[i - 1][j - 1]);
            }
        }
        return Math.max(0, 1 - rows[x.length][y.length] / Math.max(x.length, y.length));
    }

    function stopAudio() {
        if (!audio) return;
        try { audio.pause(); } catch (_) {}
        audio = null;
    }

    function clearSpeakingAudio() {
        stopAudio();
        pendingAutoPlayIndex = -1;
        audioSessionId = "";
        audioCache.forEach(url => {
            try { URL.revokeObjectURL(url); } catch (_) {}
        });
        audioCache.clear();
        messages.forEach(item => {
            if (item.role === "ai") item.audioStatus = "";
        });
        post("cancelSpeakingAudio");
    }

    function prepareAllAiAudio() {
        const aiLines = messages
            .map((item, index) => ({ item, index }))
            .filter(entry => entry.item.role === "ai" && entry.item.text);
        if (!aiLines.length) return;

        audioSessionId = window.crypto?.randomUUID
            ? window.crypto.randomUUID()
            : `${Date.now()}_${Math.floor(Math.random() * 100000)}`;
        aiLines.forEach(entry => {
            entry.item.audioStatus = "loading";
        });
        renderMessages();

        post("prepareSpeakingAudio", {
            sessionId: audioSessionId,
            voice: aiLines[0].item.voice || targetInfo().voice,
            messages: aiLines.map(entry => ({
                index: entry.index,
                text: entry.item.text
            }))
        });
    }

    function audioUrlFromBase64(base64) {
        const binary = atob(base64 || "");
        const bytes = new Uint8Array(binary.length);
        for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
        return URL.createObjectURL(new Blob([bytes], { type: "audio/mpeg" }));
    }

    function cacheAudio(index, base64) {
        const oldUrl = audioCache.get(index);
        if (oldUrl) {
            try { URL.revokeObjectURL(oldUrl); } catch (_) {}
        }
        const url = audioUrlFromBase64(base64);
        audioCache.set(index, url);
        if (messages[index]) messages[index].audioStatus = "ready";
        return url;
    }

    function playAudioUrl(index, url) {
        stopAudio();
        audio = new Audio(url);
        audio.onended = () => {
            audio = null;
            setStatus("Bấm micro ở message User để trả lời.");
        };
        audio.onerror = () => setStatus("Không phát được giọng đọc AI.");
        audio.play().catch(() => setStatus("Không phát được giọng đọc AI."));
        setStatus("AI đang đọc...");
        document.querySelector(`[data-speaking-index="${index}"]`)?.scrollIntoView({ behavior: "smooth", block: "center" });
    }

    function setBusy(value) {
        busy = !!value;
        $("btnSpeakingGenerate")?.classList.toggle("speaking-generate-busy", busy);
        if ($("btnSpeakingGenerate")) $("btnSpeakingGenerate").disabled = busy;
        const label = $("btnSpeakingGenerate")?.querySelector("span");
        if (label) label.textContent = busy ? "Đang tạo..." : "Tạo đối thoại luyện nói";
    }

    function setStatus(text) {
        if ($("speakingStatus")) $("speakingStatus").textContent = text || "";
    }

    function esc(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

    window.handleSpeakingHostMessage = function (message) {
        const msg = typeof message === "string" ? JSON.parse(message) : (message || {});
        const data = msg.data || {};
        if (msg.action === "speakingOptions") {
            courses = Array.isArray(data.courses) ? data.courses : [];
            voices = Array.isArray(data.voices) ? data.voices : [];
            selectedLanguage = data.selectedLanguage || selectedLanguage || "en";
            renderCourses(data.selectedCourseId || selectedSet?.id || "");
            renderLanguageOptions();
            renderVoiceOptions(data.defaultVoice || "");
        } else if (msg.action === "speakingBusy") {
            setBusy(!!data.busy);
            if (data.busy) setStatus("Gemini đang tạo hội thoại...");
        } else if (msg.action === "speakingPractice") {
            clearSpeakingAudio();
            $("speakingTitle").textContent = data.title || "Hội thoại luyện nói";
            messages = (Array.isArray(data.messages) ? data.messages : []).map((x, index) => ({
                role: index % 2 === 0 ? "ai" : "user",
                text: String(x.text || ""),
                vietnamese: String(x.vietnamese || ""),
                vietnamesePronunciation: String(x.vietnamesePronunciation || ""),
                languageCode: data.languageCode || targetInfo().languageCode,
                voice: data.voice || targetInfo().voice,
                transcript: "",
                score: null,
                showPronunciation: false,
                audioStatus: index % 2 === 0 ? "loading" : ""
            }));
            renderMessages();
            prepareAllAiAudio();
            setStatus("Đang tạo trước giọng đọc cho các message AI...");
        } else if (msg.action === "speakingAudioReady") {
            if (!data.sessionId || data.sessionId !== audioSessionId) return;
            const index = Number(data.index);
            const url = cacheAudio(index, data.base64 || "");
            renderMessages();
            if (pendingAutoPlayIndex === index) {
                pendingAutoPlayIndex = -1;
                playAudioUrl(index, url);
            }
        } else if (msg.action === "speakingAudioFailed") {
            if (!data.sessionId || data.sessionId !== audioSessionId) return;
            const index = Number(data.index);
            if (messages[index]) messages[index].audioStatus = "failed";
            if (pendingAutoPlayIndex === index) pendingAutoPlayIndex = -1;
            renderMessages();
        } else if (msg.action === "speakingAudioBatchDone") {
            if (!data.sessionId || data.sessionId !== audioSessionId) return;
            setStatus("Đã tạo xong giọng đọc AI. Bấm micro để bắt đầu luyện.");
        } else if (msg.action === "speakingAudio") {
            if (data.sessionId && data.sessionId !== audioSessionId) return;
            const index = Number(data.index);
            const url = cacheAudio(index, data.base64 || "");
            renderMessages();
            playAudioUrl(index, url);
        } else if (msg.action === "speakingToast") {
            showToast(data.text || "Có lỗi xảy ra.", data.type || "warn");
            setStatus(data.text || "");
        }
    };

    document.addEventListener("DOMContentLoaded", setup);
})();
