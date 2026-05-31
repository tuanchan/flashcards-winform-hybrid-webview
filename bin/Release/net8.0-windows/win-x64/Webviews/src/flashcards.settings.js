function toggleSettings() {
  const overlay = document.getElementById('settingsOverlay');
  overlay.classList.toggle('show');

  if (overlay.classList.contains('show')) {
    loadSettingsFromState();
  }
}

function hideSettings() {
  applyCardZoom(state.settings && state.settings.cardZoom);
  document.getElementById('settingsOverlay').classList.remove('show');
}

function loadSettingsFromState() {
  const s = state.settings;

  document.getElementById('settingsProgressToggle').classList.toggle('on', !!s.progressTracking);
  document.getElementById('settingsStarredToggle').classList.toggle('on', !!s.starredOnly);
  document.getElementById('settingsTtsToggle').classList.toggle('on', !!s.ttsEnabled);
  document.getElementById('settingsAutoPronounceToggle').classList.toggle('on', !!s.autoPronounce);
  setCardZoomControl(s.cardZoom);
  applyCardZoom(s.cardZoom);

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

function setCardZoomControl(value) {
  const zoom = clampCardZoom(value);
  const range = document.getElementById('settingsCardZoomRange');
  const label = document.getElementById('settingsCardZoomValue');

  if (range) range.value = String(zoom);
  if (label) label.textContent = zoom + '%';
}

function previewCardZoom(value) {
  const zoom = clampCardZoom(value);
  setCardZoomControl(zoom);
  applyCardZoom(zoom);
}

function clampCardZoom(value) {
  return Math.min(140, Math.max(80, parseInt(value, 10) || 100));
}

function saveSettings() {
  const cardZoom = clampCardZoom(document.getElementById('settingsCardZoomRange')?.value);
  const data = {
    progressTracking: document.getElementById('settingsProgressToggle').classList.contains('on'),
    starredOnly: document.getElementById('settingsStarredToggle').classList.contains('on'),
    ttsEnabled: document.getElementById('settingsTtsToggle').classList.contains('on'),
    autoPronounce: document.getElementById('settingsAutoPronounceToggle').classList.contains('on'),
    frontSide: customSelectValue,
    cardZoom: cardZoom
  };

  if (state.settings) state.settings.cardZoom = cardZoom;
  sendAction('saveSettings', data);
}

function confirmReset() {
  sendAction('resetProgress');
  hideSettings();
}
