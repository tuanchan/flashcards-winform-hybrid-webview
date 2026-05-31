const MIC_LANG_MAP = {
  0: 'zh-TW',
  1: 'vi-VN',
  2: 'zh-TW',
};

const CHINESE_PRONUNCIATION_EQUIV = {
  '訂': '定', '订': '定',
  '臺': '台', '檯': '台', '颱': '台',
  '裡': '里', '裏': '里',
  '隻': '只', '祇': '只',
  '週': '周',
  '麼': '么', '麽': '么',
  '乾': '干', '幹': '干',
  '發': '发', '髮': '发',
  '後': '后',
  '鐘': '钟', '鍾': '钟',
  '麵': '面',
  '萬': '万', '與': '与', '專': '专', '業': '业', '東': '东',
  '兩': '两', '嚴': '严', '個': '个', '豐': '丰', '臨': '临',
  '為': '为', '麗': '丽', '舉': '举', '義': '义', '烏': '乌',
  '樂': '乐', '習': '习', '鄉': '乡', '書': '书', '買': '买',
  '賣': '卖', '亂': '乱', '爭': '争', '於': '于', '虧': '亏',
  '雲': '云', '亞': '亚', '產': '产', '親': '亲', '儀': '仪',
  '們': '们', '價': '价', '眾': '众', '優': '优', '會': '会',
  '傘': '伞', '偉': '伟', '傳': '传', '傷': '伤', '傾': '倾',
  '僅': '仅', '僕': '仆', '兒': '儿', '內': '内', '冊': '册',
  '寫': '写', '軍': '军', '農': '农', '凍': '冻', '劉': '刘',
  '則': '则', '剛': '刚', '創': '创', '劇': '剧', '劍': '剑',
  '辦': '办', '務': '务', '動': '动', '勞': '劳', '勢': '势',
  '區': '区', '醫': '医', '華': '华', '單': '单', '賬': '账',
  '參': '参', '雙': '双', '變': '变', '葉': '叶', '號': '号',
  '聽': '听', '問': '问', '員': '员', '園': '园', '國': '国',
  '圍': '围', '圖': '图', '圓': '圆', '壞': '坏', '塊': '块',
  '堅': '坚', '報': '报', '場': '场', '塵': '尘', '壓': '压',
  '聲': '声', '備': '备', '夠': '够', '夢': '梦', '奮': '奋',
  '婦': '妇', '媽': '妈', '妝': '妆', '娛': '娱', '孫': '孙',
  '學': '学', '寧': '宁', '寶': '宝', '實': '实', '寵': '宠',
  '審': '审', '寬': '宽', '將': '将', '尋': '寻', '對': '对',
  '導': '导', '屆': '届', '層': '层', '屬': '属', '歲': '岁',
  '島': '岛', '幣': '币', '帥': '帅', '師': '师', '帳': '帐',
  '帶': '带', '幫': '帮', '幾': '几', '庫': '库', '應': '应',
  '廟': '庙', '廠': '厂', '廣': '广', '廳': '厅', '彈': '弹',
  '強': '强', '歸': '归', '當': '当', '錄': '录', '彎': '弯',
  '張': '张', '徑': '径', '從': '从', '復': '复', '憶': '忆',
  '憂': '忧', '懷': '怀', '態': '态', '總': '总', '戀': '恋',
  '惡': '恶', '悶': '闷', '惱': '恼', '愛': '爱', '慘': '惨',
  '慣': '惯', '慶': '庆', '慮': '虑', '憐': '怜', '憑': '凭',
  '憤': '愤', '懶': '懒', '懼': '惧', '戰': '战', '戲': '戏',
  '戶': '户', '拋': '抛', '捨': '舍', '掃': '扫', '掛': '挂',
  '採': '采', '揚': '扬', '換': '换', '損': '损', '搖': '摇',
  '搶': '抢', '擔': '担', '據': '据', '擇': '择', '擊': '击',
  '擋': '挡', '擠': '挤', '擬': '拟', '擴': '扩', '擺': '摆',
  '擾': '扰', '攜': '携', '攝': '摄', '敗': '败', '敵': '敌',
  '數': '数', '斷': '断', '時': '时', '曉': '晓', '暫': '暂',
  '曬': '晒', '標': '标', '棟': '栋', '欄': '栏', '樹': '树',
  '樣': '样', '橋': '桥', '機': '机', '檔': '档', '檢': '检',
  '樓': '楼', '權': '权', '歡': '欢', '歐': '欧', '歷': '历',
  '氣': '气', '漢': '汉', '湯': '汤', '溫': '温', '滅': '灭',
  '滿': '满', '潔': '洁', '潛': '潜', '潤': '润', '濃': '浓',
  '濕': '湿', '濟': '济', '灣': '湾', '災': '灾', '無': '无',
  '煩': '烦', '燈': '灯', '燒': '烧', '營': '营', '牆': '墙',
  '獨': '独', '獲': '获', '環': '环', '現': '现', '畢': '毕',
  '畫': '画', '異': '异', '療': '疗', '癢': '痒', '盞': '盏',
  '監': '监', '盤': '盘', '睏': '困', '著': '着', '瞭': '了',
  '禮': '礼', '禍': '祸', '離': '离', '種': '种', '稱': '称',
  '穩': '稳', '窮': '穷', '筆': '笔', '節': '节', '範': '范',
  '築': '筑', '簡': '简', '簽': '签', '類': '类', '糧': '粮',
  '糾': '纠', '紀': '纪', '約': '约', '紅': '红', '納': '纳',
  '純': '纯', '紙': '纸', '級': '级', '細': '细', '終': '终',
  '組': '组', '結': '结', '絕': '绝', '給': '给', '統': '统',
  '絲': '丝', '經': '经', '綠': '绿', '維': '维', '網': '网',
  '緊': '紧', '線': '线', '練': '练', '縣': '县', '績': '绩',
  '織': '织', '繪': '绘', '繼': '继', '續': '续', '罰': '罚',
  '羅': '罗', '聰': '聪', '聯': '联', '職': '职', '腦': '脑',
  '腳': '脚', '臉': '脸', '舊': '旧', '艱': '艰', '藝': '艺',
  '莊': '庄', '藍': '蓝', '藥': '药', '蘋': '苹', '處': '处',
  '虛': '虚', '蟲': '虫', '衛': '卫', '衝': '冲', '補': '补',
  '裝': '装', '製': '制', '褲': '裤', '見': '见', '規': '规',
  '視': '视', '覺': '觉', '觀': '观', '觸': '触', '計': '计',
  '訊': '讯', '討': '讨', '訓': '训', '記': '记', '訪': '访',
  '設': '设', '許': '许', '訴': '诉', '詞': '词', '該': '该',
  '詳': '详', '試': '试', '誠': '诚', '話': '话', '語': '语',
  '誤': '误', '說': '说', '誰': '谁', '課': '课', '調': '调',
  '談': '谈', '請': '请', '諒': '谅', '論': '论', '謝': '谢',
  '證': '证', '識': '识', '譯': '译', '議': '议', '護': '护',
  '讀': '读', '讚': '赞', '讓': '让', '負': '负', '財': '财',
  '貧': '贫', '貨': '货', '責': '责', '貴': '贵', '費': '费',
  '資': '资', '賓': '宾', '賠': '赔', '賞': '赏', '賺': '赚',
  '購': '购', '贏': '赢', '趕': '赶', '轉': '转', '輪': '轮',
  '軟': '软', '輕': '轻', '較': '较', '輔': '辅', '輛': '辆',
  '輸': '输', '辭': '辞', '邊': '边', '遞': '递', '遠': '远',
  '適': '适', '遲': '迟', '選': '选', '遺': '遗', '郵': '邮',
  '鄰': '邻', '醜': '丑', '釋': '释', '針': '针', '鈴': '铃',
  '銀': '银', '銅': '铜', '錄': '录', '錢': '钱', '錯': '错',
  '鍋': '锅', '鍵': '键', '鎖': '锁', '鏡': '镜', '鐵': '铁',
  '鑰': '钥', '長': '长', '門': '门', '閉': '闭', '開': '开',
  '間': '间', '閱': '阅', '關': '关', '隊': '队', '階': '阶',
  '陽': '阳', '險': '险', '隨': '随', '隱': '隐', '難': '难',
  '電': '电', '霧': '雾', '靈': '灵', '靜': '静', '頂': '顶',
  '項': '项', '順': '顺', '預': '预', '領': '领', '頭': '头',
  '題': '题', '額': '额', '顏': '颜', '願': '愿', '顧': '顾',
  '風': '风', '飛': '飞', '飯': '饭', '飲': '饮', '飽': '饱',
  '餅': '饼', '館': '馆', '馬': '马', '駕': '驾', '駛': '驶',
  '騎': '骑', '驗': '验', '驚': '惊', '體': '体', '魚': '鱼',
  '鳥': '鸟', '雞': '鸡', '黃': '黄', '點': '点', '齊': '齐',
  '龍': '龙'
};

