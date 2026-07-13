// PostToolUse(Edit|Write) hook — C# 8+ syntax and HALCON SetColor style warn.
// Non-blocking, added-line-only: for Edit events it scans ONLY new_string
// (never the whole file, to avoid false positives against legacy code).
// Warns when C# 8+ syntax (project is pinned to C# 7.2) or a non-standard
// HALCON SetColor color literal (silently swallowed exception -> no
// render, a past incident in this project) appears in the added text.

let input = '';
const stdinTimeout = setTimeout(() => process.exit(0), 3000);
process.stdin.setEncoding('utf8');
process.stdin.on('data', chunk => input += chunk);
process.stdin.on('end', () => {
  clearTimeout(stdinTimeout);
  try {
    const data = JSON.parse(input);
    const filePath = data.tool_input && data.tool_input.file_path;

    if (typeof filePath !== 'string' || !/\.cs$/i.test(filePath)) {
      process.exit(0);
    }

    let scannedText = '';
    if (data.tool_name === 'Write') {
      scannedText = data.tool_input && data.tool_input.content;
    } else if (data.tool_name === 'Edit') {
      scannedText = data.tool_input && data.tool_input.new_string;
    }

    if (typeof scannedText !== 'string' || scannedText.length === 0) {
      process.exit(0);
    }

    const warnings = [];

    // Check A: C# 8+ syntax not allowed under C# 7.2 (CLAUDE.md constraint).
    const csharp8Patterns = [
      { regex: /\brecord\s+\w+/, label: 'record type declaration' },
      { regex: /\?\?=/, label: 'null-coalescing assignment (??=)' },
      { regex: /\busing\s+var\b/, label: 'using var declaration' }
    ];

    for (let i = 0; i < csharp8Patterns.length; i++) {
      const entry = csharp8Patterns[i];
      if (entry.regex.test(scannedText)) {
        warnings.push('C# 8+ syntax detected (' + entry.label + ') — project is pinned to C# 7.2 per CLAUDE.md.');
      }
    }

    // Check B: HALCON SetColor with a non-standard color literal. An
    // invalid color name throws inside HOperatorSet, gets swallowed by
    // the project's bare catch pattern, and the overlay silently fails
    // to render (past incident).
    const whitelist = [
      'red', 'green', 'blue', 'cyan', 'magenta', 'yellow', 'white', 'black',
      'orange', 'gray', 'grey', 'slate blue', 'brown', 'pink', 'violet',
      'light gray', 'dark gray', 'forest green', 'gold', 'coral'
    ];

    const setColorRegex = /SetColor\s*\([^)]*"([^"]+)"\s*\)/g;
    let colorMatch;
    while ((colorMatch = setColorRegex.exec(scannedText)) !== null) {
      const colorLiteral = colorMatch[1];
      const lowerLiteral = colorLiteral.toLowerCase();
      const isHex = /^#[0-9A-Fa-f]{6}$/.test(colorLiteral);
      let isWhitelisted = false;
      for (let j = 0; j < whitelist.length; j++) {
        if (whitelist[j] === lowerLiteral) {
          isWhitelisted = true;
          break;
        }
      }
      if (!isWhitelisted && !isHex) {
        warnings.push('Non-standard HALCON SetColor literal "' + colorLiteral + '" — invalid color names throw ' +
          'and get silently swallowed, causing a no-render bug.');
      }
    }

    if (warnings.length === 0) {
      process.exit(0);
    }

    const bulletList = warnings.map(function (w) { return '- ' + w; }).join('\n');

    const output = {
      hookSpecificOutput: {
        hookEventName: 'PostToolUse',
        additionalContext: 'CS STYLE WARNING for ' + filePath + ':\n' + bulletList
      }
    };

    process.stdout.write(JSON.stringify(output));
    process.exit(0);
  } catch (e) {
    // Any unexpected error — fail open, silent.
    process.exit(0);
  }
});
