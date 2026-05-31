const fs = require('fs');

function replaceFile(file, replacements) {
    if (!fs.existsSync(file)) return;
    let content = fs.readFileSync(file, 'utf8');
    for (const [search, replace] of replacements) {
        content = content.replace(search, replace);
    }
    fs.writeFileSync(file, content);
    console.log('Fixed ' + file);
}

// 1. unified-ui.css
replaceFile('src/unified-ui.css', [
    [/\.feature-head\s*\{/, '.feature-head {\n  position: sticky;\n  top: 0;\n  left: 0;\n  right: 0;\n  width: 100%;\n  box-sizing: border-box;\n  z-index: 10000;']
]);

// 2. flashcards-base.css
replaceFile('src/flashcards-base.css', [
    [/height:\s*100vh;/, 'flex: 1; min-height: 0;'],
    [/(body\s*\{[^}]*?)(})/, '$1  display: flex;\n  flex-direction: column;\n  height: 100vh;\n  margin: 0;\n$2']
]);

// 3. quiz-essay.css
replaceFile('src/quiz-essay.css', [
    [/height:\s*100vh;/, 'flex: 1; min-height: 0;'],
    [/(body\s*\{[^}]*?)(})/, '$1  display: flex;\n  flex-direction: column;\n  height: 100vh;\n  margin: 0;\n$2']
]);

// 4. dialogue-feature.css
replaceFile('src/dialogue-feature.css', [
    [/height:\s*100vh;/, 'flex: 1; min-height: 0;'],
    [/(body\s*\{[^}]*?)(})/, '$1  display: flex;\n  flex-direction: column;\n  height: 100vh;\n  margin: 0;\n$2']
]);

// 5. card-import.css
replaceFile('src/card-import.css', [
    [/\.import-form\s*\{[\s\S]*?height:\s*100%;/, match => match.replace(/height:\s*100%;/, 'flex: 1; min-height: 0;')],
    [/(body\s*\{[^}]*?)(})/, '$1  display: flex;\n  flex-direction: column;\n  height: 100vh;\n  margin: 0;\n$2']
]);

