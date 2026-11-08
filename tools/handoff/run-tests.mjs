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

// Unity 콘솔은 -logFile로 Unity.log에 그대로 남으므로 stdout/stderr로 중계하지 않습니다.
// 중계하면 이 명령을 check로 돌리는 로컬 모델 워커의 컨텍스트가 Unity 로그 수천 줄로
// 가득 차 버립니다(2026-11-07 94% 사례). QUIET=0으로 중계를 되살릴 수 있습니다.
let ok = false;
try {
  const stdio = process.env.QUIET === '0' ? 'inherit' : 'ignore';
  const run = spawnSync(unity, args, { cwd: root, stdio });
  record.unityExit = run.status;
  if (run.error) throw run.error;
  const xml = readFileSync(join(out, 'TestResults.xml'), 'utf8');
  const total = Number((xml.match(/<test-run\b[^>]*\btotal="(\d+)"/) || [])[1] ?? 0);
  const passed = /<test-run\b[^>]*\bresult="Passed"/.test(xml) && /<test-case\b/.test(xml);
  record.totalTests = total;
  ok = passed && total > 0;
  record.result = ok ? 'passed' : 'failed';
  if (!ok && total === 0) record.reason = 'no test matched the filter';
  // 실패 요약을 압축해서 추출합니다: 실패한 test-case의 이름 + 메시지 첫 줄만, 최대 20건.
  // 재시도 워커에게 넘길 실패 정보는 이 요약뿐이라 Unity 로그 전체가 컨텍스트로 들어가지 않습니다.
  if (!ok && total > 0) {
    const fails = [];
    // 실패한 test-case 블록을 하나씩 떼어, 그 안의 첫 <message>만 짧게 가져옵니다.
    const caseRe = /<test-case\b[^>]*\bresult="(?:Failed|Error)"[\s\S]*?<\/test-case>/g;
    let block;
    while ((block = caseRe.exec(xml)) && fails.length < 20) {
      const chunk = block[0];
      const name = (chunk.match(/\bname="([^"]*)"/) || [])[1] || '?';
      const raw = (chunk.match(/<message>([\s\S]*?)<\/message>/) || [])[1] || '';
      const msg = raw.replace(/<!\[CDATA\[|\]\]>/g, '').trim().split('\n')[0].slice(0, 160);
      fails.push(`- ${name.split('.').pop()}: ${msg}`);
    }
    record.failedSample = fails;
  }
} catch (error) {
  record.result = 'error'; record.error = error.message;
}
record.completedUtc = new Date().toISOString();
writeFileSync(join(out, 'RunRecord.json'), JSON.stringify(record, null, 2) + '\n');
console.log(`${record.result} (${record.totalTests ?? 0} tests) — evidence: ${out}`);
if (record.failedSample && record.failedSample.length) {
  console.log(`failed tests (${record.failedSample.length} shown):`);
  for (const line of record.failedSample) console.log(line);
}
process.exit(ok ? 0 : 1);
