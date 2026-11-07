// 001-listen-and-repeat-core 인계 구현의 로직 레인 check 러너.
// Unity 배치모드로 테스트를 돌리고, TestResults.xml을 직접 확인해 종료 코드로 알립니다.
// 종료 코드: 0 = 테스트 1개 이상 실행 + 전부 통과 / 1 = 그 외 전부(실패·테스트 없음·실행 오류)
//
// 사용법: node tools/handoff/run-tests.mjs <edit|play> [testFilter]
//   testFilter는 Unity -testFilter에 그대로 전달됩니다(테스트 전체 이름에 대한
//   정규식 부분 일치, 세미콜론으로 여러 개). 생략하면 해당 플랫폼 전체를 돌립니다.
// 주의: 배치모드는 같은 프로젝트를 연 에디터와 동시에 못 돕니다. 에디터를 닫고 실행하세요.
import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve, join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const unity = process.env.UNITY_EDITOR || '/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity';
const platform = { edit: 'EditMode', play: 'PlayMode' }[process.argv[2]];
if (!platform) { console.error('Choose edit or play'); process.exit(1); }
const filter = process.argv[3] || '';

const id = new Date().toISOString().replace(/[:.]/g, '-') + '-' + platform + (filter ? '-' + filter.replace(/[^A-Za-z0-9]+/g, '_').slice(0, 60) : '');
const out = join(root, 'Artifacts/Handoff001', id);
mkdirSync(out, { recursive: true });
const record = { id, platform, filter, startedUtc: new Date().toISOString(),
  scope: 'Unity automated tests only; logic-lane check for specs/001-listen-and-repeat-core',
  source: spawnSync('git', ['rev-parse', 'HEAD'], { cwd: root, encoding: 'utf8' }).stdout.trim(),
  dirty: spawnSync('git', ['status', '--porcelain'], { cwd: root, encoding: 'utf8' }).stdout.trim().length > 0 };

const args = ['-batchmode', '-projectPath', join(root, 'unity'),
  '-runTests', '-testPlatform', platform,
  '-testResults', join(out, 'TestResults.xml'), '-logFile', join(out, 'Unity.log')];
if (filter) args.push('-testFilter', filter);
record.command = [unity, ...args];

let ok = false;
try {
  const run = spawnSync(unity, args, { cwd: root, stdio: 'inherit' });
  record.unityExit = run.status;
  if (run.error) throw run.error;
  const xml = readFileSync(join(out, 'TestResults.xml'), 'utf8');
  const total = Number((xml.match(/<test-run\b[^>]*\btotal="(\d+)"/) || [])[1] ?? 0);
  const passed = /<test-run\b[^>]*\bresult="Passed"/.test(xml) && /<test-case\b/.test(xml);
  record.totalTests = total;
  ok = passed && total > 0;
  record.result = ok ? 'passed' : 'failed';
  if (!ok && total === 0) record.reason = 'no test matched the filter';
} catch (error) {
  record.result = 'error'; record.error = error.message;
}
record.completedUtc = new Date().toISOString();
writeFileSync(join(out, 'RunRecord.json'), JSON.stringify(record, null, 2) + '\n');
console.log(`${record.result} (${record.totalTests ?? 0} tests) — evidence: ${out}`);
process.exit(ok ? 0 : 1);
