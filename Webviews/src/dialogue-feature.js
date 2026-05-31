const $ = id => document.getElementById(id);

let voices = [];
let courses = [];
let dialogueItems = [];
let mode = "course";
let activeId = "";
let playing = false;
let audioReady = false;
let geminiBusy = false;
let editMode = false;
let editBaseProject = null;
let savingEdit = false;
let selectedLanguage = "";
let courseFilter = "";
let dialogueFilter = "";
let toastTimer = null;

let project = {
  id: newProjectId("dialogue"),
  title: "Travel practice",
  languageKey: "",
  languageName: "",
  languageCode: "",
  leftVoice: "",
  rightVoice: "",
  createdAt: new Date().toISOString(),
  updatedAt: "",
  messages: []
};

function post(action, data = {}) {
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.postMessage(JSON.stringify({ action, data }));
  }
}

function newProjectId(title) {
  const safe = String(title || "dialogue")
    .trim()
    .replace(/[^\p{L}\p{N}]+/gu, "_")
    .replace(/^_+|_+$/g, "") || "dialogue";
  const stamp = new Date().toISOString().replace(/[-:TZ.]/g, "").slice(0, 14);
  return `${safe}_${stamp}`;
}

function createMessage(side = "left", text = "") {
  const id = window.crypto && crypto.randomUUID
    ? crypto.randomUUID().replace(/-/g, "")
    : `${Date.now()}_${Math.floor(Math.random() * 100000)}`;

  return {
    id: `msg_${id}`,
    side,
    text,
    pauseSeconds: 0.8,
    hidden: false,
    loop: false,
    audioFile: ""
  };
}

function cloneProject(value) {
  return JSON.parse(JSON.stringify(value || project));
}

function ensureMessages() {
  if (project.messages.length) return;
  project.messages.push(createMessage("left", "Hello, are you ready to practice?"));
  project.messages.push(createMessage("right", "Yes, let's start with a short dialogue."));
}

function syncProjectFromUi() {
  const title = $("dialogueTitle")?.value || "Dialogue";
  project.title = title.trim() || "Dialogue";
  selectedLanguage = $("voiceLanguage")?.value || selectedLanguage;
  project.languageKey = selectedLanguage;
  project.leftVoice = $("leftVoice")?.value || project.leftVoice || "";
  project.rightVoice = $("rightVoice")?.value || project.rightVoice || "";

  document.querySelectorAll("[data-message-id]").forEach(row => {
    const msg = project.messages.find(x => x.id === row.dataset.messageId);
    if (!msg) return;

    const text = row.querySelector(".message-text");
    const pause = row.querySelector(".pause-input");
    msg.text = text ? text.value : msg.text;
    msg.pauseSeconds = pause ? Math.max(0, Math.min(8, parseFloat(pause.value || "0") || 0)) : msg.pauseSeconds;
  });
}

function hasAudioFiles(value = project) {
  return !!(value && Array.isArray(value.messages) && value.messages.some(x => x && x.audioFile));
}

function setAudioReady(ready) {
  audioReady = !!ready;
  updateAudioControls();
}

function markAudioDirty() {
  if (playing) post("stopPlayback", {});
  if (editMode) {
    updateAudioControls();
    return;
  }
  setAudioReady(false);
}

function updateAudioControls() {
  const play = $("btnPlay");
  const save = $("btnSave");
  const seek = $("seekWrap");
  if (play) {
    play.classList.toggle("hidden", !audioReady || editMode);
    play.textContent = playing ? "Ⅱ" : "▶";
    play.title = playing ? "Tạm dừng" : "Phát";
  }
  if (save) {
    save.classList.toggle("hidden", audioReady && !editMode);
  }
  if ($("btnEdit")) {
    $("btnEdit").classList.toggle("active", editMode);
    $("btnEdit").querySelector("span").textContent = editMode ? "Thoát sửa" : "Sửa";
  }
  if (seek) {
    seek.classList.toggle("hidden", !playing);
  }
}

function resetSeek() {
  $("seekRange").value = "0";
  $("seekLabel").textContent = "00:00 / 00:00";
}

function formatTime(seconds) {
  const total = Math.max(0, Math.floor(Number(seconds || 0)));
  const m = Math.floor(total / 60);
  const s = total % 60;
  return `${String(m).padStart(2, "0")}:${String(s).padStart(2, "0")}`;
}