let micRecognition = null;
let micIsRecording = false;
let micSuccessAudio = null;

function getMicLang() {
  const raw = state && state.settings && state.settings.languageCode
    ? state.settings.languageCode
    : MIC_LANG_MAP[state && state.settings ? state.settings.frontSide : 0];
  return normalizeMicLang(raw);
}

function normalizeMicLang(code) {
  const value = String(code || '').trim();
  const lower = value.toLowerCase();
  if (!lower || lower === 'zh' || lower === 'zh-tw' || lower === 'zh-hant' || lower === 'zh-hk' || lower === 'zh-mo') return 'zh-TW';
  if (lower === 'zh-cn' || lower === 'zh-hans' || lower === 'zh-sg') return 'zh-CN';
  if (lower === 'en') return 'en-US';
  if (lower === 'vi') return 'vi-VN';
  if (lower === 'ja') return 'ja-JP';
  if (lower === 'ko') return 'ko-KR';
  if (lower === 'de') return 'de-DE';
  if (lower === 'fr') return 'fr-FR';
  if (lower === 'es') return 'es-ES';
  if (lower === 'ru') return 'ru-RU';
  return value || 'zh-TW';
}

function getTargetText() {
  return (state && state.front) ? state.front : '';
}

function openMicPopup() {
  const overlay = document.getElementById('micOverlay');
  if (!overlay) return;

  const target = getTargetText();
  document.getElementById('micTargetWord').textContent = target || '—';
  document.getElementById('micTargetSub').textContent =
    (state && state.sub) ? state.sub : ((state && state.back) ? state.back : '');

  micReset();
  overlay.classList.add('show');
}

