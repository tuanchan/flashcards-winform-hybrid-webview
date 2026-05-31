const fs = require('fs');
const files = ['quiz-feature.html', 'quiz-essay.html', 'dialogue-feature.html'];

files.forEach(f => {
    if (!fs.existsSync(f)) return;
    let content = fs.readFileSync(f, 'utf8');
    content = content.replace(/window\.chrome\.webview\.postMessage\(JSON\.stringify\(\{action:'goHome'\}\)\)/g, "window.chrome.webview.postMessage({action:'goHome'})");
    fs.writeFileSync(f, content);
    console.log('Fixed ' + f);
});
