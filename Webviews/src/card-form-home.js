(() => {
    const ss = document.getElementById("sortSelect");
    if (ss) {
        ss.innerHTML =
            '<option value="default">Mặc định</option>' +
            '<option value="title_asc">Tên A → Z</option>' +
            '<option value="title_desc">Tên Z → A</option>' +
            '<option value="count_asc">Số thẻ tăng dần</option>' +
            '<option value="count_desc">Số thẻ giảm dần</option>';
    }
})();

(() => {
    let curX = 0,
        curY = 0,
        tgX = 0,
        tgY = 0;

    const interBubble = document.querySelector(".interactive");
    let __lastBg = 0;

    function move(now) {
        if (now - __lastBg < 33) {
            requestAnimationFrame(move);
            return;
        }

        __lastBg = now;
        curX += (tgX - curX) / 20;
        curY += (tgY - curY) / 20;

        if (interBubble) {
            interBubble.style.transform =
                "translate(" +
                Math.round(curX) +
                "px," +
                Math.round(curY) +
                "px)";
        }

        requestAnimationFrame(move);
    }

    window.addEventListener("mousemove", e => {
        tgX = e.clientX;
        tgY = e.clientY;
    });

    requestAnimationFrame(move);
})();

document.addEventListener("DOMContentLoaded", () => {
    setupEventListeners();
    setupWritingPractice();
    restoreHomeBackground();
    notifyReady();
    setDark(true);
});

function restoreHomeBackground() {
    localStorage.setItem("tocfl.homeBackground", "image");
    applyHomeBackground(true);
}

function applyHomeBackground(useImage) {
    document.body.classList.toggle("image-background", !!useImage);

    const btn = document.getElementById("btnBackground");
    if (btn) btn.textContent = useImage ? "Nền Ảnh" : "Nền Động";
}

window.toggleHomeBackground = function () {
    const next = !document.body.classList.contains("image-background");
    localStorage.setItem("tocfl.homeBackground", next ? "image" : "animated");
    applyHomeBackground(next);
};

window.showDialogueStub = function () {
    closeWritingPractice();
    sendToBackend("showDialogue");
};

window.openAppSettings = function () {
    const overlay = document.getElementById("appSettingsOverlay");
    if (overlay) overlay.classList.add("show");
    sendToBackend("getGeminiSettings");
};

window.closeAppSettings = function () {
    const overlay = document.getElementById("appSettingsOverlay");
    if (overlay) overlay.classList.remove("show");
};

window.applyGeminiSettings = function (settings) {
    const apiKey = document.getElementById("geminiApiKey");
    const model = document.getElementById("geminiModel");
    const pixabayApiKey = document.getElementById("pixabayApiKey");

    if (apiKey) apiKey.value = settings && settings.apiKey ? settings.apiKey : "";
    if (model) model.value = settings && settings.model ? settings.model : "gemini-flash-lite-latest";
    if (pixabayApiKey) pixabayApiKey.value = settings && settings.pixabayApiKey ? settings.pixabayApiKey : "";
};

window.saveAppSettings = function () {
    const apiKey = document.getElementById("geminiApiKey");
    const model = document.getElementById("geminiModel");
    const pixabayApiKey = document.getElementById("pixabayApiKey");

    sendToBackend("saveGeminiSettings", {
        apiKey: apiKey ? apiKey.value : "",
        model: model ? model.value : "gemini-flash-lite-latest",
        pixabayApiKey: pixabayApiKey ? pixabayApiKey.value : ""
    });

    closeAppSettings();
    showToast("Đã lưu cài đặt Gemini.");
};

window.openExternalKeyPage = function (url) {
    sendToBackend("openExternalUrl", { url });
};

let writingVoices = [];
let writingCourses = [];
let writingMode = "course";
let writingCourseFilter = "";
let writingSelectedLanguage = "";
let writingScript = null;
let writingBusy = false;
let writingLastIssues = [];

const writingDifficultyOptions = {
    basic: {
        value: "basic",
        label: "Cơ bản",
        sentenceCount: 3,
        instruction: "câu ngắn, cấu trúc quen thuộc, ít mệnh đề phụ, phù hợp để luyện nền tảng"
    },
    hard: {
        value: "hard",
        label: "Khó",
        sentenceCount: 5,
        instruction: "câu tự nhiên hơn, có nối ý, dùng thêm cụm từ và cấu trúc trung cấp"
    },
    advanced: {
        value: "advanced",
        label: "Nâng cao",
        sentenceCount: 7,
        instruction: "đoạn dài hơn, liên kết ý rõ, dùng cấu trúc phong phú nhưng vẫn đời thường"
    }
};

