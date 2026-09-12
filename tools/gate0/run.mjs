import { spawnSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync, cpSync, readdirSync, statSync } from 'node:fs';
import { resolve, join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { build } from 'esbuild';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const unity = process.env.UNITY_EDITOR || '/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity';
const action = process.argv[2];
const valid = ['prepare','prepare-contracts','validate-contracts','test-edit','test-play','build-android','build-webgl','package-toss'];
if (!valid.includes(action)) throw new Error(`Choose ${valid.join(', ')}`);
if (Number(process.versions.node.split('.')[0]) < 24) throw new Error('Node 24 or newer required by the installed AIT CLI.');
const id = new Date().toISOString().replace(/[:.]/g, '-') + '-' + action;
const out = join(root, 'Artifacts/T2', id);
mkdirSync(out, { recursive:true });
const record = { id, action, startedUtc:new Date().toISOString(), scope:'T2 diagnostic only', gateEligible:false,
  source:spawnSync('git',['rev-parse','HEAD'],{cwd:root,encoding:'utf8'}).stdout.trim(),
  dirty:spawnSync('git',['status','--porcelain'],{cwd:root,encoding:'utf8'}).stdout.trim().length > 0,
  node:process.versions.node };
if(action.endsWith('-contracts')) record.scope = 'T3 configuration only; not a measured Gate result';
if(action.startsWith('test-')) record.scope = 'Unity automated tests only; not device measurement';
function run(command,args,env={}) {
  const result = spawnSync(command,args,{cwd:root,stdio:'inherit',env:{...process.env,...env}});
  if (result.error) throw result.error;
  if (result.status !== 0) throw new Error(`${command} exited ${result.status}`);
}
function hashes(directory,prefix='') {
  return readdirSync(directory).sort().flatMap(name => {
    const path=join(directory,name), key=prefix+name;
    return statSync(path).isDirectory() ? hashes(path,key+'/') : [{path:key,sha256:createHash('sha256').update(readFileSync(path)).digest('hex')}];
  });
}
try {
  if (action === 'package-toss') {
    if (!process.argv[3]) throw new Error('Supply the successful T2 WebGL build directory.');
    const input=resolve(root,process.argv[3]);
    const report=JSON.parse(readFileSync(join(input,'BuildReport.json'),'utf8'));
    if(report.result !== 'Succeeded' || report.target !== 'WebGL') throw new Error('Expected a successful WebGL BuildReport.');
    const dest=join(out,'WebGL');
    cpSync(input,dest,{recursive:true});
    const htmlPath=join(dest,'index.html');
    let html=readFileSync(htmlPath,'utf8');
    if(!html.includes('window.gate0Smoke')) throw new Error('Expected Gate0Smoke WebGL template.');
    await build({entryPoints:[join(root,'tools/gate0/toss-orientation.js')],bundle:true,format:'iife',platform:'browser',target:'es2020',outfile:join(dest,'toss-orientation.js')});
    html=html.replace('</body>','<script src="toss-orientation.js"></script>\n</body>');
    writeFileSync(htmlPath,html);
    record.appName='mallang-brain';
    record.consoleAppNameVerified=true;
    run(process.execPath,[join(root,'node_modules/@apps-in-toss/web-framework/bin/ait.js'),'build'],{GATE0_WEB_BUNDLE:dest});
    cpSync(join(root,`${record.appName}.ait`),join(out,`${record.appName}.ait`));
    record.sourceWebGL=input;
  } else {
    const args=['-batchmode','-projectPath',join(root,'unity'),'-logFile',join(out,'Unity.log')];
    if(action.startsWith('test-')) args.push('-runTests','-testPlatform',action==='test-edit'?'EditMode':'PlayMode','-testResults',join(out,'TestResults.xml'));
    else {
      const method=action==='validate-contracts'?'Gate0ContractSetup.ValidateMeasurement':action==='prepare-contracts'?'Gate0ContractSetup.Prepare':action==='prepare'?'SmokeSceneSetup.Prepare':action==='build-android'?'SmokeBuild.BuildAndroid':'SmokeBuild.BuildWebGL';
      args.push('-executeMethod','Mallang.Gate0.Editor.Build.'+method,'-quit');
      if(action.startsWith('build-')) args.push('-buildTarget',action==='build-android'?'Android':'WebGL');
    }
    record.command=[unity,...args];
    run(unity,args,{GATE0_BUILD_DIR:out});
    if(action.startsWith('test-')) {
      const xml=readFileSync(join(out,'TestResults.xml'),'utf8');
      if(!/<test-run\b[^>]*result="Passed"/.test(xml) || !/<test-case\b/.test(xml)) throw new Error('No passing Unity test run was recorded.');
    }
  }
  record.result='succeeded';
} catch(error) {
  record.result='failed'; record.error=error.message; process.exitCode=1; console.error(error.message);
} finally {
  record.completedUtc=new Date().toISOString(); record.files=hashes(out);
  writeFileSync(join(out,'SmokeRecord.json'),JSON.stringify(record,null,2),{flag:'wx'});
  console.log(`T2 evidence: ${out}`);
}