function voiceLanguageKey(voice) {
  const value = String(voice || "").trim();
  const found = voices.find(v => v.voice === value);
  if (found) return found.languageKey || String(found.languageCode || "").split("-")[0] || "";
  return value.split("-")[0] || "";
}

function renderLanguages(defaults = {}) {
  const map = new Map();
  voices.forEach(v => {
    const key = v.languageKey || String(v.languageCode || "").split("-")[0] || v.voice;
    if (!map.has(key)) map.set(key, v.languageName || key);
  });

  const preferredLanguage =
    defaults.selectedLanguage ||
    defaults.languageKey ||
    project.languageKey ||
    voiceLanguageKey(defaults.leftVoice) ||
    voiceLanguageKey(project.leftVoice) ||
    voiceLanguageKey(defaults.rightVoice) ||
    voiceLanguageKey(project.rightVoice) ||
    selectedLanguage ||
    voices[0]?.languageKey ||
    "en";

  selectedLanguage = map.has(preferredLanguage)
    ? preferredLanguage
    : (map.keys().next().value || preferredLanguage);

  $("voiceLanguage").innerHTML = Array.from(map.entries()).map(([key, label]) => (
    `<option value="${esc(key)}">${esc(label)}</option>`
  )).join("");
  $("voiceLanguage").value = selectedLanguage;
  project.languageKey = selectedLanguage;
  renderGeminiLanguageOptions(selectedLanguage);
}

function voicesForLanguage() {
  const lang = selectedLanguage || $("voiceLanguage")?.value || "";
  const filtered = voices.filter(v => (v.languageKey || String(v.languageCode || "").split("-")[0]) === lang);
  return filtered.length ? filtered : voices;
}

function pickVoice(preferredGender, fallback) {
  const list = voicesForLanguage();
  if (fallback && list.some(v => v.voice === fallback)) return fallback;

  const genderMatch = list.find(v => String(v.gender || "").toLowerCase() === preferredGender);
  if (genderMatch) return genderMatch.voice;

  return list[0]?.voice || "";
}

function renderVoices(defaults = {}) {
  renderLanguages(defaults);

  const list = voicesForLanguage();
  const html = list.map(v => (
    `<option value="${esc(v.voice)}">${esc(v.label || v.voice)}</option>`
  )).join("");

  $("leftVoice").innerHTML = html;
  $("rightVoice").innerHTML = html;

  project.leftVoice = pickVoice("female", project.leftVoice || defaults.leftVoice);
  project.rightVoice = pickVoice("male", project.rightVoice || defaults.rightVoice);

  $("leftVoice").value = project.leftVoice;
  $("rightVoice").value = project.rightVoice;
  project.languageKey = selectedLanguage;
  renderGeminiVoiceOptions();
}

function applyLanguageAndVoices(languageKey, leftVoice, rightVoice, options = {}) {
  selectedLanguage = languageKey || voiceLanguageKey(leftVoice) || selectedLanguage;
  project.languageKey = selectedLanguage;
  project.leftVoice = leftVoice || "";
  project.rightVoice = rightVoice || "";
  renderVoices({ selectedLanguage, leftVoice, rightVoice });

  if (leftVoice && Array.from($("leftVoice").options).some(o => o.value === leftVoice)) {
    project.leftVoice = leftVoice;
    $("leftVoice").value = leftVoice;
  }

  if (rightVoice && Array.from($("rightVoice").options).some(o => o.value === rightVoice)) {
    project.rightVoice = rightVoice;
    $("rightVoice").value = rightVoice;
  }

  if ($("geminiLanguage")) {
    $("geminiLanguage").value = selectedLanguage;
    renderGeminiVoiceOptions();

    if (leftVoice && Array.from($("geminiLeftVoice").options).some(o => o.value === leftVoice)) {
      $("geminiLeftVoice").value = leftVoice;
    }

    if (rightVoice && Array.from($("geminiRightVoice").options).some(o => o.value === rightVoice)) {
      $("geminiRightVoice").value = rightVoice;
    }
  }

  if (options.dirty !== false) markAudioDirty();
}

function syncToolbarToProjectVoices() {
  const langKey = project.languageKey ||
    voiceLanguageKey(project.leftVoice) ||
    voiceLanguageKey(project.rightVoice) ||
    selectedLanguage;

  applyLanguageAndVoices(langKey, project.leftVoice, project.rightVoice, { dirty: false });
}