function setupWritingPractice() {
    const btnGenerate = document.getElementById("btnWritingGenerate");
    const btnImport = document.getElementById("btnWritingImport");
    const btnCopyPrompt = document.getElementById("btnWritingCopyPrompt");
    const btnApplyImport = document.getElementById("btnWritingApplyImport");
    const btnCancelImport = document.getElementById("btnWritingCancelImport");
    const btnBack = document.getElementById("btnWritingBack");
    const btnHint = document.getElementById("btnWritingHint");
    const btnGrade = document.getElementById("btnWritingGrade");
    const btnClear = document.getElementById("btnWritingClear");
    const modeCourse = document.getElementById("writingModeCourse");
    const modeTopic = document.getElementById("writingModeTopic");
    const courseSearch = document.getElementById("writingCourseSearch");
    const courseSelect = document.getElementById("writingCourseSelect");
    const voiceSource = document.getElementById("writingVoiceSource");
    const language = document.getElementById("writingLanguage");
    const leftVoice = document.getElementById("writingLeftVoice");
    const rightVoice = document.getElementById("writingRightVoice");
    const userText = document.getElementById("writingUserText");

    if (btnGenerate) btnGenerate.addEventListener("click", generateWritingPractice);
    if (btnImport) btnImport.addEventListener("click", openWritingImportPanel);
    if (btnCopyPrompt) btnCopyPrompt.addEventListener("click", copyWritingImportPrompt);
    if (btnApplyImport) btnApplyImport.addEventListener("click", applyWritingImport);
    if (btnCancelImport) btnCancelImport.addEventListener("click", closeWritingImportPanel);
    if (btnBack) btnBack.addEventListener("click", closeWritingPractice);
    if (btnHint) btnHint.addEventListener("click", requestWritingHint);
    if (btnGrade) btnGrade.addEventListener("click", gradeWritingPractice);
    if (btnClear) btnClear.addEventListener("click", clearWritingAnswer);
    if (modeCourse) modeCourse.addEventListener("click", () => setWritingMode("course"));
    if (modeTopic) modeTopic.addEventListener("click", () => setWritingMode("topic"));
    if (courseSearch) {
        courseSearch.addEventListener("input", e => {
            writingCourseFilter = e.target.value || "";
            renderWritingCourses(document.getElementById("writingCourseSelect")?.value || selectedSet?.id || "");
        });
    }
    if (courseSelect) {
        courseSelect.addEventListener("change", () => {
            writingSelectedLanguage = selectedSetLanguageKey();
            renderWritingLanguageOptions(writingSelectedLanguage);
            renderWritingVoiceOptions();
            updateWritingAnswerLabel();
        });
    }
    if (voiceSource) voiceSource.addEventListener("change", updateWritingVoiceMode);
    if (language) language.addEventListener("change", () => {
        writingSelectedLanguage = language.value || writingSelectedLanguage;
        renderWritingVoiceOptions();
        updateWritingAnswerLabel();
    });
    if (leftVoice) leftVoice.addEventListener("change", updateWritingAnswerLabel);
    if (rightVoice) rightVoice.addEventListener("change", updateWritingAnswerLabel);
    if (userText) {
        userText.addEventListener("input", () => {
            clearWritingUserHighlight();
            if (writingLastIssues.length && document.getElementById("writingResult")?.classList.contains("show")) {
                renderWritingMarkedUserText(userText.value, writingLastIssues);
            }
        });
    }

    document.addEventListener("keydown", e => {
        if (e.key === "Escape" && document.body.classList.contains("writing-mode")) closeWritingPractice();
    });

    setWritingMode("course");
    updateWritingVoiceMode();
}

window.openWritingPractice = function () {
    const screen = document.getElementById("writingScreen");
    document.body.classList.add("writing-mode");
    if (screen) {
        screen.classList.add("show");
        screen.setAttribute("aria-hidden", "false");
    }

    sendToBackend("getWritingOptions");
    renderWritingCourses(selectedSet?.id || "");
    updateWritingAnswerLabel();

    setTimeout(() => {
        const target = writingMode === "topic"
            ? document.getElementById("writingTopicInput")
            : document.getElementById("writingCourseSearch");
        if (target) target.focus();
    }, 40);
};

window.closeWritingPractice = function () {
    if (writingBusy) return;
    const screen = document.getElementById("writingScreen");
    document.body.classList.remove("writing-mode");
    if (screen) {
        screen.classList.remove("show");
        screen.setAttribute("aria-hidden", "true");
    }
    sendToBackend("refreshSelectedCourseCover", {});
};

