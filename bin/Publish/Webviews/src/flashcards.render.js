function render() {
  const body = document.body;
  body.classList.toggle('dark', state.dark);

  const card = document.getElementById('card');
  card.classList.toggle('flipped', isFlipped);

  if (!state.progressTracking) {
    card.classList.remove('neon-known', 'neon-learning', 'neon-pulse', 'stamp-active');

    const stamp = document.getElementById('progressStamp');
    if (stamp) {
      stamp.classList.remove('known', 'learning', 'show', 'hide');

      const span = stamp.querySelector('span');
      if (span) span.textContent = '';
    }
  }

  document.getElementById('frontText').textContent = state.front || 'Chưa có thẻ';
  document.getElementById('backText').textContent = state.back || 'Hãy tạo học phần trước.';

  const subWrap = document.getElementById('subWrap');
  const subText = document.getElementById('subText');
  if (state.sub && state.sub.trim()) {
    subWrap.classList.remove('hidden');
    subText.textContent = state.sub;
  } else {
    subWrap.classList.add('hidden');
    subText.textContent = '';
  }

  const starFront = document.getElementById('starFront');
  const starBack = document.getElementById('starBack');
  starFront.classList.toggle('on', !!state.starred);
  starBack.classList.toggle('on', !!state.starred);

  const toggleProgressEl = document.getElementById('toggleProgress');
  toggleProgressEl.classList.toggle('on', !!state.progressTracking);

  const navIndex = document.getElementById('navIndex');
  navIndex.textContent = state.index + ' / ' + state.total;
  applyCardZoom(state.settings && state.settings.cardZoom);

  const btnPrev = document.getElementById('btnPrev');
  const btnNext = document.getElementById('btnNext');

  btnPrev.disabled = !state.canPrev;
  btnNext.disabled = !state.canNext;

  if (state.progressTracking) {
    btnPrev.classList.add('mark-learning');
    btnNext.classList.add('mark-known');

    btnPrev.innerHTML = `
      <svg class='svg-ico' viewBox='0 0 24 24' aria-hidden='true'>
        <path d='M18 6L6 18'/><path d='M6 6l12 12'/>
      </svg>`;

    btnNext.innerHTML = `
      <svg class='svg-ico' viewBox='0 0 24 24' aria-hidden='true'>
        <path d='M20 6 9 17l-5-5'/>
      </svg>`;

  } else {
    btnPrev.classList.remove('mark-learning');
    btnNext.classList.remove('mark-known');

    btnPrev.innerHTML = `
      <svg class='svg-ico' viewBox='0 0 24 24' aria-hidden='true'>
        <path d='M15 18l-6-6 6-6'/>
      </svg>`;

    btnNext.innerHTML = `
      <svg class='svg-ico' viewBox='0 0 24 24' aria-hidden='true'>
        <path d='M9 6l6 6-6 6'/>
      </svg>`;

  }

  const btnShuffle = document.getElementById('btnShuffle');
  btnShuffle.classList.toggle('active', !!state.shuffleEnabled);

  const completionOverlay = document.getElementById('completionOverlay');
  if (state.showCompletion) {
    completionOverlay.classList.remove('hidden');
    completionOverlay.classList.add('show');
  } else {
    completionOverlay.classList.remove('show');
    completionOverlay.classList.add('hidden');
  }

  if (state.showCompletion && state.completion) {
    const c = state.completion;
    document.getElementById('completionSubtitle').textContent =
      (c.title || '') + (c.isReview ? ' (Ôn từ đang học)' : '');
    document.getElementById('statKnown').textContent = 'Đã biết: ' + (c.known || 0);
    document.getElementById('statLearning').textContent = 'Đang học: ' + (c.learning || 0);
    document.getElementById('statRemaining').textContent = 'Còn lại: ' + (c.remaining || 0);

    const btnReview = document.getElementById('btnReviewLearning');
    btnReview.disabled = !c.canReviewLearning;
    btnReview.style.opacity = c.canReviewLearning ? '1' : '0.4';
  }
}

function applyCardZoom(value) {
  const zoom = Math.min(140, Math.max(80, parseInt(value, 10) || 100));
  document.documentElement.style.setProperty('--flashcard-zoom', String(zoom / 100));
}
