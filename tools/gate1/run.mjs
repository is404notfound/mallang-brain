import { spawnSync } from 'node:child_process';
import { cpSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { resolve, join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { build } from 'esbuild';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const unity = process.env.UNITY_EDITOR || '/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity';
const action = process.argv[2];
if (!['build-webgl', 'package-toss'].includes(action)) throw new Error('Choose build-webgl or package-toss');
if (Number(process.versions.node.split('.')[0]) < 24) throw new Error('Node 24 or newer is required by the installed AIT CLI.');

const id = new Date().toISOString().replace(/[:.]/g, '-') + '-' + action;
const out = join(root, 'Artifacts/Gate1', id);
mkdirSync(out, { recursive:true });
const record = { id, action, startedUtc:new Date().toISOString(), scope:'Gate 1 playable prototype; not a Gate 0 measurement artifact', gateEligible:false,
  source:spawnSync('git',['rev-parse','HEAD'],{cwd:root,encoding:'utf8'}).stdout.trim(),
  dirty:spawnSync('git',['status','--porcelain'],{cwd:root,encoding:'utf8'}).stdout.trim().length > 0,
  node:process.versions.node };

function run(command, args, env = {}) {
  const result = spawnSync(command, args, { cwd:root, stdio:'inherit', env:{...process.env, ...env} });
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${command} exited ${result.status}`);
}

function hashes(directory, prefix = '') {
  return readdirSync(directory).sort().flatMap(name => {
    const path = join(directory, name), key = prefix + name;
    return statSync(path).isDirectory() ? hashes(path, key + '/') : [{ path:key, sha256:createHash('sha256').update(readFileSync(path)).digest('hex') }];
  });
}

try {
  if (action === 'build-webgl') {
    run(unity, ['-batchmode', '-projectPath', join(root, 'unity'), '-executeMethod', 'Mallang.Gate1.Editor.Build.ContestBuild.BuildWebGL', '-quit', '-buildTarget', 'WebGL', '-logFile', join(out, 'Unity.log')], { GATE1_BUILD_DIR:out });
    const report = JSON.parse(readFileSync(join(out, 'WebGL', 'BuildReport.json'), 'utf8'));
    if (report.result !== 'Succeeded' || report.target !== 'WebGL') throw new Error('No successful Gate 1 WebGL build was recorded.');
  } else {
    if (!process.argv[3]) throw new Error('Supply the successful Gate 1 WebGL build directory.');
    const input = resolve(root, process.argv[3]);
    const report = JSON.parse(readFileSync(join(input, 'BuildReport.json'), 'utf8'));
    if (report.result !== 'Succeeded' || report.target !== 'WebGL') throw new Error('Expected a successful Gate 1 WebGL BuildReport.');
    const destination = join(out, 'WebGL');
    cpSync(input, destination, { recursive:true });
    const htmlPath = join(destination, 'index.html');
    let html = readFileSync(htmlPath, 'utf8');
    if (!html.includes('window.gate1Prototype')) throw new Error('Expected the Gate1Prototype WebGL template.');
    await build({ entryPoints:[join(root, 'tools/gate1/toss-orientation.js')], bundle:true, format:'iife', platform:'browser', target:'es2020', outfile:join(destination, 'toss-orientation.js') });
    html = html.replace('</body>', '<script src="toss-orientation.js"></script>\n</body>');
    writeFileSync(htmlPath, html);
    run(process.execPath, [join(root, 'node_modules/@apps-in-toss/web-framework/bin/ait.js'), 'build'], { GATE1_WEB_BUNDLE:destination });
    cpSync(join(root, 'mallang-brain.ait'), join(out, 'mallang-brain.ait'));
    record.sourceWebGL = input;
  }
  record.result = 'succeeded';
} catch (error) {
  record.result = 'failed'; record.error = error.message; process.exitCode = 1; console.error(error.message);
} finally {
  record.completedUtc = new Date().toISOString(); record.files = hashes(out);
  writeFileSync(join(out, 'BuildRecord.json'), JSON.stringify(record, null, 2), { flag:'wx' });
  console.log(`Gate 1 build evidence: ${out}`);
}
