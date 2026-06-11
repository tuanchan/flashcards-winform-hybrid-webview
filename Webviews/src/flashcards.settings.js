function toggleSettings() {
  const overlay = document.getElementById('settingsOverlay');
  overlay.classList.toggle('show');

  if (overlay.classList.contains('show')) {
    loadSettingsFromState();
  }
}

function hideSettings() {
  applyCardSize(state.settings);
  document.getElementById('settingsOverlay').classList.remove('show');
}

function loadSettingsFromState() {
  const s = state.settings;

  document.getElementById('settingsProgressToggle').classList.toggle('on', !!s.progressTracking);
  document.getElementById('settingsStarredToggle').classList.toggle('on', !!s.starredOnly);
  document.getElementById('settingsTtsToggle').classList.toggle('on', !!s.ttsEnabled);
  document.getElementById('settingsAutoPronounceToggle').classList.toggle('on', !!s.autoPronounce);
  document.getElementById('settingsCardCustomSizeToggle')?.classList.toggle('on', !!s.cardCustomSize);
  setCardZoomControl(s.cardZoom);
  setCardWidthControl(s.cardWidth);
  setCardHeightControl(s.cardHeight);
  syncCardSizeModeControls();
  applyCardSize(s);

  rebuildFrontSideDropdown(s.frontSideOptions, s.frontSide);
}

let frontSideOptions = [
  { value: 0, text: 'Ngôn ngữ gốc' },
  { value: 1, text: 'Tiếng Việt' },
  { value: 2, text: 'Phiên âm' }
];

function rebuildFrontSideDropdown(options, selectedValue) {
  frontSideOptions = Array.isArray(options) && options.length
    ? options.map(x => ({
      value: parseInt(x.value, 10),
      text: x.text || ''
    }))
    : [
      { value: 0, text: 'Ngôn ngữ gốc' },
      { value: 1, text: 'Tiếng Việt' },
      { value: 2, text: 'Phiên âm' }
    ];

  const dropdown = document.getElementById('customSelectDropdown');
  const text = document.getElementById('customSelectText');

  if (!dropdown || !text) return;

  dropdown.innerHTML = '';

  frontSideOptions.forEach(opt => {
    const item = document.createElement('div');
    item.className = 'custom-option';
    item.dataset.value = String(opt.value);
    item.textContent = opt.text;
    if (String(opt.value) === String(selectedValue)) {
      item.classList.add('selected');
    }
    item.onclick = function () { selectCustomOption(this); };
    dropdown.appendChild(item);
  });

  const found = frontSideOptions.find(x => String(x.value) === String(selectedValue));
  text.textContent = found ? found.text : (frontSideOptions[0]?.text || 'Ngôn ngữ gốc');
  customSelectValue = parseInt(selectedValue ?? 0, 10) || 0;
}

let customSelectValue = 0;

function toggleCustomSelect() {
  const btn = document.getElementById('customSelectBtn');
  const dropdown = document.getElementById('customSelectDropdown');

  btn.classList.toggle('open');
  dropdown.classList.toggle('hidden');
}

function selectCustomOption(element) {
  const value = parseInt(element.getAttribute('data-value'), 10);
  const text = element.textContent;

  customSelectValue = value;
  document.getElementById('customSelectText').textContent = text;

  document.querySelectorAll('.custom-option').forEach(opt => {
    opt.classList.remove('selected');
  });
  element.classList.add('selected');

  document.getElementById('customSelectBtn').classList.remove('open');
  document.getElementById('customSelectDropdown').classList.add('hidden');
}

document.addEventListener('click', e => {
  const wrapper = document.querySelector('.custom-select-wrapper');
  if (wrapper && !wrapper.contains(e.target)) {
    document.getElementById('customSelectBtn').classList.remove('open');
    document.getElementById('customSelectDropdown').classList.add('hidden');
  }
});

function toggleSettingOption(toggle) {
  toggle.classList.toggle('on');
}

function toggleCardSizeMode() {
  const toggle = document.getElementById('settingsCardCustomSizeToggle');
  if (!toggle) return;
  toggle.classList.toggle('on');
  syncCardSizeModeControls();
  previewCurrentCardSize();
}

function setCardZoomControl(value) {
  const zoom = clampCardZoom(value);
  const range = document.getElementById('settingsCardZoomRange');
  const label = document.getElementById('settingsCardZoomValue');

  if (range) range.value = String(zoom);
  if (label) label.textContent = zoom + '%';
}

