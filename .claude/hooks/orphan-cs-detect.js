// PostToolUse(Write) hook — orphan .cs detection.
// Warns (non-blocking) when a newly-written .cs file is not registered
// under <Compile Include="..."> in DatumMeasurement.csproj, meaning it
// will not actually compile into the build.

const fs = require('fs');
const path = require('path');

const PROJECT_ROOT = process.env.CLAUDE_PROJECT_DIR || 'C:/Info/Project/DataMeasurement';
const CSPROJ_PATH = path.join(PROJECT_ROOT, 'WPF_Example', 'DatumMeasurement.csproj');

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

    // Skip dated backup files (e.g. Action_BottomInspection_0428.cs).
    // CLAUDE.md: these are dead weight, not compiled, and use the
    // un-dated filename instead.
    const stem = path.basename(filePath, path.extname(filePath));
    if (/_\d{3,4}$/.test(stem)) {
      process.exit(0);
    }

    if (!fs.existsSync(CSPROJ_PATH)) {
      process.exit(0);
    }

    const csprojText = fs.readFileSync(CSPROJ_PATH, 'utf8');
    const includeSet = new Set();
    const includeRegex = /<Compile\s+Include="([^"]+)"/g;
    let match;
    while ((match = includeRegex.exec(csprojText)) !== null) {
      const normalized = match[1].toLowerCase().replace(/\\/g, '/');
      includeSet.add(normalized);
    }

    // Compute the written file's path relative to WPF_Example/.
    const normalizedFilePath = filePath.toLowerCase().replace(/\\/g, '/');
    const anchor = 'wpf_example/';
    const anchorIndex = normalizedFilePath.indexOf(anchor);

    if (anchorIndex === -1) {
      // File is not under WPF_Example/ at all — not a csproj concern.
      process.exit(0);
    }

    const relativePath = normalizedFilePath.substring(anchorIndex + anchor.length);

    if (includeSet.has(relativePath)) {
      process.exit(0);
    }

    const output = {
      hookSpecificOutput: {
        hookEventName: 'PostToolUse',
        additionalContext: 'ORPHAN .cs WARNING: "' + relativePath + '" is not listed under ' +
          '<Compile Include> in WPF_Example/DatumMeasurement.csproj. It will NOT compile into ' +
          'the build until it is added there.'
      }
    };

    process.stdout.write(JSON.stringify(output));
    process.exit(0);
  } catch (e) {
    // Any unexpected error — fail open, silent.
    process.exit(0);
  }
});