function setWritingMode(next) {
    writingMode = next === "topic" ? "topic" : "course";
    document.getElementById("writingModeCourse")?.classList.toggle("active", writingMode === "course");
    document.getElementById("writingModeTopic")?.classList.toggle("active", writingMode === "topic");
    document.querySelector(".writing-modal")?.classList.toggle("topic-mode", writingMode === "topic");
}

function updateWritingVoiceMode() {
    const custom = document.getElementById("writingVoiceSource")?.value === "custom";
    document.querySelector(".writing-modal")?.classList.toggle("custom-voice", custom);
    if (custom) {
        renderWritingLanguageOptions(writingSelectedLanguage);
        renderWritingVoiceOptions();
    }
    updateWritingAnswerLabel();
}

function renderWritingCourses(selectedCourseId = "") {
    const select = document.getElementById("writingCourseSelect");
    if (!select) return;

    const source = writingCourses.length ? writingCourses : allCourses;
    const q = writingCourseFilter.trim().toLowerCase();
    const filtered = q
        ? source.filter(c => String(c.title || "").toLowerCase().includes(q))
        : source;

    const list = filtered.length
        ? filtered
        : [{ id: "", title: "Chưa có học phần", count: 0 }];

    select.innerHTML = list.map(c =>
        `<option value="${escapeWritingHtml(c.id)}">${escapeWritingHtml(c.title)} (${Number(c.count || 0)})</option>`
    ).join("");

    const preferred = selectedCourseId || selectedSet?.id || "";
    if (preferred && list.some(c => c.id === preferred)) {
        select.value = preferred;
    }
}

function renderWritingLanguageOptions(preferredKey = "") {
    const language = document.getElementById("writingLanguage");
    if (!language) return;

    const map = new Map();
    writingVoices.forEach(v => {
        const key = v.languageKey || String(v.languageCode || "").split("-")[0] || v.voice;
        if (!map.has(key)) map.set(key, v.languageName || key);
    });

    const current = preferredKey || writingSelectedLanguage || language.value || selectedSetLanguageKey() || "en";
    language.innerHTML = Array.from(map.entries()).map(([key, label]) =>
        `<option value="${escapeWritingHtml(key)}">${escapeWritingHtml(label)}</option>`
    ).join("");

    language.value = map.has(current) ? current : (map.keys().next().value || "en");
    writingSelectedLanguage = language.value || writingSelectedLanguage;
}

function renderWritingVoiceOptions() {
    const lang = document.getElementById("writingLanguage")?.value || writingSelectedLanguage || selectedSetLanguageKey();
    const source = writingVoices.filter(v =>
        (v.languageKey || String(v.languageCode || "").split("-")[0]) === lang
    );
    const list = source.length ? source : writingVoices;
    const html = list.map(v =>
        `<option value="${escapeWritingHtml(v.voice)}">${escapeWritingHtml(v.label || v.voice)}</option>`
    ).join("");

    const left = document.getElementById("writingLeftVoice");
    const right = document.getElementById("writingRightVoice");
    if (!left || !right) return;

    const previousLeft = left.value;
    const previousRight = right.value;
    left.innerHTML = html;
    right.innerHTML = html;

    left.value = list.some(v => v.voice === previousLeft)
        ? previousLeft
        : (list.find(v => String(v.gender || "").toLowerCase() === "female")?.voice || list[0]?.voice || "");
    right.value = list.some(v => v.voice === previousRight)
        ? previousRight
        : (list.find(v => String(v.gender || "").toLowerCase() === "male")?.voice || list[1]?.voice || list[0]?.voice || "");
}

function selectedSetLanguageKey() {
    const courseId = document.getElementById("writingCourseSelect")?.value || selectedSet?.id || "";
    const source = writingCourses.length ? writingCourses : allCourses;
    const course = source.find(c => c.id === courseId) || source.find(c => c.id === selectedSet?.id);
    const code = String(course?.languageCode || "").trim();
    if (code) return code.split("-")[0].toLowerCase();
    return writingSelectedLanguage || "en";
}

function writingLanguageInfo(key) {
    return writingVoices.find(v => (v.languageKey || String(v.languageCode || "").split("-")[0]) === key) || null;
}

