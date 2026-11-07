// worker.md 틀의 {{FEATURE}}, {{LAST_FAILURE}} 두 자리표시자를 채워 찍습니다.
// 사용법: node harness/render-prompt.js '<기능 JSON 한 줄>' <직전 실패 파일 경로>
const fs = require("fs");
const path = require("path");
const feature = JSON.parse(process.argv[2]);
const failfile = process.argv[3];
const tmpl = fs.readFileSync(path.join(__dirname, "worker.md"), "utf8");
const featureText = [
  `${feature.id}: ${feature.title}`,
  ...(feature.steps || []).map((s) => `- ${s}`),
  `check: ${feature.check}`,
].join("\n");
const lastFailure =
  failfile && fs.existsSync(failfile) ? fs.readFileSync(failfile, "utf8").trim() : "";
process.stdout.write(
  tmpl.replace("{{FEATURE}}", featureText).replace("{{LAST_FAILURE}}", lastFailure)
);