function closeMicPopup() {
  micStop();
  const overlay = document.getElementById('micOverlay');
  if (overlay) overlay.classList.remove('show');
}

function micReset() {
  micStop();
  setMicStatus('Nhấn nút để bắt đầu');
  document.getElementById('micResultBox').classList.add('hidden');
  document.getElementById('micScoreWrap').classList.add('hidden');
  document.getElementById('micResultText').innerHTML = '';
  document.getElementById('micBarFill').style.width = '0%';
  document.getElementById('micBarFill').className = 'mic-bar-fill';
  document.getElementById('micScorePct').textContent = '0%';
  setMicRecordingUI(false);
}

function setMicStatus(msg) {
  const el = document.getElementById('micStatus');
  if (el) el.textContent = msg;
}

function setMicRecordingUI(recording) {
  micIsRecording = recording;
  const btn = document.getElementById('micStartBtn');
  const iconWrap = document.getElementById('micIconWrap');
  const ripple = document.getElementById('micRipple');
  if (btn) {
    btn.innerHTML = recording
      ? `<svg class="svg-ico" viewBox="0 0 24 24" style="width:18px;height:18px"><rect x="6" y="6" width="12" height="12" rx="2"/></svg>Dừng lại`
      : `<svg class="svg-ico" viewBox="0 0 24 24" style="width:18px;height:18px"><rect x="9" y="2" width="6" height="11" rx="3"/><path d="M5 10a7 7 0 0 0 14 0"/><line x1="12" y1="19" x2="12" y2="22"/><line x1="8" y1="22" x2="16" y2="22"/></svg>Bắt đầu`;
    btn.classList.toggle('recording', recording);
  }
  if (iconWrap) iconWrap.classList.toggle('recording', recording);
  if (ripple) ripple.classList.toggle('recording', recording);
}