function getWritingTargetInfo() {
    const useCustom = document.getElementById("writingVoiceSource")?.value === "custom";
    const courseId = document.getElementById("writingCourseSelect")?.value || selectedSet?.id || "";
    const source = writingCourses.length ? writingCourses : allCourses;
    const course = source.find(c => c.id === courseId) || source.find(c => c.id === selectedSet?.id);

    if (useCustom) {
        const langKey = document.getElementById("writingLanguage")?.value || writingSelectedLanguage || "en";
        const info = writingLanguageInfo(langKey);
        return {
            languageKey: langKey,
            languageName: info?.languageName || langKey,
            languageCode: info?.languageCode || langKey
        };
    }

    const code = String(course?.languageCode || "").trim();
    const key = code ? code.split("-")[0].toLowerCase() : selectedSetLanguageKey();
    const info = writingLanguageInfo(key);
    return {
        languageKey: key,
        languageName: course?.language || info?.languageName || key || "English",
        languageCode: code || info?.languageCode || key || "en"
    };
}

function getWritingDifficultyInfo() {
    const raw = document.getElementById("writingDifficulty")?.value || "basic";
    return writingDifficultyOptions[raw] || writingDifficultyOptions.basic;
}

function getWritingSelectedCourse() {
    const courseId = document.getElementById("writingCourseSelect")?.value || selectedSet?.id || "";
    const source = writingCourses.length ? writingCourses : allCourses;
    return source.find(c => c.id === courseId) || source.find(c => c.id === selectedSet?.id) || null;
}

function getWritingSelectedVocabulary() {
    const course = getWritingSelectedCourse();
    const vocab = Array.isArray(course?.vocabulary) ? course.vocabulary : [];
    return vocab
        .map(item => ({
            term: String(item?.term || item?.Term || "").trim(),
            meaning: String(item?.meaning || item?.definition || item?.Definition || "").trim(),
            pinyin: String(item?.pinyin || item?.Pinyin || "").trim()
        }))
        .filter(item => item.term);
}

function generateWritingPractice() {
    if (writingBusy) return;

    const target = getWritingTargetInfo();
    const courseId = document.getElementById("writingCourseSelect")?.value || selectedSet?.id || "";
    const topic = document.getElementById("writingTopicInput")?.value || "";
    const difficulty = getWritingDifficultyInfo();
    const sentenceCount = difficulty.sentenceCount;

    if (writingMode === "course" && !courseId) {
        showToast("Bạn chưa chọn học phần.");
        return;
    }

    if (writingMode === "topic" && !topic.trim()) {
        showToast("Nhập chủ đề trước khi tạo đoạn viết.");
        return;
    }

    writingScript = null;
    clearWritingResult();
    const vi = document.getElementById("writingVietnameseText");
    const user = document.getElementById("writingUserText");
    if (vi) vi.value = "";
    if (user) user.value = "";
    clearWritingUserHighlight();

    sendToBackend("generateWritingPractice", {
        mode: writingMode,
        courseId,
        topic,
        sentenceCount,
        difficulty: difficulty.value,
        difficultyLabel: difficulty.label,
        targetLanguageKey: target.languageKey,
        targetLanguageName: target.languageName,
        targetLanguageCode: target.languageCode
    });
}

function openWritingImportPanel() {
    const panel = document.getElementById("writingImportPanel");
    if (panel) panel.classList.add("show");

    const input = document.getElementById("writingImportJson");
    if (input) {
        if (!input.value.trim()) input.value = buildWritingImportExample();
        setTimeout(() => input.focus(), 40);
    }
}

function closeWritingImportPanel() {
    const panel = document.getElementById("writingImportPanel");
    if (panel) panel.classList.remove("show");
}

function applyWritingImport() {
    const input = document.getElementById("writingImportJson");
    const raw = input ? input.value.trim() : "";
    if (!raw) {
        showToast("Dán JSON trước khi import.");
        return;
    }

    let parsed;
    try {
        parsed = JSON.parse(raw);
    } catch {
        showToast("JSON chưa hợp lệ.");
        return;
    }

    const item = normalizeWritingImport(parsed);
    if (!item.vietnameseText || !item.targetText) {
        showToast("JSON cần có vietnameseText và targetText.");
        return;
    }

    writingScript = item;
    const vi = document.getElementById("writingVietnameseText");
    const user = document.getElementById("writingUserText");
    if (vi) vi.value = item.vietnameseText;
    if (user) {
        user.value = "";
        user.focus();
    }

    clearWritingResult();
    clearWritingUserHighlight();
    closeWritingImportPanel();
    updateWritingAnswerLabel();
    showToast("Đã import đoạn viết JSON.");
}

