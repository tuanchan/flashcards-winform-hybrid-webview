// =========================
  // Theme (NEW - không ảnh hưởng logic)
  // - CardFormWeb dùng body.dark-mode
  // - Bạn có thể gọi từ C#: window.setDarkMode(true/false)
  // =========================
  function setDarkMode(dark){
    document.body.classList.toggle('dark-mode', !!dark);
  }
  window.setDarkMode = setDarkMode;

  // Nếu không gọi từ C# thì mặc định theo OS (optional)

  // =========================
  // State
  // =========================
  let lastPreview = [];
  let dialogOkAction = null;

  // Elements
  const txtTitle = document.getElementById('txtTitle');
  const titlePlaceholder = document.getElementById('titlePlaceholder');
  const txtRaw = document.getElementById('txtRaw');
  const rawPlaceholder = document.getElementById('rawPlaceholder');
  const rbTdTab = document.getElementById('rbTdTab');
  const rbTdComma = document.getElementById('rbTdComma');
  const rbTdCustom = document.getElementById('rbTdCustom');
  const txtTdCustom = document.getElementById('txtTdCustom');
  const rbCardNewline = document.getElementById('rbCardNewline');
  const rbCardSemicolon = document.getElementById('rbCardSemicolon');
  const rbCardCustom = document.getElementById('rbCardCustom');
  const txtCardCustom = document.getElementById('txtCardCustom');
  const btnGridImport = document.getElementById('btnGridImport');
  const btnPreview = document.getElementById('btnPreview');
  const btnSave = document.getElementById('btnSave');
  const txtCoverImage = document.getElementById('txtCoverImage');
  const btnPickCover = document.getElementById('btnPickCover');
  const lblCount = document.getElementById('lblCount');
  const previewList = document.getElementById('previewList');
  const dialogOverlay = document.getElementById('dialogOverlay');
  const dialogIcon = document.getElementById('dialogIcon');
  const dialogTitle = document.getElementById('dialogTitle');
  const dialogMessage = document.getElementById('dialogMessage');
  const btnDialogOk = document.getElementById('btnDialogOk');
  const gridOverlay = document.getElementById('gridOverlay');
  const gridRows = document.getElementById('gridRows');
  const btnGridClose = document.getElementById('btnGridClose');
  const btnGridAddRow = document.getElementById('btnGridAddRow');
  const btnGridSave = document.getElementById('btnGridSave');

  // Language dropdown elements
  const ddLang = document.getElementById('ddLang');
  const ddLangBtn = document.getElementById('ddLangBtn');
  const ddLangText = document.getElementById('ddLangText');
  const ddLangMenu = document.getElementById('ddLangMenu');
  const selLang = document.getElementById('selLang');

  // =========================
  // Language dropdown logic
  // =========================
  const LANG_KEY = 'cardImport_selectedLang';
  const IMPORT_SETTINGS_KEY = 'cardImport_settings_v1';

  function initLanguageDropdown() {
    // Khôi phục ngôn ngữ đã chọn từ localStorage
    const saved = normalizeLanguageCode(localStorage.getItem(LANG_KEY));
    if (saved) {
      const item = ddLangMenu.querySelector(`.ddItem[data-value="${saved}"]`);
      if (item) {
        ddLangMenu.querySelectorAll('.ddItem').forEach(i => i.classList.remove('active'));
        item.classList.add('active');
        ddLangText.textContent = item.textContent;
        selLang.value = saved;
      }
    }
  }

  ddLangBtn.addEventListener('click', function(e) {
    e.stopPropagation();
    const isOpen = ddLang.classList.toggle('open');
    ddLangMenu.classList.toggle('hidden', !isOpen);
  });

  ddLangMenu.addEventListener('click', function(e) {
    const item = e.target.closest('.ddItem');
    if (!item) return;
    ddLangMenu.querySelectorAll('.ddItem').forEach(i => i.classList.remove('active'));
    item.classList.add('active');
    ddLangText.textContent = item.textContent;
    selLang.value = item.dataset.value;
    // Ghi nhớ lựa chọn
    localStorage.setItem(LANG_KEY, item.dataset.value);
    ddLang.classList.remove('open');
    ddLangMenu.classList.add('hidden');
  });

  document.addEventListener('click', function(e) {
    if (!ddLang.contains(e.target)) {
      ddLang.classList.remove('open');
      ddLangMenu.classList.add('hidden');
    }
  });

  // Function to select radio when clicking on wrapper
  function selectRadio(id) {
    document.getElementById(id).checked = true;
    updateCustomStates();
    persistImportSettings();
  }

  // Wire events
  rbTdCustom.addEventListener('change', handleImportSettingChanged);
  rbTdTab.addEventListener('change', handleImportSettingChanged);
  rbTdComma.addEventListener('change', handleImportSettingChanged);
  rbCardCustom.addEventListener('change', handleImportSettingChanged);
  rbCardNewline.addEventListener('change', handleImportSettingChanged);
  rbCardSemicolon.addEventListener('change', handleImportSettingChanged);
  txtTdCustom.addEventListener('input', persistImportSettings);
  txtCardCustom.addEventListener('input', persistImportSettings);
  if (txtCoverImage) txtCoverImage.addEventListener('input', persistImportSettings);
  const chkAutoGemini = document.getElementById('chkAutoGemini');
  if (chkAutoGemini) chkAutoGemini.addEventListener('change', persistImportSettings);
  if (btnGridImport) btnGridImport.addEventListener('click', openGridPopup);
  if (btnGridClose) btnGridClose.addEventListener('click', closeGridPopup);
  if (btnGridAddRow) btnGridAddRow.addEventListener('click', () => addGridRow());
  if (btnGridSave) btnGridSave.addEventListener('click', saveGridRowsToRaw);
  if (gridOverlay) {
    gridOverlay.addEventListener('click', e => {
      if (e.target === gridOverlay) closeGridPopup();
    });
  }
  btnPreview.addEventListener('click', doPreview);
  btnSave.addEventListener('click', doSave);
  if (btnPickCover) btnPickCover.addEventListener('click', () => postMessage({ type: 'pickCoverImage' }));
  txtRaw.addEventListener('input', () => {
    lblCount.textContent = '';
    updateRawPlaceholder();
  });
  btnDialogOk.addEventListener('click', hideDialog);

  window.handleCoverImagePicked = function(path) {
    if (txtCoverImage) txtCoverImage.value = path || '';
    persistImportSettings();
  };

  // Title placeholder logic
  txtTitle.addEventListener('focus', () => {
    titlePlaceholder.classList.add('hidden');
  });

  txtTitle.addEventListener('blur', () => {
    if (!txtTitle.value.trim()) titlePlaceholder.classList.remove('hidden');
  });

  txtTitle.addEventListener('input', () => {
    if (txtTitle.value) titlePlaceholder.classList.add('hidden');
    else titlePlaceholder.classList.remove('hidden');
  });

  // Raw input placeholder logic
  txtRaw.addEventListener('focus', () => rawPlaceholder.classList.add('hidden'));
  txtRaw.addEventListener('blur', () => {
    if (!txtRaw.value.trim()) rawPlaceholder.classList.remove('hidden');
  });

  function updateRawPlaceholder() {
    if (txtRaw.value) rawPlaceholder.classList.add('hidden');
    else rawPlaceholder.classList.remove('hidden');
  }

  // Update custom input states
  function updateCustomStates() {
    txtTdCustom.disabled = !rbTdCustom.checked;
    txtCardCustom.disabled = !rbCardCustom.checked;
  }

  function handleImportSettingChanged() {
    updateCustomStates();
    persistImportSettings();
  }

  function getTermDefSepMode() {
    if (rbTdComma.checked) return 'comma';
    if (rbTdCustom.checked) return 'custom';
    return 'tab';
  }

  function getCardSepMode() {
    if (rbCardSemicolon.checked) return 'semicolon';
    if (rbCardCustom.checked) return 'custom';
    return 'newline';
  }

  function setRadioChecked(map, value, fallback) {
    const radio = map[value] || map[fallback];
    if (radio) radio.checked = true;
  }

  function restoreImportSettings() {
    let settings = null;
    try {
      settings = JSON.parse(localStorage.getItem(IMPORT_SETTINGS_KEY) || 'null');
    } catch (e) {
      settings = null;
    }

    if (!settings || typeof settings !== 'object') return;

    setRadioChecked({
      tab: rbTdTab,
      comma: rbTdComma,
      custom: rbTdCustom
    }, settings.termDefMode, 'tab');

    setRadioChecked({
      newline: rbCardNewline,
      semicolon: rbCardSemicolon,
      custom: rbCardCustom
    }, settings.cardMode, 'newline');

    if (typeof settings.termDefCustom === 'string') txtTdCustom.value = settings.termDefCustom;
    if (typeof settings.cardCustom === 'string') txtCardCustom.value = settings.cardCustom;
    if (txtCoverImage && typeof settings.coverImageSource === 'string') txtCoverImage.value = settings.coverImageSource;

    const chk = document.getElementById('chkAutoGemini');
    if (chk && typeof settings.autoGenerateExamples === 'boolean') {
      chk.checked = settings.autoGenerateExamples;
    }
  }

  function persistImportSettings() {
    try {
      const chk = document.getElementById('chkAutoGemini');
      localStorage.setItem(IMPORT_SETTINGS_KEY, JSON.stringify({
        termDefMode: getTermDefSepMode(),
        termDefCustom: txtTdCustom.value,
        cardMode: getCardSepMode(),
        cardCustom: txtCardCustom.value,
        coverImageSource: txtCoverImage ? txtCoverImage.value.trim() : '',
        autoGenerateExamples: chk ? chk.checked : false
      }));
    } catch (e) {
      console.warn('Failed to persist import settings:', e);
    }
  }

  // Get separators
  function getTermDefSep() {
    if (rbTdTab.checked) return '\t';
    if (rbTdComma.checked) return ',';
    return txtTdCustom.value.trim();
  }

  function getCardSep() {
    if (rbCardNewline.checked) return '\n';
    if (rbCardSemicolon.checked) return ';';
    return txtCardCustom.value.trim();
  }

  // Get selected language info
  function getSelectedLanguage() {
    const active = ddLangMenu.querySelector('.ddItem.active');
    return {
      code: normalizeLanguageCode(selLang.value) || 'en',
      label: active ? active.textContent.trim() : 'Tiếng Anh (English)'
    };
  }

  function normalizeLanguageCode(code) {
    const value = String(code || '').trim();
    const lower = value.toLowerCase();
    if (lower === 'zh' || lower === 'zh-hant' || lower === 'zh-tw' || lower === 'zh-hk' || lower === 'zh-mo') return 'zh-TW';
    if (lower === 'zh-hans' || lower === 'zh-cn' || lower === 'zh-sg') return 'zh-CN';
    return value;
  }

  function splitTailPronunciation(definitionRaw) {
    const text = String(definitionRaw || '').trim();
    if (!text || !isCloseParen(text[text.length - 1])) return { definition: text, pinyin: '' };

    const openIndex = findTailOpenParen(text);
    if (openIndex <= 0) return { definition: text, pinyin: '' };

    const definition = text.slice(0, openIndex).trim();
    const pinyin = text.slice(openIndex + 1, -1).trim();
    return pinyin ? { definition, pinyin } : { definition: text, pinyin: '' };
  }

  function findTailOpenParen(text) {
    let depth = 0;

    for (let i = text.length - 1; i >= 0; i--) {
      const ch = text[i];

      if (isCloseParen(ch)) {
        depth++;
        continue;
      }

      if (!isOpenParen(ch)) continue;

      depth--;
      if (depth === 0) return i;
      if (depth < 0) return -1;
    }

    return -1;
  }

  function isOpenParen(ch) {
    return ch === '(' || ch === '（';
  }

  function isCloseParen(ch) {
    return ch === ')' || ch === '）';
  }

  // Parse cards
  function parseCards(raw, termDefSep, cardSep) {
    const normalized = raw.replace(/\r\n/g, '\n').replace(/\r/g, '\n');
    const lines = normalized.split(cardSep);
    const cards = [];

    for (let line of lines) {
      line = line.trim();
      if (!line) continue;

      const parts = line.split(termDefSep);
      if (parts.length >= 2) {
        const parsedDefinition = splitTailPronunciation(parts.slice(1).join(termDefSep));
        cards.push({
          term: parts[0].trim(),
          definition: parsedDefinition.definition,
          pinyin: parsedDefinition.pinyin
        });
      }
    }
    return cards;
  }

  function openGridPopup() {
    if (!gridOverlay || !gridRows) return;

    const existing = txtRaw.value.trim()
      ? parseCards(txtRaw.value, getTermDefSep(), getCardSep())
      : [];

    gridRows.innerHTML = '';
    const rows = existing.length ? existing : [{}, {}, {}, {}];
    rows.forEach(card => addGridRow(card));
    gridOverlay.classList.add('active');

    const firstInput = gridRows.querySelector('input');
    if (firstInput) setTimeout(() => firstInput.focus(), 50);
  }

  function closeGridPopup() {
    if (gridOverlay) gridOverlay.classList.remove('active');
  }

  function addGridRow(card = {}) {
    if (!gridRows) return;

    const row = document.createElement('tr');
    row.innerHTML = `
      <td class="grid-index-cell"></td>
      <td><input class="grid-input" data-field="term" value="${escapeAttr(card.term || '')}" autocomplete="off"></td>
      <td><input class="grid-input" data-field="definition" value="${escapeAttr(card.definition || '')}" autocomplete="off"></td>
      <td><input class="grid-input" data-field="pinyin" value="${escapeAttr(card.pinyin || '')}" autocomplete="off"></td>
    `;
    gridRows.appendChild(row);
    refreshGridNumbers();

    const inputs = row.querySelectorAll('input');
    inputs.forEach(input => {
      input.addEventListener('keydown', e => {
        if (e.key !== 'Enter') return;
        e.preventDefault();
        const currentRow = input.closest('tr');
        const isLastRow = currentRow && currentRow === gridRows.lastElementChild;
        if (isLastRow) {
          addGridRow();
          const next = gridRows.lastElementChild?.querySelector('input');
          if (next) next.focus();
        }
      });
    });
  }

  function refreshGridNumbers() {
    if (!gridRows) return;
    Array.from(gridRows.children).forEach((row, index) => {
      const cell = row.querySelector('.grid-index-cell');
      if (cell) cell.textContent = String(index + 1);
    });
  }

  function readGridRows() {
    if (!gridRows) return [];

    return Array.from(gridRows.querySelectorAll('tr'))
      .map(row => {
        const read = field => row.querySelector(`[data-field="${field}"]`)?.value.trim() || '';
        return {
          term: read('term'),
          definition: read('definition'),
          pinyin: read('pinyin')
        };
      })
      .filter(card => card.term || card.definition || card.pinyin);
  }

  function saveGridRowsToRaw() {
    const cards = readGridRows().filter(card => card.term && card.definition);
    if (!cards.length) {
      showDialog('Thiếu dữ liệu', 'Hãy nhập ít nhất một hàng có đủ Từ mới và Định nghĩa.', true);
      return;
    }

    rbTdTab.checked = true;
    rbCardNewline.checked = true;
    updateCustomStates();
    persistImportSettings();

    txtRaw.value = cards.map(card => {
      const definition = card.pinyin
        ? `${card.definition} (${card.pinyin})`
        : card.definition;
      return `${card.term}\t${definition}`;
    }).join('\n');

    updateRawPlaceholder();
    closeGridPopup();
    doPreview();
  }

  // Preview
  function doPreview() {
    const termDefSep = getTermDefSep();
    const cardSep = getCardSep();

    if (rbTdCustom.checked && !termDefSep) {
      showDialog('Thiếu phân cách', "Bạn đang chọn 'Tùy chỉnh' nhưng chưa nhập ký tự phân cách.", true);
      return;
    }

    if (rbCardCustom.checked && !cardSep) {
      showDialog('Thiếu phân cách', "Bạn đang chọn 'Tùy chỉnh' nhưng chưa nhập ký tự phân cách.", true);
      return;
    }

    const raw = txtRaw.value.trim();
    if (!raw) {
      previewList.innerHTML = '';
      lblCount.textContent = '';
      showDialog('Thiếu dữ liệu', 'Bạn chưa nhập dữ liệu.', true);
      lastPreview = [];
      return;
    }

    lastPreview = parseCards(raw, termDefSep, cardSep);
    renderPreview(lastPreview);
  }

  // Render preview
  function renderPreview(cards) {
    previewList.innerHTML = '';
    lblCount.textContent = `${cards.length} thẻ`;

    const take = Math.min(cards.length, 500);
    for (let i = 0; i < take; i++) {
      const card = cards[i];
      const pinyinHtml = card.pinyin ? `
          <div class="preview-field">
            <div class="preview-field-caption">Phiên âm</div>
            <div class="preview-field-value">${escapeHtml(card.pinyin)}</div>
          </div>
      ` : '';
      const item = document.createElement('div');
      item.className = 'preview-item';
      item.innerHTML = `
        <div class="preview-item-header">
          <div class="preview-number">${i + 1}</div>
          <div class="preview-item-title">Thẻ ${i + 1}</div>
        </div>
        <div class="preview-content">
          <div class="preview-field">
            <div class="preview-field-caption">Thuật ngữ</div>
            <div class="preview-field-value">${escapeHtml(card.term)}</div>
          </div>
          <div class="preview-field">
            <div class="preview-field-caption">Định nghĩa</div>
            <div class="preview-field-value">${escapeHtml(card.definition)}</div>
          </div>
          ${pinyinHtml}
        </div>
      `;
      previewList.appendChild(item);
    }
  }

  // Save
  function doSave() {
    if (lastPreview.length === 0) doPreview();
    if (lastPreview.length === 0) {
      showDialog('Không có dữ liệu', 'Chưa có thẻ nào hợp lệ để lưu.', true);
      return;
    }

    const title = txtTitle.value.trim();
    if (!title) {
      showDialog('Thiếu tên học phần', 'Bạn chưa đặt tên học phần.\nHãy nhập tên học phần trước khi lưu.', true, () => {
        txtTitle.focus();
      });
      return;
    }

    const lang = getSelectedLanguage();

    const data = {
      type: 'save',
      title: title,
      language: lang.label,
      languageCode: lang.code,
      cards: lastPreview,
      rawInput: txtRaw.value,
      coverImageSource: txtCoverImage ? txtCoverImage.value.trim() : '',
      termDefSep: getTermDefSep(),
      cardSep: getCardSep(),
      autoGenerateExamples: document.getElementById('chkAutoGemini') ? document.getElementById('chkAutoGemini').checked : false
    };

    postMessage(data);

    showDialog('Đã lưu', `Đã lưu ${lastPreview.length} thẻ.`, false, () => {
      postMessage({ type: 'close', dialogResult: 'OK' });
    });
  }

  // Dialog
  function showDialog(title, message, warning, onOk = null) {
    if (window.__unifiedToast) {
      window.__unifiedToast(message || title, warning ? 'warn' : 'success', {
        title: title || '',
        duration: warning ? 4800 : 1800
      });

      if (onOk) {
        setTimeout(onOk, warning ? 150 : 700);
      }
      return;
    }

    dialogTitle.textContent = title;
    dialogMessage.textContent = message;
    dialogIcon.textContent = warning ? '⚠️' : '✅';
    dialogOkAction = onOk;
    dialogOverlay.classList.add('active');
  }

  function hideDialog() {
    dialogOverlay.classList.remove('active');
    const action = dialogOkAction;
    dialogOkAction = null;
    if (action) action();
  }

  // Post message to C#
  function postMessage(data) {
    try {
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(JSON.stringify(data));
      }
    } catch (e) {
      console.error('Failed to post message:', e);
    }
  }

  // Utility
  function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }

  function escapeAttr(text) {
    return escapeHtml(text).replace(/`/g, '&#96;');
  }

  // Initialize
  restoreImportSettings();
  updateCustomStates();
  updateRawPlaceholder();
  initLanguageDropdown();