function previewCardZoom(value) {
  if (isCardCustomSizeEnabled()) return;
  const zoom = clampCardZoom(value);
  setCardZoomControl(zoom);
  applyCardZoom(zoom);
}

function clampCardZoom(value) {
  return Math.min(140, Math.max(80, parseInt(value, 10) || 100));
}

function setCardWidthControl(value) {
  const width = clampCardDimension(value);
  const range = document.getElementById('settingsCardWidthRange');
  const label = document.getElementById('settingsCardWidthValue');

  if (range) range.value = String(width);
  if (label) label.textContent = 'Rộng ' + width + '%';
}

function setCardHeightControl(value) {
  const height = clampCardDimension(value);
  const range = document.getElementById('settingsCardHeightRange');
  const label = document.getElementById('settingsCardHeightValue');

  if (range) range.value = String(height);
  if (label) label.textContent = 'Cao ' + height + '%';
}

function previewCardWidth(value) {
  if (!isCardCustomSizeEnabled()) return;
  const width = clampCardDimension(value);
  setCardWidthControl(width);
  document.documentElement.style.setProperty('--flashcard-width', width + '%');
  document.documentElement.style.setProperty('--flashcard-max-width', Math.round(1200 * width / 100) + 'px');
}

function previewCardHeight(value) {
  if (!isCardCustomSizeEnabled()) return;
  const height = clampCardDimension(value);
  setCardHeightControl(height);
  document.documentElement.style.setProperty('--flashcard-height', height + '%');
}

function clampCardDimension(value) {
  return Math.min(120, Math.max(70, parseInt(value, 10) || 100));
}

function resetCardSizeControls() {
  setCardZoomControl(100);
  setCardWidthControl(100);
  setCardHeightControl(100);
  previewCurrentCardSize();
}

function isCardCustomSizeEnabled() {
  return !!document.getElementById('settingsCardCustomSizeToggle')?.classList.contains('on');
}

function syncCardSizeModeControls() {
  const custom = isCardCustomSizeEnabled();
  const zoomRange = document.getElementById('settingsCardZoomRange');
  const widthRange = document.getElementById('settingsCardWidthRange');
  const heightRange = document.getElementById('settingsCardHeightRange');

  if (zoomRange) zoomRange.disabled = custom;
  if (widthRange) widthRange.disabled = !custom;
  if (heightRange) heightRange.disabled = !custom;

  document.getElementById('settingsCardZoomGroup')?.classList.toggle('disabled', custom);
  document.getElementById('settingsCardWidthGroup')?.classList.toggle('disabled', !custom);
  document.getElementById('settingsCardHeightGroup')?.classList.toggle('disabled', !custom);
}

function previewCurrentCardSize() {
  applyCardSize({
    cardZoom: clampCardZoom(document.getElementById('settingsCardZoomRange')?.value),
    cardWidth: clampCardDimension(document.getElementById('settingsCardWidthRange')?.value),
    cardHeight: clampCardDimension(document.getElementById('settingsCardHeightRange')?.value),
    cardCustomSize: isCardCustomSizeEnabled()
  });
}

function saveSettings() {
  const cardZoom = clampCardZoom(document.getElementById('settingsCardZoomRange')?.value);
  const cardWidth = clampCardDimension(document.getElementById('settingsCardWidthRange')?.value);
  const cardHeight = clampCardDimension(document.getElementById('settingsCardHeightRange')?.value);
  const cardCustomSize = isCardCustomSizeEnabled();
  const data = {
    progressTracking: document.getElementById('settingsProgressToggle').classList.contains('on'),
    starredOnly: document.getElementById('settingsStarredToggle').classList.contains('on'),
    ttsEnabled: document.getElementById('settingsTtsToggle').classList.contains('on'),
    autoPronounce: document.getElementById('settingsAutoPronounceToggle').classList.contains('on'),
    frontSide: customSelectValue,
    cardZoom: cardZoom,
    cardWidth: cardWidth,
    cardHeight: cardHeight,
    cardCustomSize: cardCustomSize
  };

  if (state.settings) {
    state.settings.cardZoom = cardZoom;
    state.settings.cardWidth = cardWidth;
    state.settings.cardHeight = cardHeight;
    state.settings.cardCustomSize = cardCustomSize;
  }
  sendAction('saveSettings', data);
}

function confirmReset() {
  sendAction('resetProgress');
  hideSettings();
}