function normalizeWritingImport(value) {
    const source = Array.isArray(value) ? value[0] : value;
    const target = getWritingTargetInfo();
    const vietnameseText = firstWritingString(
        source?.vietnameseText,
        source?.vietnamese,
        source?.vi,
        source?.meaningVietnamese,
        source?.nghiaTiengViet,
        source?.sourceVietnamese
    );
    const targetText = firstWritingString(
        source?.targetText,
        source?.correctText,
        source?.expectedText,
        source?.answer,
        source?.target,
        source?.foreignText,
        source?.translatedText
    );

    return {
        title: firstWritingString(source?.title, source?.name) || "Imported writing",
        topic: firstWritingString(source?.topic, source?.context) || "Imported writing",
        vietnameseText,
        targetText,
        targetLanguageName: firstWritingString(source?.targetLanguageName, source?.languageName, source?.language) || target.languageName,
        targetLanguageCode: firstWritingString(source?.targetLanguageCode, source?.languageCode, source?.code) || target.languageCode,
        usedVocabulary: Array.isArray(source?.usedVocabulary) ? source.usedVocabulary : [],
        contextNote: firstWritingString(source?.contextNote, source?.note)
    };
}

function firstWritingString(...values) {
    for (const value of values) {
        if (typeof value === "string" && value.trim()) return value.trim();
    }
    return "";
}

function buildWritingImportExample() {
    const target = getWritingTargetInfo();
    return JSON.stringify({
        title: "Work delay notice",
        topic: document.getElementById("writingTopicInput")?.value || selectedSet?.title || "daily communication",
        vietnameseText: "Sáng nay tôi đến muộn vì trời mưa lớn. Tôi đã nhắn cho đồng nghiệp trước khi vào họp. Sau đó tôi gửi lại phần ghi chú để mọi người nắm nội dung chính.",
        targetText: "This morning I was late because it was raining heavily. I messaged my coworker before joining the meeting. After that, I sent the notes again so everyone could understand the main points.",
        targetLanguageName: target.languageName || "English",
        targetLanguageCode: target.languageCode || "en-US",
        usedVocabulary: [],
        contextNote: "Đoạn ngắn, thực tế, dùng để luyện viết theo tình huống giao tiếp."
    }, null, 2);
}

function buildWritingImportPrompt() {
    const target = getWritingTargetInfo();
    const courseId = document.getElementById("writingCourseSelect")?.value || selectedSet?.id || "";
    const source = writingCourses.length ? writingCourses : allCourses;
    const course = source.find(c => c.id === courseId) || source.find(c => c.id === selectedSet?.id);
    const topic = document.getElementById("writingTopicInput")?.value || course?.title || selectedSet?.title || "giao tiếp đời thường";
    const sentenceCount = getWritingDifficultyInfo().sentenceCount;

    return [
        "Hãy tạo một JSON thuần để import vào app luyện viết.",
        "Không dùng markdown, không bọc ```json.",
        "",
        `Ngôn ngữ đích: ${target.languageName || "English"} (${target.languageCode || "en-US"})`,
        `Chủ đề/ngữ cảnh: ${topic}`,
        `Số câu tiếng Việt: khoảng ${sentenceCount} câu.`,
        "",
        "Yêu cầu nội dung:",
        "- Tạo một đoạn tiếng Việt ngắn, thực tế, tự nhiên, không văn phong AI.",
        "- Tạo targetText là bản viết đúng bằng ngôn ngữ đích, cùng ý với đoạn tiếng Việt.",
        "- Dùng từ vựng đời thường, hợp giao tiếp/công việc/học tập/thời sự nhẹ.",
        "- Không dùng từ quá cao siêu, không viết quá dài.",
        "",
        "Chỉ trả về đúng schema JSON này:",
        "{",
        '  "title": "tiêu đề ngắn",',
        '  "topic": "chủ đề",',
        '  "vietnameseText": "đoạn tiếng Việt để người học nhìn và viết lại",',
        '  "targetText": "bản đúng bằng ngôn ngữ đích để app dùng chấm",',
        `  "targetLanguageName": "${target.languageName || "English"}",`,
        `  "targetLanguageCode": "${target.languageCode || "en-US"}",`,
        '  "usedVocabulary": ["từ/cụm đã dùng nếu có"],',
        '  "contextNote": "ghi chú ngắn bằng tiếng Việt"',
        "}"
    ].join("\n");
}