function micToggle() {
  if (micIsRecording) {
    micStop();
  } else {
    micStart();
  }
}

function micStart() {
  const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
  if (!SpeechRecognition) {
    setMicStatus('Trình duyệt không hỗ trợ nhận diện giọng nói.');
    return;
  }

  micStop();

  micRecognition = new SpeechRecognition();
  micRecognition.lang = getMicLang();
  micRecognition.interimResults = false;
  micRecognition.maxAlternatives = 5;
  micRecognition.continuous = false;

  setMicRecordingUI(true);
  setMicStatus('Đang nghe... Hãy nói to và rõ ràng.');

  micRecognition.onresult = function(event) {
    const bestTranscript = chooseBestMicTranscript(event.results[0]);
    micStop();
    micShowResult(bestTranscript);
  };

  micRecognition.onerror = function(event) {
    micStop();
    if (event.error === 'not-allowed' || event.error === 'permission-denied') {
      setMicStatus('Vui lòng cho phép truy cập Microphone.');
    } else if (event.error === 'no-speech') {
      setMicStatus('Không phát hiện giọng nói. Thử lại nhé!');
    } else {
      setMicStatus('Lỗi: ' + event.error);
    }
  };

  micRecognition.onend = function() {
    if (micIsRecording) {
      setMicRecordingUI(false);
      setMicStatus('Không nhận được giọng nói. Thử lại nhé!');
    }
  };

  micRecognition.start();
}

function micStop() {
  if (micRecognition) {
    try { micRecognition.stop(); } catch (e) {}
    micRecognition = null;
  }
  setMicRecordingUI(false);
}

function micShowResult(spoken) {
  const target = getTargetText();
  if (!spoken) {
    setMicStatus('Không nhận được giọng nói. Thử lại nhé!');
    return;
  }

  setMicStatus('');

  const lang = getMicLang();
  const spokenNorm = normalizeText(spoken, lang);
  const targetNorm = normalizeText(target, lang);

  const score = calcSimilarity(spokenNorm, targetNorm);
  const pct = Math.round(score * 100);

  let htmlResult = '';
  if (isCjkLang(lang)) {
    const targetChars = [...targetNorm];
    const displayText = stripMicPunctuation(spoken).replace(/\s+/g, '');
    for (const ch of [...displayText]) {
      const comparable = lang.startsWith('zh') ? normalizeChineseForPronunciation(ch) : ch;
      const inTarget = targetChars.includes(comparable);
      htmlResult += `<span class="${inTarget ? 'word-ok' : 'word-wrong'}">${escapeHtml(ch)}</span>`;
    }
  } else {
    const targetWords = targetNorm.split(/\s+/);
    const spokenWords = spokenNorm.split(/\s+/);
    spokenWords.forEach(w => {
      const ok = targetWords.some(tw => tw === w || tw.includes(w) || w.includes(tw));
      htmlResult += `<span class="${ok ? 'word-ok' : 'word-wrong'}">${escapeHtml(w)}</span> `;
    });
  }

  document.getElementById('micResultText').innerHTML = htmlResult.trim() || escapeHtml(spoken);
  document.getElementById('micResultBox').classList.remove('hidden');

  const fillEl = document.getElementById('micBarFill');
  const pctEl = document.getElementById('micScorePct');
  fillEl.style.width = pct + '%';
  fillEl.className = 'mic-bar-fill' + (pct >= 70 ? ' high' : pct >= 40 ? '' : ' low');
  pctEl.textContent = pct + '%';
  pctEl.style.color = pct >= 70 ? '#10b981' : pct >= 40 ? '#e6e6f0' : '#ff5577';
  document.getElementById('micScoreWrap').classList.remove('hidden');

  if (pct === 100) {
    playMicSuccessAudio();
  }
}

