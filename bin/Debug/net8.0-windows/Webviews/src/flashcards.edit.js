function openEdit(data) {
  document.getElementById('editTerm').value = (data && data.term) ? data.term : '';
  document.getElementById('editDefinition').value = (data && data.definition) ? data.definition : '';
  document.getElementById('editPinyin').value = (data && data.pinyin) ? data.pinyin : '';

  const overlay = document.getElementById('editOverlay');
  overlay.classList.add('show');

  setTimeout(() => { document.getElementById('editTerm').focus(); }, 0);
}

function hideEdit() {
  document.getElementById('editOverlay').classList.remove('show');
}

function saveEdit() {
  const term = document.getElementById('editTerm').value || '';
  const definition = document.getElementById('editDefinition').value || '';
  const pinyin = document.getElementById('editPinyin').value || '';

  sendAction('saveEdit', { term, definition, pinyin });
  hideEdit();
}

document.addEventListener('keydown', e => {
  const settingsOpen = document.getElementById('settingsOverlay').classList.contains('show');
  const editOpen = document.getElementById('editOverlay').classList.contains('show');
  const completionOpen = state.showCompletion && !document.getElementById('completionOverlay').classList.contains('hidden');

  if (editOpen) {
    if (e.key === 'Escape') { hideEdit(); e.preventDefault(); return; }
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      saveEdit(); e.preventDefault(); return;
    }
    const tag = (e.target && e.target.tagName) ? e.target.tagName.toLowerCase() : '';
    if (e.key === 'Enter' && tag === 'input') {
      saveEdit(); e.preventDefault(); return;
    }
    return;
  }

  if (settingsOpen) {
    if (e.key === 'Escape') { hideSettings(); e.preventDefault(); }
    return;
  }

  if (completionOpen) {
    if (e.key === 'Escape') { sendAction('dismissCompletion'); e.preventDefault(); }
    return;
  }

  switch (e.key) {
    case 'ArrowLeft':
      sendAction('prev');
      e.preventDefault();
      break;
    case 'ArrowRight':
      sendAction('next');
      e.preventDefault();
      break;
    case ' ':
      toggleFlip();
      e.preventDefault();
      break;
    case 'n':
    case 'N':
      sendAction('sound');
      e.preventDefault();
      break;
    case 'j':
    case 'J':
      sendAction('prev');
      e.preventDefault();
      break;
    case 'l':
    case 'L':
      sendAction('next');
      e.preventDefault();
      break;
    case 'e':
    case 'E':
      sendAction('edit');
      e.preventDefault();
      break;
    case 's':
    case 'S':
      sendAction('star');
      e.preventDefault();
      break;
    case 'Escape':
      if (isFlipped) {
        toggleFlip();
        e.preventDefault();
      }
      break;
  }
});

document.getElementById('cardWrapper').addEventListener('click', e => {
  if (e.target.closest('button')) return;
  toggleFlip();
});

document.getElementById('editOverlay').addEventListener('mousedown', e => {
  if (e.target && e.target.id === 'editOverlay') hideEdit();
});