function buildWritingImportPromptWithVocabulary() {
    const target = getWritingTargetInfo();
    const course = getWritingSelectedCourse();
    const difficulty = getWritingDifficultyInfo();
    const topic = document.getElementById("writingTopicInput")?.value || course?.title || selectedSet?.title || "giao tiếp đời thường";
    const vocabulary = getWritingSelectedVocabulary();
    const vocabularyJson = JSON.stringify(vocabulary, null, 2);

    return [
        "Hãy tạo một JSON thuần để import vào app luyện viết.",
        "Không dùng markdown, không bọc ```json.",
        "",
        `Ngôn ngữ đích: ${target.languageName || "English"} (${target.languageCode || "en-US"})`,
        `Chủ đề/ngữ cảnh: ${topic}`,
        `Mức độ: ${difficulty.label}`,
        `Số câu tiếng Việt: khoảng ${difficulty.sentenceCount} câu.`,
        `Yêu cầu mức độ: ${difficulty.instruction}.`,
        "",
        `Học phần hiện tại: ${course?.title || "không có"}`,
        "Danh sách từ vựng học phần hiện tại JSON:",
        vocabularyJson || "[]",
        "",
        "Yêu cầu nội dung:",
        "- Tạo một đoạn tiếng Việt ngắn, thực tế, tự nhiên, không văn phong AI.",
        "- Tạo targetText là bản viết đúng bằng ngôn ngữ đích, cùng ý với đoạn tiếng Việt.",
        "- Nếu có danh sách từ vựng, dùng một số từ trong danh sách một cách tự nhiên, không nhồi từ.",
        "- Độ khó phải bám đúng mức độ đã chọn.",
        "- Không dùng từ quá cao siêu, không viết quá dài.",
        "",
        "Chỉ trả về đúng schema JSON này:",
        "{",
        '  "title": "tiêu đề ngắn",',
        '  "topic": "chủ đề",',
        `  "difficulty": "${difficulty.label}",`,
        '  "vietnameseText": "đoạn tiếng Việt để người học nhìn và viết lại",',
        '  "targetText": "bản đúng bằng ngôn ngữ đích để app dùng chấm",',
        `  "targetLanguageName": "${target.languageName || "English"}",`,
        `  "targetLanguageCode": "${target.languageCode || "en-US"}",`,
        '  "usedVocabulary": ["từ/cụm đã dùng nếu có"],',
        '  "contextNote": "ghi chú ngắn bằng tiếng Việt"',
        "}"
    ].join("\n");
}

async function copyWritingImportPrompt() {
    const prompt = buildWritingImportPromptWithVocabulary();
    const ok = await copyTextToClipboard(prompt);
    showToast(ok ? "Đã chép prompt import JSON." : "Không chép được prompt.");
}

async function copyTextToClipboard(text) {
    try {
        if (navigator.clipboard && window.isSecureContext) {
            await navigator.clipboard.writeText(text);
            return true;
        }
    } catch { }

    try {
        const ta = document.createElement("textarea");
        ta.value = text;
        ta.setAttribute("readonly", "");
        ta.style.position = "fixed";
        ta.style.left = "-9999px";
        document.body.appendChild(ta);
        ta.select();
        const ok = document.execCommand("copy");
        document.body.removeChild(ta);
        return ok;
    } catch {
        return false;
    }
}

function requestWritingHint() {
    if (writingBusy) return;
    if (!writingScript) {
        showToast("Hãy tạo đoạn viết trước khi xem gợi ý.");
        return;
    }

    sendToBackend("hintWritingPractice", {
        courseId: document.getElementById("writingCourseSelect")?.value || selectedSet?.id || "",
        topic: writingScript.topic || "",
        vietnameseText: writingScript.vietnameseText || "",
        targetLanguageName: writingScript.targetLanguageName || "",
        targetLanguageCode: writingScript.targetLanguageCode || "",
        difficulty: writingScript.difficulty || getWritingDifficultyInfo().label,
        usedVocabulary: Array.isArray(writingScript.usedVocabulary) ? writingScript.usedVocabulary : [],
        contextNote: writingScript.contextNote || ""
    });
}

function gradeWritingPractice() {
    if (writingBusy) return;
    if (!writingScript) {
        showToast("Hãy tạo đoạn viết trước khi chấm.");
        return;
    }

    const userText = document.getElementById("writingUserText")?.value || "";
    if (!userText.trim()) {
        showToast("Nhập bài viết của bạn trước khi gửi chấm.");
        return;
    }

    sendToBackend("gradeWritingPractice", {
        courseId: document.getElementById("writingCourseSelect")?.value || selectedSet?.id || "",
        topic: writingScript.topic || "",
        vietnameseText: writingScript.vietnameseText || "",
        expectedText: writingScript.targetText || "",
        userText,
        targetLanguageName: writingScript.targetLanguageName || "",
        targetLanguageCode: writingScript.targetLanguageCode || ""
    });
}