function languageInfo(key) {
  return voices.find(v => (v.languageKey || String(v.languageCode || "").split("-")[0]) === key) || null;
}

function renderGeminiLanguageOptions(preferredKey = "") {
  const geminiLanguage = $("geminiLanguage");
  if (!geminiLanguage) return;

  const map = new Map();
  voices.forEach(v => {
    const key = v.languageKey || String(v.languageCode || "").split("-")[0] || v.voice;
    if (!map.has(key)) map.set(key, v.languageName || key);
  });

  const current = preferredKey || selectedLanguage || $("voiceLanguage")?.value || geminiLanguage.value || "en";
  geminiLanguage.innerHTML = Array.from(map.entries()).map(([key, label]) => (
    `<option value="${esc(key)}">${esc(label)}</option>`
  )).join("");
  geminiLanguage.value = map.has(current) ? current : (selectedLanguage || "en");
}

function renderGeminiVoiceOptions() {
  const lang = $("geminiLanguage")?.value || selectedLanguage || "";
  const list = voices.filter(v => (v.languageKey || String(v.languageCode || "").split("-")[0]) === lang);
  const source = list.length ? list : voices;
  const html = source.map(v => (
    `<option value="${esc(v.voice)}">${esc(v.label || v.voice)}</option>`
  )).join("");

  const left = $("geminiLeftVoice");
  const right = $("geminiRightVoice");
  if (!left || !right) return;

  const previousLeft = left.value;
  const previousRight = right.value;
  left.innerHTML = html;
  right.innerHTML = html;

  left.value = source.some(v => v.voice === previousLeft)
    ? previousLeft
    : (source.find(v => String(v.gender || "").toLowerCase() === "female")?.voice || source[0]?.voice || "");
  right.value = source.some(v => v.voice === previousRight)
    ? previousRight
    : (source.find(v => String(v.gender || "").toLowerCase() === "male")?.voice || source[1]?.voice || source[0]?.voice || "");
}

function renderCourses(selectedCourseId = "") {
  const q = courseFilter.trim().toLowerCase();
  const filtered = q
    ? courses.filter(c => String(c.title || "").toLowerCase().includes(q))
    : courses;

  const list = filtered.length
    ? filtered
    : [{ id: "", title: "Chưa có học phần", count: 0 }];

  $("courseSelect").innerHTML = list.map(c => (
    `<option value="${esc(c.id)}">${esc(c.title)} (${Number(c.count || 0)})</option>`
  )).join("");

  if (selectedCourseId && list.some(c => c.id === selectedCourseId)) {
    $("courseSelect").value = selectedCourseId;
  }
}

function renderDialogueList() {
  const q = dialogueFilter.trim().toLowerCase();
  const filtered = q
    ? dialogueItems.filter(x => String(x.title || "").toLowerCase().includes(q))
    : dialogueItems;

  $("dialogueCount").textContent = `${dialogueItems.length} mục đã lưu`;

  if (!filtered.length) {
    $("dialogueList").innerHTML = `<div class="dialogue-empty">Chưa có đối thoại đã lưu.</div>`;
    return;
  }

  $("dialogueList").innerHTML = filtered.map(item => {
    const active = item.id === project.id;
    const count = Number(item.messageCount || 0);
    return `
      <div class="dialogue-item ${active ? "active" : ""}" data-dialogue-id="${esc(item.id)}">
        <button class="dialogue-main" type="button" data-dialogue-act="load" data-dialogue-id="${esc(item.id)}">
          <div class="dialogue-item-title">${esc(item.title || "Dialogue")}</div>
          <div class="dialogue-item-meta">${count} message</div>
        </button>
        <div class="dialogue-actions">
          <button class="dialogue-action" type="button" title="Sửa tên" data-dialogue-act="rename" data-dialogue-id="${esc(item.id)}">Sửa</button>
          <button class="dialogue-action danger" type="button" title="Xóa" data-dialogue-act="delete" data-dialogue-id="${esc(item.id)}">Xóa</button>
        </div>
      </div>
    `;
  }).join("");

  $("dialogueList").querySelectorAll("[data-dialogue-act='load']").forEach(btn => {
    btn.addEventListener("click", () => {
      if (editMode && !exitEditModeWithConfirm()) return;
      post("loadDialogue", { id: btn.dataset.dialogueId || "" });
    });
  });

  $("dialogueList").querySelectorAll("[data-dialogue-act='rename']").forEach(btn => {
    btn.addEventListener("click", () => {
      const id = btn.dataset.dialogueId || "";
      const item = dialogueItems.find(x => x.id === id);
      renameDialogue(id, item?.title || "Dialogue");
    });
  });

  $("dialogueList").querySelectorAll("[data-dialogue-act='delete']").forEach(btn => {
    btn.addEventListener("click", () => {
      const id = btn.dataset.dialogueId || "";
      const item = dialogueItems.find(x => x.id === id);
      deleteDialogue(id, item?.title || "Dialogue");
    });
  });
}

