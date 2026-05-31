const fs = require('fs');

const files = [
  'flashcards-feature.html',
  'quiz-feature.html',
  'quiz-essay.html',
  'dialogue-feature.html',
  'card-import.html'
];

files.forEach(f => {
  let content = fs.readFileSync(f, 'utf8');
  
  // Find feature-head block
  const match = content.match(/<div class="feature-head">[\s\S]*?<\/div>\s*<\/div>/);
  if (match) {
    // Remove from current position
    content = content.replace(match[0], '');
    
    // Insert right after <body>
    content = content.replace(/<body>/i, '<body>\n  ' + match[0]);
    
    fs.writeFileSync(f, content);
    console.log('Fixed HTML for ' + f);
  } else {
    console.log('Not found in ' + f);
  }
});