function clearWritingAnswer() {
    const user = document.getElementById("writingUserText");
    if (user) user.value = "";
    clearWritingResult();
    clearWritingUserHighlight();
}

function clearWritingResult() {
    ["writingScore", "writingHighlight", "writingNotes", "writingSuggestion", "writingHint"].forEach(id => {
        const el = document.getElementById(id);
        if (el) el.innerHTML = "";
    });
    document.getElementById("writingHint")?.classList.remove("show");
    writingLastIssues = [];
    clearWritingUserHighlight();
    document.getElementById("writingResult")?.classList.remove("show");
}

function setWritingBusy(busy, phase) {
    writingBusy = !!busy;
    document.querySelector(".writing-modal")?.classList.toggle("generating", writingBusy);

    const gen = document.getElementById("btnWritingGenerate");
    const hint = document.getElementById("btnWritingHint");
    const grade = document.getElementById("btnWritingGrade");
    if (gen) {
        gen.disabled = writingBusy;
        gen.classList.toggle("loading", writingBusy && phase === "generate");
        const label = gen.querySelector("span");
        if (label) label.textContent = writingBusy && phase === "generate" ? "Đang tạo..." : "Tạo đoạn viết";
    }
    if (grade) {
        grade.disabled = writingBusy;
        grade.textContent = writingBusy && phase === "grade" ? "Đang chấm..." : "Gửi chấm";
    }
    if (hint) {
        hint.disabled = writingBusy;
        hint.textContent = writingBusy && phase === "hint" ? "Đang gợi ý..." : "Gợi ý";
    }
}

function updateWritingAnswerLabel() {
    const info = getWritingTargetInfo();
    const label = document.getElementById("writingAnswerLabel");
    const name = info.languageName || "ngôn ngữ đã chọn";
    const code = info.languageCode && info.languageCode !== name ? ` - ${info.languageCode}` : "";
    if (label) label.textContent = `Bài viết của bạn (${name}${code})`;
}

function renderWritingGrade(result) {
    const userText = document.getElementById("writingUserText")?.value || "";
    const issues = Array.isArray(result?.issues) ? result.issues : [];
    const score = document.getElementById("writingScore");
    const highlight = document.getElementById("writingHighlight");
    const notes = document.getElementById("writingNotes");
    const suggestion = document.getElementById("writingSuggestion");
    const wrap = document.getElementById("writingResult");

    if (score) {
        score.innerHTML =
            `<span>${Number(result?.score || 0)}/100</span>` +
            `<p>${escapeWritingHtml(result?.overallFeedback || "Gemini đã chấm xong.")}</p>`;
    }

    writingLastIssues = issues;
    clearWritingUserHighlight();
    renderWritingMarkedUserText(userText, issues);

    if (notes) {
        if (!issues.length) {
            notes.innerHTML = `<div class="writing-note ok">Chưa thấy lỗi lớn. Bạn có thể so lại gợi ý bên dưới để viết tự nhiên hơn.</div>`;
        } else {
            notes.innerHTML = issues.map(item => `
                <div class="writing-note">
                    <div class="writing-note-main">
                        <span>${escapeWritingHtml(item.wrongText || "Thiếu ý")}</span>
                        <b>${escapeWritingHtml(item.correction || "")}</b>
                    </div>
                    <p>${escapeWritingHtml(item.explanation || "")}</p>
                </div>
            `).join("");
        }
    }

    if (suggestion) {
        const rewrite = result?.suggestedRewrite || writingScript?.targetText || "";
        suggestion.innerHTML =
            `<div class="writing-result-title">Gợi ý nên viết</div>` +
            `<div class="writing-suggested-text">${escapeWritingHtml(rewrite)}</div>`;
    }

    if (wrap) wrap.classList.add("show");
}

function renderWritingUserHighlight(text, issues) {
    clearWritingUserHighlight();
    renderWritingMarkedUserText(text, issues);
}

function renderWritingMarkedUserText(text, issues) {
    const highlight = document.getElementById("writingHighlight");
    if (!highlight) return;

    const source = String(text || "").trim();
    if (!source) {
        highlight.innerHTML = "";
        return;
    }

    highlight.innerHTML =
        `<div class="writing-result-title">Bài bạn đã viết</div>` +
        `<div class="writing-marked-text">${buildWritingHighlight(source, issues)}</div>`;
}

