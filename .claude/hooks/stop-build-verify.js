// Stop hook — MSBuild Debug|x64 gate.
// Only builds when *.cs files are dirty in git status. Blocks the Stop
// event via {"decision":"block"} when the build reports "error CS".
// Fails open (exit 0) on any missing tool, timeout, or unexpected error
// so the session is never bricked by environment drift.

const fs = require('fs');
const path = require('path');
const { execFileSync, spawnSync } = require('child_process');

const PROJECT_ROOT = process.env.CLAUDE_PROJECT_DIR || 'C:/Info/Project/DataMeasurement';
const CSPROJ_PATH = path.join(PROJECT_ROOT, 'WPF_Example', 'DatumMeasurement.csproj');
const MSBUILD_EXE = 'C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe';

let input = '';
const stdinTimeout = setTimeout(() => process.exit(0), 3000);
process.stdin.setEncoding('utf8');
process.stdin.on('data', chunk => input += chunk);
process.stdin.on('end', () => {
  clearTimeout(stdinTimeout);
  try {
    const data = JSON.parse(input);

    // Infinite-loop guard: if this Stop hook already fired once for this
    // turn, do not fire again.
    if (data.stop_hook_active === true) {
      process.exit(0);
    }

    // Gate: only build when *.cs files show as dirty. Avoids building
    // every single turn regardless of what changed.
    let gitOutput = '';
    try {
      gitOutput = execFileSync('git', ['status', '--porcelain', '--', '*.cs'], {
        cwd: PROJECT_ROOT,
        encoding: 'utf8',
        timeout: 10000
      });
    } catch (e) {
      // git not available or repo error — fail open, do not build.
      process.exit(0);
    }

    if (!gitOutput || gitOutput.trim().length === 0) {
      process.exit(0);
    }

    // MSBuild missing at expected path — fail open.
    if (!fs.existsSync(MSBUILD_EXE)) {
      process.stderr.write('stop-build-verify: MSBuild.exe not found at expected path, skipping build gate.\n');
      process.exit(0);
    }

    const buildArgs = [
      CSPROJ_PATH,
      '-p:Configuration=Debug',
      '-p:Platform=x64',
      '-nologo',
      '-v:m',
      '-clp:ErrorsOnly'
    ];

    let result;
    try {
      result = spawnSync(MSBUILD_EXE, buildArgs, {
        cwd: PROJECT_ROOT,
        encoding: 'utf8',
        timeout: 60000
      });
    } catch (e) {
      process.stderr.write('stop-build-verify: MSBuild spawn threw, failing open: ' + e.message + '\n');
      process.exit(0);
    }

    if (result.error) {
      process.stderr.write('stop-build-verify: MSBuild spawn error, failing open: ' + result.error.message + '\n');
      process.exit(0);
    }

    if (result.status === null) {
      // Killed by timeout (or signal) — fail open.
      process.stderr.write('stop-build-verify: MSBuild timed out, failing open.\n');
      process.exit(0);
    }

    const combinedOutput = (result.stdout || '') + (result.stderr || '');

    if (combinedOutput.indexOf('error CS') === -1) {
      // Build clean (or failed for a non-CS reason) — allow stop.
      process.exit(0);
    }

    const errorLines = combinedOutput
      .split(/\r?\n/)
      .filter(line => line.indexOf('error CS') !== -1)
      .slice(0, 10);

    const reason = 'MSBuild Debug|x64 reported C# compile errors:\n' + errorLines.join('\n');

    const output = { decision: 'block', reason: reason };
    console.log(JSON.stringify(output));
    process.exit(0);
  } catch (e) {
    // Any unexpected error — fail open, never block the session.
    process.exit(0);
  }
});