function renameDialogue(id, currentTitle) {
  const next = window.prompt("Tên đối thoại mới", currentTitle || "Dialogue");
  const title = String(next || "").trim();
  if (!id || !title || title === currentTitle) return;
  post("renameDialogue", { id, title });
}

function deleteDialogue(id, title) {
  if (!id) return;
  const ok = window.confirm(`Xóa đối thoại "${title || "Dialogue"}"?`);
  if (!ok) return;
  post("deleteDialogue", { id });
}

function hasEditChanges() {
  if (!editMode || !editBaseProject) return false;
  syncProjectFromUi();
  return JSON.stringify(project) !== JSON.stringify(editBaseProject);
}

function enterEditMode() {
  if (playing) post("stopPlayback", {});
  editBaseProject = cloneProject(project);
  editMode = true;
  renderProject();
  updateAudioControls();
  return true;
}

function exitEditModeWithConfirm() {
  if (!editMode) return true;

  if (hasEditChanges()) {
    const ok = window.confirm("Bạn chưa lưu chỉnh sửa. Bỏ thay đổi và dùng lại text/audio cũ?");
    if (!ok) return false;
    project = cloneProject(editBaseProject);
  }

  editMode = false;
  editBaseProject = null;
  renderProject();
  updateAudioControls();
  return true;
}

function toggleEditMode() {
  if (editMode) {
    exitEditModeWithConfirm();
    return;
  }

  enterEditMode();
}

function renderProject() {
  $("dialogueTitle").value = project.title || "Dialogue";
  if (voices.length && $("leftVoice").options.length) {
    if (Array.from($("leftVoice").options).some(o => o.value === project.leftVoice)) {
      $("leftVoice").value = project.leftVoice;
    }
    if (Array.from($("rightVoice").options).some(o => o.value === project.rightVoice)) {
      $("rightVoice").value = project.rightVoice;
    }
  }
  renderDialogueList();

  const list = $("messageList");
  list.innerHTML = project.messages.map((msg, index) => {
    const right = msg.side === "right";
    const hidden = !!msg.hidden;
    const loop = !!msg.loop;
    const editDisabled = editMode ? "" : " disabled";
    const readOnlyAttr = editMode ? "" : " readonly";
    return `
      <div class="message-row ${right ? "right" : "left"} ${hidden ? "hidden-message" : ""} ${activeId === msg.id ? "active" : ""} ${editMode ? "editing" : "locked"}" data-message-id="${esc(msg.id)}" data-message-index="${index}">
        <div class="bubble">
          <div class="message-meta">
            <button class="side-toggle" type="button" data-act="side"${editDisabled}>${right ? "Bên phải" : "Bên trái"}</button>
            <span class="message-index">#${index + 1}</span>
            <label class="pause-wrap">
              <input class="pause-input" type="number" min="0" max="8" step="0.1" value="${Number(msg.pauseSeconds || 0).toFixed(1)}"${editDisabled}>
              <span>giây</span>
            </label>
            <button class="mini-btn ${loop ? "loop-on" : ""}" type="button" data-act="loop">${loop ? "Loop" : "Loop"}</button>
            <button class="mini-btn ${hidden ? "hide-on" : ""}" type="button" data-act="hide">${hidden ? "Hiện" : "Ẩn"}</button>
            <button class="mini-btn danger" type="button" data-act="remove"${editDisabled}>Xóa</button>
          </div>
          <textarea class="message-text" spellcheck="false"${readOnlyAttr}>${esc(msg.text || "")}</textarea>
          <div class="hidden-note">Message đang ẩn</div>
        </div>
      </div>
    `;
  }).join("");

  list.querySelectorAll("[data-act]").forEach(btn => {
    btn.addEventListener("click", () => handleMessageAction(btn));
  });

  list.querySelectorAll(".message-text,.pause-input").forEach(input => {
    input.addEventListener("input", () => {
      if (!editMode) return;
      syncProjectFromUi();
      markAudioDirty();
    });
  });

  list.querySelectorAll("[data-message-id]").forEach(row => {
    row.addEventListener("click", e => {
      if (e.target.closest("button,input")) return;
      if (!audioReady || editMode) return;
      playFromMessage(parseInt(row.dataset.messageIndex || "0", 10));
    });
  });
}