function renderWritingHint(data) {
    const panel = document.getElementById("writingHint");
    if (!panel) return;

    const sections = [
        { title: "Ý chính nên bám", items: data?.keyIdeas },
        { title: "Cách dùng từ", items: data?.wordHints },
        { title: "Cấu trúc gợi ý", items: data?.structureHints }
    ].filter(section => Array.isArray(section.items) && section.items.length);

    if (!sections.length) {
        panel.innerHTML = `<div class="writing-result-title">Gợi ý</div><div class="writing-note ok">Chưa có gợi ý phù hợp cho đoạn này.</div>`;
    } else {
        panel.innerHTML = sections.map(section => `
            <div class="writing-hint-section">
                <div class="writing-result-title">${escapeWritingHtml(section.title)}</div>
                <ul>${section.items.map(item => `<li>${escapeWritingHtml(item)}</li>`).join("")}</ul>
            </div>
        `).join("");
    }

    panel.classList.add("show");
}

function renderWritingUserHighlightLayer(text, issues) {
    const shell = document.getElementById("writingEditorShell");
    const layer = document.getElementById("writingUserHighlight");
    if (!shell || !layer) return;

    const source = String(text || "");
    if (!source || !issues.length) {
        clearWritingUserHighlight();
        return;
    }

    shell.classList.add("graded");
    layer.innerHTML = buildWritingHighlight(source, issues);
    syncWritingHighlightScroll();
}

function clearWritingUserHighlight() {
    const shell = document.getElementById("writingEditorShell");
    const layer = document.getElementById("writingUserHighlight");
    if (shell) shell.classList.remove("graded");
    if (layer) layer.innerHTML = "";
}

function syncWritingHighlightScroll() {
    const textarea = document.getElementById("writingUserText");
    const layer = document.getElementById("writingUserHighlight");
    if (!textarea || !layer) return;

    layer.scrollTop = textarea.scrollTop;
    layer.scrollLeft = textarea.scrollLeft;
}

function buildWritingHighlight(text, issues) {
    const source = String(text || "");
    if (!source) return "";

    const ranges = [];
    const lower = source.toLocaleLowerCase();

    (issues || []).forEach(issue => {
        const needle = String(issue?.wrongText || "").trim();
        if (!needle) return;

        const lowerNeedle = needle.toLocaleLowerCase();
        let pos = lower.indexOf(lowerNeedle);
        while (pos >= 0) {
            const end = pos + needle.length;
            const overlaps = ranges.some(r => pos < r.end && end > r.start);
            if (!overlaps) {
                ranges.push({ start: pos, end });
                break;
            }
            pos = lower.indexOf(lowerNeedle, pos + 1);
        }
    });

    if (!ranges.length) return escapeWritingHtml(source);

    ranges.sort((a, b) => a.start - b.start);
    let html = "";
    let cursor = 0;
    ranges.forEach(r => {
        if (r.start < cursor) return;
        html += escapeWritingHtml(source.slice(cursor, r.start));
        html += `<mark class="writing-error">${escapeWritingHtml(source.slice(r.start, r.end))}</mark>`;
        cursor = r.end;
    });
    html += escapeWritingHtml(source.slice(cursor));
    return html;
}

function escapeWritingHtml(value) {
    return String(value || "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

window.handleWritingHostMessage = function (message) {
    const msg = message || {};
    const data = msg.data || {};

    if (msg.action === "writingOptions") {
        writingCourses = Array.isArray(data.courses) ? data.courses : [];
        writingVoices = Array.isArray(data.voices) ? data.voices : [];
        writingSelectedLanguage = data.selectedLanguage || writingSelectedLanguage || selectedSetLanguageKey();
        renderWritingCourses(data.selectedCourseId || selectedSet?.id || "");
        renderWritingLanguageOptions(writingSelectedLanguage);
        renderWritingVoiceOptions();
        updateWritingAnswerLabel();
        return;
    }

    if (msg.action === "writingBusy") {
        setWritingBusy(!!data.busy, data.phase || "");
        return;
    }

    if (msg.action === "writingPractice") {
        writingScript = data;
        const vi = document.getElementById("writingVietnameseText");
        const user = document.getElementById("writingUserText");
        if (vi) vi.value = data.vietnameseText || "";
        if (user) {
            user.value = "";
            user.focus();
        }
        clearWritingResult();
        clearWritingUserHighlight();
        updateWritingAnswerLabel();
        showToast("Đã tạo đoạn viết.");
        return;
    }

    if (msg.action === "writingGrade") {
        renderWritingGrade(data);
        return;
    }

    if (msg.action === "writingHint") {
        renderWritingHint(data);
        return;
    }

    if (msg.action === "writingToast") {
        showToast(data.text || "Không xử lý được yêu cầu.", data.type || "");
    }
};
