let state = {
  hasSet: false,
  hasCards: false,
  index: 0,
  total: 0,
  front: '',
  back: '',
  sub: '',
  starred: false,
  progressTracking: false,
  shuffleEnabled: false,
  dark: false,
  canPrev: false,
  canNext: false,
  showCompletion: false,
  completion: {},
  srs: {
    level: 0,
    dueDate: '',
    isDue: true,
    text: 'SRS Lv 0 - đến hạn',
    reviewCount: 0,
    lapseCount: 0
  },
  settings: {
    progressTracking: false,
    starredOnly: false,
    ttsEnabled: false,
    autoPronounce: false,
    frontSide: 0,
    cardZoom: 100,
    cardWidth: 100,
    cardHeight: 100,
    cardCustomSize: false,
    sourceLanguage: 'Ngôn ngữ gốc',
    frontSideOptions: [
      { value: 0, text: 'Ngôn ngữ gốc' },
      { value: 1, text: 'Tiếng Việt' },
      { value: 2, text: 'Phiên âm' }
    ]
  }
};

let isFlipped = false;
const MARK_DELAY_MS = 920;
let stateApplyLocked = false;
let queuedState = null;
let lockTimer = null;

function lockStateApply(ms) {
  stateApplyLocked = true;
  if (lockTimer) clearTimeout(lockTimer);

  lockTimer = setTimeout(() => {
    stateApplyLocked = false;

    if (queuedState) {
      state = queuedState;
      queuedState = null;
      isFlipped = false;
      render();
    }
  }, ms);
}

let isTransitioning = false;
// Lưu animationend listener để có thể removeEventListener đúng cách
let _ghostAnimEndHandler = null;

function playCardTransition(direction) {
  if (isTransitioning) return;
  isTransitioning = true;

  const ghost = document.getElementById('cardGhost');
  const ghostContent = document.getElementById('cardGhostContent');

  ghostContent.textContent = state.front || '';

  // Dùng classList.remove trước để reset animation sạch
  ghost.classList.remove('animating', 'reverse');

  // Xóa listener cũ nếu còn sót (tránh memory leak)
  if (_ghostAnimEndHandler) {
    ghost.removeEventListener('animationend', _ghostAnimEndHandler);
    _ghostAnimEndHandler = null;
  }

  // Set direction trước khi add 'animating' — tránh browser batch chung 1 frame
  if (direction === 'prev') ghost.classList.add('reverse');

  // requestAnimationFrame đảm bảo browser đã flush style cũ trước khi trigger animation mới
  requestAnimationFrame(() => {
    requestAnimationFrame(() => {
      ghost.classList.add('animating');

      _ghostAnimEndHandler = () => {
        ghost.classList.remove('animating', 'reverse');
        _ghostAnimEndHandler = null;
        isTransitioning = false;
      };

      ghost.addEventListener('animationend', _ghostAnimEndHandler, { once: true });

      // Failsafe: nếu animationend không fire (tab ẩn, browser throttle)
      // thì tự cleanup sau duration + buffer
      setTimeout(() => {
        if (isTransitioning) {
          ghost.classList.remove('animating', 'reverse');
          isTransitioning = false;
          _ghostAnimEndHandler = null;
        }
      }, 560);
    });
  });
}

let markPending = false;
let markFailSafeTimer = null;

function triggerProgressFx(type) {
  const card = document.getElementById('card');
  const stamp = document.getElementById('progressStamp');
  if (!card || !stamp) return;

  const span = stamp.querySelector('span') || stamp;

  card.classList.remove('neon-known', 'neon-learning', 'neon-pulse', 'stamp-active');
  stamp.classList.remove('known', 'learning', 'show', 'hide');

  if (type === 'known') {
    span.textContent = 'Đã thuộc';
    stamp.classList.add('known');
    card.classList.add('neon-known');
  } else {
    span.textContent = 'Đang học';
    stamp.classList.add('learning');
    card.classList.add('neon-learning');
  }

  card.classList.add('stamp-active');

  void stamp.offsetWidth;
  void card.offsetWidth;

  stamp.classList.add('show');
  card.classList.add('neon-pulse');

  markPending = true;

  if (markFailSafeTimer) clearTimeout(markFailSafeTimer);
  markFailSafeTimer = setTimeout(() => {
    if (markPending) finishMarkFx();
  }, 900);
}

function finishMarkFx() {
  const card = document.getElementById('card');
  const stamp = document.getElementById('progressStamp');
  if (!card || !stamp) return;

  const span = stamp.querySelector('span') || stamp;

  stamp.classList.remove('show');
  stamp.classList.add('hide');

  setTimeout(() => {
    stamp.classList.remove('hide', 'known', 'learning');
    card.classList.remove('neon-pulse', 'neon-known', 'neon-learning', 'stamp-active');
    span.textContent = '';
  }, 240);

  markPending = false;
}

function sendAction(action, data) {
  const payload = Object.assign({ action }, data || {});
  const isProgressMode = !!(state && state.progressTracking);

  if ((action === 'prev' || action === 'next') && isProgressMode) {
    if (action === 'prev') {
      if (typeof triggerProgressFx === 'function') triggerProgressFx('learning');
    } else {
      if (typeof triggerProgressFx === 'function') triggerProgressFx('known');
    }

    window.chrome.webview.postMessage(JSON.stringify(payload));
    return;
  }

  if (action === 'prev') playCardTransition('prev');
  else if (action === 'next') playCardTransition('next');

  window.chrome.webview.postMessage(JSON.stringify(payload));
}

function toggleProgress() {
  state.progressTracking = !state.progressTracking;
  sendAction('toggleProgress', { enabled: state.progressTracking });
  render();
}

function toggleFlip() {
  isFlipped = !isFlipped;
  document.getElementById('card').classList.toggle('flipped', isFlipped);
  if (isFlipped) {
    sendAction('flip');
  }
}

window.updateState = function(newState) {
  state = newState;
  isFlipped = false;
  render();

  if (markPending) {
    if (markFailSafeTimer) { clearTimeout(markFailSafeTimer); markFailSafeTimer = null; }

    setTimeout(() => {
      finishMarkFx();
    }, 500);
  }
};