function handleMessageAction(btn) {
  if (editMode) syncProjectFromUi();
  const row = btn.closest("[data-message-id]");
  if (!row) return;

  const msg = project.messages.find(x => x.id === row.dataset.messageId);
  if (!msg) return;

  const act = btn.dataset.act;
  if ((act === "side" || act === "remove") && !editMode) return;

  if (act === "side") {
    msg.side = msg.side === "right" ? "left" : "right";
    markAudioDirty();
  } else if (act === "hide") {
    msg.hidden = !msg.hidden;
  } else if (act === "loop") {
    msg.loop = !msg.loop;
  } else if (act === "remove") {
    project.messages = project.messages.filter(x => x.id !== msg.id);
    if (!project.messages.length) project.messages.push(createMessage("left", ""));
    markAudioDirty();
  }

  renderProject();
}

function addMessage() {
  if (!editMode && !enterEditMode()) return;
  syncProjectFromUi();
  const last = project.messages[project.messages.length - 1];
  const side = last && last.side === "left" ? "right" : "left";
  project.messages.push(createMessage(side, ""));
  markAudioDirty();
  renderProject();
  requestAnimationFrame(() => {
    const rows = document.querySelectorAll("[data-message-id]");
    const lastRow = rows[rows.length - 1];
    if (lastRow) {
      lastRow.scrollIntoView({ behavior: "smooth", block: "center" });
      const text = lastRow.querySelector(".message-text");
      if (text) text.focus();
    }
  });
}

function setMode(next) {
  mode = next === "topic" ? "topic" : "course";
  $("modeCourse").classList.toggle("active", mode === "course");
  $("modeTopic").classList.toggle("active", mode === "topic");
  document.querySelector(".generator").classList.toggle("topic-mode", mode === "topic");
}

function openGeminiPopup() {
  renderGeminiLanguageOptions();
  renderGeminiVoiceOptions();
  $("geminiOverlay").classList.add("show");
  const input = mode === "course" ? $("courseSearch") : $("topicInput");
  if (input) setTimeout(() => input.focus(), 40);
}

function closeGeminiPopup(force = false) {
  if (geminiBusy && !force) return;
  $("geminiOverlay").classList.remove("show");
}

function setAudioBusy(busy) {
  $("btnSave").classList.toggle("loading", !!busy);
  $("btnSave").disabled = !!busy;
}

function setGeminiBusy(busy) {
  geminiBusy = !!busy;
  document.body.classList.toggle("gemini-generating", geminiBusy);
  $("geminiPopup")?.classList.toggle("generating", geminiBusy);
  $("btnGenerate").classList.toggle("loading", !!busy);
  $("btnGenerate").disabled = !!busy;
}

function updateGeminiVoiceMode() {
  const custom = $("geminiVoiceSource")?.value === "custom";
  document.querySelector(".generator").classList.toggle("custom-voice", custom);
  if (custom) syncToolbarFromGeminiCustom();
}

function syncToolbarFromGeminiCustom() {
  if ($("geminiVoiceSource")?.value !== "custom") return;
  const langKey = $("geminiLanguage")?.value || selectedLanguage;
  const leftVoice = $("geminiLeftVoice")?.value || "";
  const rightVoice = $("geminiRightVoice")?.value || "";
  applyLanguageAndVoices(langKey, leftVoice, rightVoice);
}

function saveDialogue() {
  syncProjectFromUi();
  const forceRegenerateAudio = editMode && hasEditChanges();
  if (forceRegenerateAudio) {
    const ok = window.confirm("Lưu chỉnh sửa và tạo lại toàn bộ audio?");
    if (!ok) return;
  }

  savingEdit = editMode;
  setAudioBusy(true);
  post("saveDialogue", { ...project, forceRegenerateAudio });
}

