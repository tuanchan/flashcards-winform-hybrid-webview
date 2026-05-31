function openGeminiExamples() {
  const overlay = document.getElementById('geminiOverlay');
  if (overlay) overlay.classList.add('show');
  showGeminiLoading('Đang lấy ví dụ...');
  sendAction('geminiExamples');
}

function closeGeminiExamples() {
  const overlay = document.getElementById('geminiOverlay');
  if (overlay) overlay.classList.remove('show');
}

function regenerateGeminiExamples() {
  showGeminiLoading('Gemini đang tạo ví dụ mới...');
  sendAction('regenerateGeminiExamples');
}

function generateGeminiImageOnly() {
  const imagePanel = document.querySelector('.gemini-image-panel');
  if (imagePanel) imagePanel.classList.add('empty');
  
  const status = document.getElementById('geminiStatus');
  if (status) status.textContent = 'Đang tìm ảnh trên Pixabay...';
  
  sendAction('generateGeminiImageOnly');
}

function showGeminiLoading(text) {
  const status = document.getElementById('geminiStatus');
  const content = document.getElementById('geminiContent');
  const image = document.getElementById('geminiImage');
  const imageCaption = document.getElementById('geminiImageCaption');
  if (status) {
    status.textContent = text || 'Đang xử lý...';
    status.style.color = '';
  }
  if (content) content.classList.remove('hidden'); // Show content to show skeleton
  
  const list = document.getElementById('geminiExampleList');
  const hint = document.getElementById('geminiMemoryHint');
  const imagePanel = document.querySelector('.gemini-image-panel');
  
  if (list) {
    list.innerHTML = `
      <div class="gemini-example gemini-example-loading">
        <div class="gemini-loading-line wide"></div>
        <div class="gemini-loading-line medium"></div>
        <div class="gemini-loading-line short"></div>
      </div>
      <div class="gemini-example gemini-example-loading">
        <div class="gemini-loading-line wide"></div>
        <div class="gemini-loading-line medium"></div>
        <div class="gemini-loading-line short"></div>
      </div>
    `;
  }
  if (hint) { hint.textContent = ''; hint.style.display = 'none'; }
  if (imagePanel) imagePanel.classList.add('empty'); // trigger skeleton
  
  if (image) image.removeAttribute('src');
  if (imageCaption) {
    imageCaption.textContent = '';
    imageCaption.style.display = 'none';
  }
}

window.showGeminiExamples = function(payload) {
  const status = document.getElementById('geminiStatus');
  const content = document.getElementById('geminiContent');
  const list = document.getElementById('geminiExampleList');
  const hint = document.getElementById('geminiMemoryHint');
  const image = document.getElementById('geminiImage');
  const imagePanel = document.querySelector('.gemini-image-panel');
  const imageCaption = document.getElementById('geminiImageCaption');

  const examples = payload && Array.isArray(payload.examples) ? payload.examples : [];
  const imagePath = payload && payload.imagePath ? payload.imagePath : '';
  const imageAlt = payload && payload.imageAlt ? payload.imageAlt : '';
  const imagePrompt = payload && payload.imagePrompt ? payload.imagePrompt : '';

  if (status) status.textContent = payload && payload.fromCache ? 'Đã tải ví dụ đã lưu.' : 'Đã tạo ví dụ mới.';
  if (list) {
    list.innerHTML = examples.length ? examples.map(ex => `
      <div class="gemini-example">
        <div class="gemini-example-source">${geminiEscape(readGeminiField(ex, 'source'))}</div>
        <div class="gemini-example-vi">${geminiEscape(readGeminiField(ex, 'vietnamese'))}</div>
        ${readGeminiField(ex, 'note') ? `<div class="gemini-example-note">${geminiEscape(readGeminiField(ex, 'note'))}</div>` : ''}
      </div>
    `).join('') : `
      <div class="gemini-example gemini-example-loading">
        <div class="gemini-loading-line wide"></div>
        <div class="gemini-loading-line medium"></div>
        <div class="gemini-loading-line short"></div>
      </div>
      <div class="gemini-example gemini-example-loading">
        <div class="gemini-loading-line wide"></div>
        <div class="gemini-loading-line medium"></div>
        <div class="gemini-loading-line short"></div>
      </div>
    `;
  }

  if (hint) {
    const memory = payload && payload.memoryHint ? payload.memoryHint : '';
    hint.textContent = memory ? 'Gợi ý nhớ nhanh: ' + memory : '';
    hint.style.display = memory ? '' : 'none';
  }

  if (image && imagePanel) {
    imagePanel.classList.toggle('empty', !imagePath);
    image.onerror = () => {
      image.removeAttribute('src');
      image.alt = '';
      imagePanel.classList.add('empty');
    };
    if (imagePath) {
      image.src = imagePath;
      image.alt = imageAlt || imagePrompt || 'Ảnh minh họa từ vựng';
    } else {
      image.removeAttribute('src');
      image.alt = '';
    }
  }

  if (imageCaption) {
    imageCaption.textContent = '';
    imageCaption.style.display = 'none';
  }

  if (content) content.classList.remove('hidden');
};

window.showGeminiError = function(message) {
  const status = document.getElementById('geminiStatus');
  const content = document.getElementById('geminiContent');
  if (status) {
    status.textContent = message || 'Không thể tạo ví dụ Gemini.';
    status.style.color = '#ff6b8a';
  }
  if (content) content.classList.add('hidden');
};

function geminiEscape(str) {
  return String(str || '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function readGeminiField(obj, name) {
  if (!obj) return '';
  const pascal = name.charAt(0).toUpperCase() + name.slice(1);
  return obj[name] || obj[pascal] || '';
}

document.addEventListener('click', e => {
  const overlay = document.getElementById('geminiOverlay');
  if (overlay && e.target === overlay) closeGeminiExamples();
});

render();
