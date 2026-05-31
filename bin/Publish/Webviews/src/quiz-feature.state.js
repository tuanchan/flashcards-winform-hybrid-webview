function post(action, data = {}) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage({ action, data });
    }
}

const quizHost = document.getElementById("quizHost");
const progressText = document.getElementById("progressText");
const setTitle = document.getElementById("setTitle");
const pageContent = document.getElementById("pageContent");
const footerContent = document.getElementById("footerContent");

const setupOverlay = document.getElementById("setupOverlay");
const setupSetTitle = document.getElementById("setupSetTitle");
const setupClose = document.getElementById("setupClose");
const lblMax = document.getElementById("lblMax");
const inpCount = document.getElementById("inpCount");
const selAnswer = document.getElementById("selAnswer");
const swMulti = document.getElementById("swMulti");
const swEssay = document.getElementById("swEssay");
const swSentence = document.getElementById("swSentence");
const btnStart = document.getElementById("btnStart");

const ddAnswer = document.getElementById("ddAnswer");
const ddAnswerBtn = document.getElementById("ddAnswerBtn");
const ddAnswerText = document.getElementById("ddAnswerText");
const ddAnswerMenu = document.getElementById("ddAnswerMenu");

let answerModeOptions = [
    { value: 0, text: "Ngôn ngữ gốc" },
    { value: 1, text: "Tiếng Việt" },
    { value: 2, text: "Cả hai" }
];

const footerCard = document.getElementById("footerCard");
const footerTitle = document.getElementById("footerTitle");
const footerBtnPrimary = document.getElementById("footerBtnPrimary");
const footerBtnGhost = document.getElementById("footerBtnGhost");

const resultOverlay = document.getElementById("resultOverlay");
const resSetTitle = document.getElementById("resSetTitle");
const resClose = document.getElementById("resClose");
const resOk = document.getElementById("resOk");
const resBad = document.getElementById("resBad");
const resTime = document.getElementById("resTime");
const donut = document.getElementById("donut");
const btnViewResult = document.getElementById("btnViewResult");
const btnExit = document.getElementById("btnExit");

const sentenceGradeOverlay = document.getElementById("sentenceGradeOverlay");
const sentenceGradeSetTitle = document.getElementById("sentenceGradeSetTitle");
const sentenceGradeClose = document.getElementById("sentenceGradeClose");
const btnSentenceGradeLocal = document.getElementById("btnSentenceGradeLocal");
const btnSentenceGradeGemini = document.getElementById("btnSentenceGradeGemini");

let totalQuestions = 0;
let currentThemeDark = false;