function togglePlayback() {
  if (editMode && !exitEditModeWithConfirm()) return;
  syncProjectFromUi();
  if (playing) {
    post("stopPlayback", {});
    return;
  }
  if (!audioReady) return;
  post("playDialogue", project);
}

function playFromMessage(index) {
  if (editMode && !exitEditModeWithConfirm()) return;
  if (!audioReady) return;
  syncProjectFromUi();
  post("playDialogue", { ...project, startIndex: Math.max(0, index || 0) });
}

function generateDialogue() {
  if (geminiBusy) return;
  if (editMode && !exitEditModeWithConfirm()) return;
  syncProjectFromUi();
  const useCustomVoice = $("geminiVoiceSource")?.value === "custom";
  const langKey = useCustomVoice
    ? ($("geminiLanguage")?.value || selectedLanguage)
    : ($("voiceLanguage")?.value || selectedLanguage);
  const info = languageInfo(langKey);
  const leftVoice = useCustomVoice ? ($("geminiLeftVoice")?.value || "") : project.leftVoice;
  const rightVoice = useCustomVoice ? ($("geminiRightVoice")?.value || "") : project.rightVoice;

  if (useCustomVoice) {
    applyLanguageAndVoices(langKey, leftVoice, rightVoice);
  }

  post("generateGeminiDialogue", {
    mode,
    courseId: $("courseSelect")?.value || "",
    topic: $("topicInput")?.value || "",
    count: parseInt($("messageCount")?.value || "8", 10),
    targetLanguageKey: langKey,
    leftVoice,
    rightVoice,
    targetLanguageName: info?.languageName || langKey || "",
    targetLanguageCode: info?.languageCode || langKey || ""
  });
}

function handleHostMessage(message) {
  const msg = message || {};
  const data = msg.data || {};

  if (msg.action === "init") {
    voices = Array.isArray(data.voices) ? data.voices : [];
    courses = Array.isArray(data.courses) ? data.courses : [];
    dialogueItems = Array.isArray(data.dialogues) ? data.dialogues : [];
    document.body.classList.toggle("light", !data.dark);
    selectedLanguage = data.selectedLanguage || "";
    renderVoices(data.defaults || {});
    renderCourses(data.selectedCourseId || "");
    renderDialogueList();
    ensureMessages();
    renderProject();
    return;
  }

  if (msg.action === "theme") {
    document.body.classList.toggle("light", !data.dark);
    return;
  }

  if (msg.action === "saveDone") {
    if (data.project) project = data.project;
    if (data.path) $("savePath").textContent = data.path;
    if (Array.isArray(data.dialogues)) dialogueItems = data.dialogues;
    if (data.project) syncToolbarToProjectVoices();
    if (savingEdit) {
      editMode = false;
      editBaseProject = null;
      savingEdit = false;
    }
    setAudioBusy(false);
    setAudioReady(hasAudioFiles(project));
    renderProject();
    return;
  }

  if (msg.action === "dialogueList") {
    dialogueItems = Array.isArray(data.items) ? data.items : [];
    renderDialogueList();
    return;
  }

  if (msg.action === "dialogueLoaded") {
    if (data.project) {
      project = data.project;
      syncToolbarToProjectVoices();
      setAudioReady(hasAudioFiles(project));
      renderProject();
    }
    if (data.path) $("savePath").textContent = data.path;
    return;
  }

  if (msg.action === "dialogueRenamed") {
    if (Array.isArray(data.dialogues)) dialogueItems = data.dialogues;
    if (data.id === project.id && data.title) {
      project.title = data.title;
      $("dialogueTitle").value = data.title;
    }
    renderDialogueList();
    return;
  }

  if (msg.action === "dialogueDeleted") {
    if (Array.isArray(data.dialogues)) dialogueItems = data.dialogues;
    if (data.id === project.id) {
      project.id = newProjectId(project.title || "dialogue");
      $("savePath").textContent = "";
      setAudioReady(false);
    }
    renderDialogueList();
    return;
  }

  if (msg.action === "activeMessage") {
    activeId = data.id || "";
    renderProject();
    return;
  }

  if (msg.action === "playbackState") {
    playing = !!data.playing;
    if (!playing) {
      setAudioBusy(false);
      activeId = "";
      resetSeek();
    }
    updateAudioControls();
    return;
  }

  if (msg.action === "playbackProgress") {
    const elapsed = Number(data.elapsedSeconds || 0);
    const total = Math.max(0.001, Number(data.totalSeconds || 0));
    $("seekRange").value = String(Math.max(0, Math.min(1000, Math.round((elapsed / total) * 1000))));
    $("seekLabel").textContent = `${formatTime(elapsed)} / ${formatTime(total)}`;
    return;
  }

  if (msg.action === "geminiBusy") {
    setGeminiBusy(!!data.busy);
    return;
  }

  if (msg.action === "audioBusy") {
    setAudioBusy(!!data.busy);
    return;
  }

  if (msg.action === "geminiDialogue") {
    if (data.project) {
      project = data.project;
      syncToolbarToProjectVoices();
      ensureMessages();
      setAudioReady(false);
      renderProject();
      closeGeminiPopup(true);
      document.querySelector(".conversation")?.scrollTo({ top: 0, behavior: "smooth" });
    }
    return;
  }

  if (msg.action === "toast") {
    setAudioBusy(false);
    savingEdit = false;
    showToast(data.text || "", data.type || "");
  }
}

