  const $ = (id)=>document.getElementById(id);

  const state = {
    isDark:false,
    tcFont:null,
    answerIsChinese:false,
    tokens:[],
    placeholder:'',
    chipsHidden:false,
  };

  function post(msg){
    try{
      if(window.chrome && window.chrome.webview){
        window.chrome.webview.postMessage(msg);
      }
    }catch{}
  }

  function setTheme(isDark, tcFont){
    state.isDark = !!isDark;
    if(tcFont) state.tcFont = tcFont;

    document.body.classList.toggle('dark', state.isDark);
    if(state.tcFont){
      document.documentElement.style.setProperty('--tcFont', `'${state.tcFont}',` + getComputedStyle(document.documentElement).getPropertyValue('--tcFont'));
    }
  }

  function showEmpty(dayTitle, message){
    hideGrading();
    hideGradeChoice();
    $('topProgress').textContent = '0 / 0';
    $('topDay').textContent = dayTitle || '';
    $('smallLabel').textContent = '';
    $('qNum').textContent = '';
    $('prompt').textContent = message || '';
    $('prompt').classList.remove('tc');
    $('prompt').classList.add('ui');

    $('answerHeader').style.display = 'none';
    $('chips').style.display = 'none';
    $('btnToggleChips').style.display = 'none';
    $('bottomRow').style.display = 'none';
  }

  function applyChipsVisibility(){
    const hasTokens = state.tokens && state.tokens.length > 0;
    const hidden = !!state.chipsHidden;
    const btn = $('btnToggleChips');
    const chips = $('chips');
    const header = $('answerHeader');

    btn.style.display = hasTokens ? '' : 'none';
    btn.setAttribute('aria-pressed', hidden ? 'true' : 'false');
    btn.title = hidden ? 'Hiện chip' : 'Ẩn chip';
    btn.setAttribute('aria-label', hidden ? 'Hiện chip' : 'Ẩn chip');
    btn.innerHTML = hidden
      ? "<i class='fa-regular fa-eye-slash'></i>"
      : "<i class='fa-solid fa-eye'></i>";

    chips.style.display = hasTokens && !hidden ? '' : 'none';
    header.style.display = hasTokens && !hidden ? '' : 'none';
  }

  function toggleChips(){
    state.chipsHidden = !state.chipsHidden;
    applyChipsVisibility();
    $('input').focus();
  }

  function renderChips(tokens, answerIsChinese){
    const host = $('chips');
    host.innerHTML = '';
    const cls = answerIsChinese ? 'chip tc' : 'chip ui';

    tokens.forEach(t=>{
      const el = document.createElement('div');
      el.className = cls;
      el.textContent = t;
      el.addEventListener('click', ()=>{
        const inp = $('input');
        if(answerIsChinese){
          inp.value = (inp.value || '') + t;
        }else{
          const cur = (inp.value || '').trim();
          if(cur.length===0) inp.value = t;
          else inp.value = (cur.endsWith(' ') ? cur : cur + ' ') + t;
        }
        inp.focus();
        inp.selectionStart = inp.value.length;
        inp.selectionEnd = inp.value.length;
      });
      host.appendChild(el);
    });

    host.scrollTop = 0;
  }

  function showQuestion(q){
    hideGrading();
    hideGradeChoice();
    $('topProgress').textContent = q.topProgress || '';
    $('topDay').textContent = q.dayTitle || '';
    $('smallLabel').textContent = q.smallLabel || '';
    $('qNum').textContent = q.qNum || '';

    $('prompt').textContent = q.prompt || '';
    $('prompt').classList.toggle('tc', !!q.promptIsChinese);
    $('prompt').classList.toggle('ui', !q.promptIsChinese);

    state.answerIsChinese = !!q.answerIsChinese;
    state.tokens = q.tokens || [];
    state.placeholder = q.placeholder || '';

    $('bottomRow').style.display = '';

    renderChips(state.tokens, state.answerIsChinese);
    applyChipsVisibility();

    const inp = $('input');
    inp.value = q.userAnswer || '';
    inp.placeholder = state.placeholder;
    inp.classList.toggle('tc', state.answerIsChinese);

    $('btnSkip').textContent = 'Trước';
    $('btnSkip').disabled = !q.canPrevious;

    $('btnNext').textContent = q.buttonNextText || 'Tiếp';

    setTimeout(()=>{
      inp.focus();
      inp.selectionStart = inp.value.length;
      inp.selectionEnd = inp.value.length;
    }, 0);
  }

  function drawDonut(percent){
    const canvas = $('donut');
    const ctx = canvas.getContext('2d');
    const w = canvas.width, h = canvas.height;
    ctx.clearRect(0, 0, w, h);
    const cx = w/2, cy = h/2;
    const r = 44;
    const thick = 10;

    // base ring
    ctx.beginPath();
    ctx.strokeStyle = state.isDark ? 'rgba(255,255,255,.18)' : 'rgba(15,23,42,.12)';
    ctx.lineWidth = thick;
    ctx.arc(cx, cy, r, 0, Math.PI*2);
    ctx.stroke();

    // progress arc with gradient
    const start = -Math.PI/2;
    const sweep = (Math.max(0, Math.min(100, percent))/100) * Math.PI*2;

    // Create gradient for the progress arc
    const gradient = ctx.createLinearGradient(cx-r, cy-r, cx+r, cy+r);
    gradient.addColorStop(0, '#3e5cff');
    gradient.addColorStop(0.5, '#5b7aff');
    gradient.addColorStop(1, '#7894ff');

    ctx.beginPath();
    ctx.strokeStyle = gradient;
    ctx.lineWidth = thick;
    ctx.lineCap = 'round';
    ctx.arc(cx, cy, r, start, start + sweep);
    ctx.stroke();

    // Add glow effect
    ctx.shadowBlur = 12;
    ctx.shadowColor = 'rgba(62, 92, 255, 0.6)';
    ctx.beginPath();
    ctx.strokeStyle = gradient;
    ctx.lineWidth = thick;
    ctx.lineCap = 'round';
    ctx.arc(cx, cy, r, start, start + sweep);
    ctx.stroke();
    ctx.shadowBlur = 0;

    // text with gradient
    const txtGradient = ctx.createLinearGradient(cx-30, cy-10, cx+30, cy+10);
    txtGradient.addColorStop(0, '#3e5cff');
    txtGradient.addColorStop(1, '#7894ff');

    ctx.fillStyle = txtGradient;
    ctx.font = 'bold 26px Segoe UI';
    const txt = percent + '%';
    const m = ctx.measureText(txt);
    ctx.fillText(txt, cx - m.width/2, cy + 9);
  }

  function showResult(r){
    hideGrading();
    hideGradeChoice();
    $('resSetTitle').textContent = r.setTitle || '';
    $('resCorrect').textContent = r.correctText || '';
    $('resWrong').textContent = r.wrongText || '';
    $('resTime').textContent = r.timeText || '';

    const pct = Math.max(0, Math.min(100, r.percent || 0));
    drawDonut(pct);

    $('mainWrap').classList.add('blurred');
    $('ovResult').classList.add('show');
  }

  function hideResult(){
    $('mainWrap').classList.remove('blurred');
    $('ovResult').classList.remove('show');
  }

  function showReview(rv){
    hideGrading();
    hideGradeChoice();
    $('revSmall').textContent = rv.small || '';
    $('revQNum').textContent = rv.qNum || '';

    $('revPrompt').textContent = rv.prompt || '';
    $('revPrompt').classList.toggle('tc', !!rv.promptIsChinese);
    $('revPrompt').classList.toggle('ui', !rv.promptIsChinese);

    $('revTryLaterWrap').style.display = rv.showTryLater ? '' : 'none';
    $('revTryLaterText').textContent = rv.tryLaterText || '';
    $('revTryLaterText').classList.toggle('tc', !!rv.tryLaterIsChinese);

    $('revYourWrap').style.display = rv.showYour ? '' : 'none';
    $('revYourText').textContent = rv.yourText || '';
    $('revYourText').classList.toggle('tc', !!rv.yourIsChinese);

    const yourOk = !!rv.yourOk;
    $('revYourIcon').textContent = yourOk ? '✓' : '✕';
    $('revYourIcon').style.color = yourOk ? 'var(--green)' : 'var(--red)';

    $('revCorrectText').textContent = rv.correctText || '';
    $('revCorrectText').classList.toggle('tc', !!rv.correctIsChinese);

    const geminiText = rv.geminiExplanation || '';
    $('revGeminiWrap').style.display = geminiText ? '' : 'none';
    $('revGeminiText').textContent = geminiText;

    $('revPrev').disabled = !rv.canPrev;
    $('revNext').disabled = !rv.canNext;

    $('mainWrap').classList.add('blurred');
    $('ovReview').classList.add('show');
  }

  function hideReview(){
    $('mainWrap').classList.remove('blurred');
    $('ovReview').classList.remove('show');
  }

  function showGrading(){
    $('mainWrap').classList.add('blurred');
    hideGradeChoice();
    $('ovGrading').classList.add('show');
  }

  function hideGrading(){
    $('mainWrap').classList.remove('blurred');
    $('ovGrading').classList.remove('show');
  }

  function showGradeChoice(){
    $('mainWrap').classList.add('blurred');
    $('ovGradeChoice').classList.add('show');
  }

  function hideGradeChoice(){
    $('ovGradeChoice').classList.remove('show');
  }

  // Buttons
  $('btnSkip').addEventListener('click', ()=>post({ type:'previous', text: $('input').value || '' }));
  $('btnNext').addEventListener('click', ()=>post({ type:'submit', text: $('input').value || '' }));

  $('input').addEventListener('keydown', (e)=>{
    if(e.key === 'Enter'){
      e.preventDefault();
      post({ type:'submit', text: $('input').value || '' });
    }
  });

  $('resClose').addEventListener('click', ()=>post({ type:'closeOverlayAndExit' }));
  $('btnExit').addEventListener('click', ()=>post({ type:'exit' }));
  $('btnViewResult').addEventListener('click', ()=>{
    hideResult();
    post({ type:'viewResult' });
  });

  $('revClose').addEventListener('click', ()=>post({ type:'closeOverlayAndExit' }));
  $('revPrev').addEventListener('click', ()=>post({ type:'reviewPrev' }));
  $('revNext').addEventListener('click', ()=>post({ type:'reviewNext' }));
  $('btnGradeLocal').addEventListener('click', ()=>{
    hideGradeChoice();
    post({ type:'gradeLocal' });
  });
  $('btnGradeGemini').addEventListener('click', ()=>{
    hideGradeChoice();
    post({ type:'gradeGemini' });
  });
  $('btnToggleChips').addEventListener('click', toggleChips);

  // Host receive
  window.__hostReceive = function(jsonString){
    try{
      const msg = JSON.parse(jsonString);

      if(msg.theme){
        setTheme(!!msg.theme.isDark, msg.theme.tcFont);
      }

      if(msg.type === 'empty'){
        hideResult(); hideReview();
        showEmpty(msg.empty?.dayTitle || '', msg.empty?.message || '');
      }
      else if(msg.type === 'question'){
        hideResult(); hideReview();
        showQuestion(msg.question);
      }
      else if(msg.type === 'result'){
        hideReview();
        showResult(msg.result);
      }
      else if(msg.type === 'grading'){
        hideResult(); hideReview();
        showGrading();
      }
      else if(msg.type === 'gradeChoice'){
        hideResult(); hideReview(); hideGrading();
        showGradeChoice();
      }
      else if(msg.type === 'review'){
        hideResult();
        showReview(msg.review);
      }
      else if(msg.type === 'theme'){
        // already applied above
      }
    }catch{}
  };

  // notify ready
  post({ type:'ready' });