function chooseBestMicTranscript(results) {
  if (!results || !results.length) return '';

  const lang = getMicLang();
  const targetNorm = normalizeText(getTargetText(), lang);
  let bestTranscript = '';
  let bestRank = -Infinity;

  for (let i = 0; i < results.length; i++) {
    const alt = results[i];
    const transcript = (alt.transcript || '').trim();
    const altNorm = normalizeText(transcript, lang);
    const confidence = Number.isFinite(alt.confidence) ? alt.confidence : 0;
    const similarity = targetNorm ? calcSimilarity(altNorm, targetNorm) : 0;
    const rank = similarity * 1000 + confidence;

    if (rank > bestRank) {
      bestRank = rank;
      bestTranscript = transcript;
    }
  }

  return bestTranscript;
}

function playMicSuccessAudio() {
  try {
    if (!micSuccessAudio) {
      micSuccessAudio = new Audio('audio/succes100.mp3');
      micSuccessAudio.preload = 'auto';
    }

    micSuccessAudio.currentTime = 0;
    const playPromise = micSuccessAudio.play();
    if (playPromise && typeof playPromise.catch === 'function') {
      playPromise.catch(() => {});
    }
  } catch (e) {}
}

function normalizeText(str, lang) {
  const value = stripMicPunctuation(str).toLowerCase();
  if (isCjkLang(lang)) {
    const compact = value.replace(/\s+/g, '');
    return lang && lang.startsWith('zh') ? normalizeChineseForPronunciation(compact) : compact;
  }

  return value.replace(/\s+/g, ' ').trim();
}

function stripMicPunctuation(str) {
  return String(str || '')
    .normalize('NFKC')
    .replace(/[\u200b-\u200f\ufeff]/g, '')
    .replace(/[.,!?;:'"()[\]{}<>，。！？、；：「」『』（）《》〈〉【】〔〕—…·“”‘’\-_\/\\|~`@#$%^&*+=]/g, '')
    .trim();
}

function normalizeChineseForPronunciation(str) {
  return [...String(str || '')]
    .map(ch => CHINESE_PRONUNCIATION_EQUIV[ch] || ch)
    .join('');
}

function isCjkLang(lang) {
  const value = String(lang || '').toLowerCase();
  return value.startsWith('zh') || value.startsWith('ja');
}

function escapeHtml(str) {
  return String(str || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function calcSimilarity(a, b) {
  if (!a && !b) return 1;
  if (!a || !b) return 0;
  if (a === b) return 1;
  const la = [...a], lb = [...b];
  const m = la.length, n = lb.length;
  const dp = Array.from({ length: m + 1 }, (_, i) =>
    Array.from({ length: n + 1 }, (_, j) => i === 0 ? j : j === 0 ? i : 0)
  );
  for (let i = 1; i <= m; i++) {
    for (let j = 1; j <= n; j++) {
      dp[i][j] = la[i - 1] === lb[j - 1]
        ? dp[i - 1][j - 1]
        : 1 + Math.min(dp[i - 1][j], dp[i][j - 1], dp[i - 1][j - 1]);
    }
  }
  const dist = dp[m][n];
  const maxLen = Math.max(m, n);
  return maxLen === 0 ? 1 : Math.max(0, 1 - dist / maxLen);
}

document.addEventListener('click', e => {
  const overlay = document.getElementById('micOverlay');
  if (overlay && e.target === overlay) closeMicPopup();
});