function showToast(text, type = "") {
  if (window.__unifiedToast) {
    window.__unifiedToast(text, type || "info");
    return;
  }

  const toast = $("toast");
  if (!toast || !text) return;

  toast.className = `toast show ${type}`;
  toast.textContent = text;
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => {
    toast.className = "toast";
  }, 2200);
}

function esc(value) {
  return String(value || "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

document.addEventListener("DOMContentLoaded", () => {
  $("btnAdd").addEventListener("click", addMessage);
  $("btnEdit").addEventListener("click", toggleEditMode);
  $("btnSave").addEventListener("click", saveDialogue);
  $("btnPlay").addEventListener("click", togglePlayback);
  $("btnOpenGemini").addEventListener("click", openGeminiPopup);
  $("btnCloseGemini").addEventListener("click", closeGeminiPopup);
  $("geminiOverlay").addEventListener("click", e => {
    if (e.target === $("geminiOverlay")) closeGeminiPopup();
  });
  $("btnGenerate").addEventListener("click", generateDialogue);
  $("modeCourse").addEventListener("click", () => setMode("course"));
  $("modeTopic").addEventListener("click", () => setMode("topic"));
  $("geminiVoiceSource").addEventListener("change", updateGeminiVoiceMode);
  $("geminiLanguage").addEventListener("change", () => {
    renderGeminiVoiceOptions();
    syncToolbarFromGeminiCustom();
  });
  $("geminiLeftVoice").addEventListener("change", syncToolbarFromGeminiCustom);
  $("geminiRightVoice").addEventListener("change", syncToolbarFromGeminiCustom);
  $("voiceLanguage").addEventListener("change", () => {
    syncProjectFromUi();
    selectedLanguage = $("voiceLanguage").value;
    project.leftVoice = "";
    project.rightVoice = "";
    renderVoices({});
    renderGeminiLanguageOptions();
    renderGeminiVoiceOptions();
    markAudioDirty();
  });
  $("leftVoice").addEventListener("change", () => { syncProjectFromUi(); markAudioDirty(); });
  $("rightVoice").addEventListener("change", () => { syncProjectFromUi(); markAudioDirty(); });
  $("dialogueTitle").addEventListener("input", () => { syncProjectFromUi(); markAudioDirty(); });
  $("courseSearch").addEventListener("input", e => {
    courseFilter = e.target.value || "";
    renderCourses($("courseSelect").value || "");
  });
  $("dialogueSearch").addEventListener("input", e => {
    dialogueFilter = e.target.value || "";
    renderDialogueList();
  });

  setMode("course");
  updateGeminiVoiceMode();
  ensureMessages();
  renderProject();
  resetSeek();
  updateAudioControls();
  post("ready", {});
});

if (window.chrome && window.chrome.webview) {
  window.chrome.webview.addEventListener("message", event => {
    handleHostMessage(event.data);
  });
}

window.addEventListener("beforeunload", e => {
  if (!hasEditChanges()) return;
  e.preventDefault();
  e.returnValue = "";
});
