#!/usr/bin/env bash
# 글루 스크립트(palolo 적응판): RR02 로직 레인 기능을 하나씩 로컬 모델에 맡기고,
# check(Unity 배치모드 테스트) 종료 코드로만 통과를 판정합니다. 스크립트는 판단하지 않습니다.
#
# 원본: apprentice-harness/driftfish/harness/run.sh. 적응 내용은 docs/03-development/handoff-001-environment.md 참고.
#  - 작업 영역 project/ → unity/Assets/Mallang/RR02/Runtime/
#  - 보호 경로에 RR02 Tests/Data/Screens와 RhythmCore·Gate0·Gate1·tools·계획 문서 추가
#  - 회귀 재검사: 통과 기능의 필터를 세미콜론으로 합쳐 Unity 1회 실행(에디터 기동 비용 절약)
#  - 상한: 로컬 2회(B 방식), 시도 1800초, check 900초
#
# 종료 코드: 0 전부 통과 / 1 준비 실패 / 2 막힌 기능 있음 / 3 전체 시도 상한 도달
# 주의: 실행 전 Unity 에디터를 닫으세요(배치모드와 동시 실행 불가).
set -uo pipefail  # -e는 쓰지 않습니다. 검사 실패(비0 종료)가 정상 흐름이기 때문입니다.

cd "$(cd "$(dirname "$0")/.." && pwd)"
source harness/env.sh

MAX_ATTEMPTS_PER_FEATURE="${MAX_ATTEMPTS_PER_FEATURE:-2}"
TRY_TIMEOUT_SECONDS="${TRY_TIMEOUT_SECONDS:-1800}"
CHECK_TIMEOUT_SECONDS="${CHECK_TIMEOUT_SECONDS:-900}"
MAX_TOTAL_TRIES="${MAX_TOTAL_TRIES:-30}"

WORK_AREA="unity/Assets/Mallang/RR02/Runtime"
PROTECTED_GIT=(
  "unity/Assets/Mallang/RR02/Tests"
  "unity/Assets/Mallang/RR02/Data"
  "unity/Assets/Mallang/RR02/Screens"
  "unity/Assets/Mallang/RhythmCore"
  "unity/Assets/Mallang/Gate0"
  "unity/Assets/Mallang/Gate1"
  harness tools "docs/03-development/handoff-001"
)
SNAP=.features.pre

log() { printf '%s %s\n' "$(date '+%F %T')" "$*" >> progress.log; }

# macOS에 timeout 명령이 없어 perl alarm으로 시간 상한을 겁니다. SIGALRM이면 142.
with_timeout() { perl -e 'alarm shift; exec @ARGV' "$@"; }

next_feature() {
  node -e 'const f=require(process.cwd()+"/features.json").features.find(x=>!x.passes&&!x.blocked);
           if(f)process.stdout.write(JSON.stringify(f));'
}

field() { node -pe 'JSON.parse(process.argv[1])[process.argv[2]] ?? ""' "$1" "$2"; }

mark() { # $1=id $2=pass|fail
  node - "$1" "$2" "$MAX_ATTEMPTS_PER_FEATURE" <<'EOF'
const fs = require("fs");
const [id, result, limit] = process.argv.slice(2);
const d = JSON.parse(fs.readFileSync("features.json", "utf8"));
const f = d.features.find(x => x.id === id);
if (result === "pass") f.passes = true;
else { f.attempts += 1; if (f.attempts >= Number(limit)) f.blocked = true; }
fs.writeFileSync("features.json", JSON.stringify(d, null, 2) + "\n");
EOF
}

revert_protected() {
  git checkout -q -- "${PROTECTED_GIT[@]}" 2>/dev/null || true
  git clean -fdq -- "${PROTECTED_GIT[@]}" 2>/dev/null || true
}
protected_dirty() {
  [ -n "$(git status --porcelain -- "${PROTECTED_GIT[@]}" 2>/dev/null)" ] && return 0
  [ -f "$SNAP" ] && ! cmp -s features.json "$SNAP"
}
revert_project() {
  git checkout -q -- "$WORK_AREA" 2>/dev/null || true
  git clean -fdq -- "$WORK_AREA" 2>/dev/null || true
}

# 회귀 재검사: 통과한 기능들의 filter를 세미콜론으로 합쳐 Unity 1회로 돌립니다.
regression_check() {
  local filters
  filters="$(node -e 'const fs=require(process.cwd()+"/features.json").features.filter(x=>x.passes&&x.filter).map(x=>x.filter);
                      if(fs.length)process.stdout.write(fs.join(";"));')"
  [ -z "$filters" ] && return 0
  with_timeout "$CHECK_TIMEOUT_SECONDS" node tools/handoff/run-tests.mjs edit "$filters" >/dev/null 2>&1
}

total=0
while :; do
  revert_protected
  if ! node -e 'JSON.parse(require("fs").readFileSync("features.json","utf8"))' 2>/dev/null; then
    [ -f "$SNAP" ] && cp "$SNAP" features.json
  fi
  feature="$(next_feature)"
  [ -z "$feature" ] && break
  if [ "$total" -ge "$MAX_TOTAL_TRIES" ]; then log "STOP total-limit total=$total"; exit 3; fi

  id="$(field "$feature" id)"
  check="$(field "$feature" check)"
  failfile=".last-failure-$id.txt"

  prompt="$(node harness/render-prompt.js "$feature" "$failfile")"
  cp features.json "$SNAP"

  # 워커는 읽기·쓰기만. Bash를 주지 않아 워커가 check(Unity 2분 실행)를 스스로 돌리며
  # 컨텍스트를 키우지 못하게 합니다. 판정은 아래 check가 전담합니다(64K 서버 보호).
  default_args=(--allowedTools "Read" "Edit(unity/Assets/Mallang/RR02/Runtime/**)" "Write(unity/Assets/Mallang/RR02/Runtime/**)" --permission-mode acceptEdits)
  if [ "${WORKER_ARGS+set}" = set ]; then
    # shellcheck disable=SC2206
    extra=($WORKER_ARGS)
  else
    extra=("${default_args[@]}")
  fi
  with_timeout "$TRY_TIMEOUT_SECONDS" $WORKER_CMD "$prompt" ${extra+"${extra[@]}"} \
    > ".worker-out-$id.txt" 2>&1
  worker_rc=$?

  total=$((total + 1))
  result=fail
  if protected_dirty; then
    revert_protected
    cp "$SNAP" features.json
    echo "보호 파일(테스트/데이터/하네스/기존 코드)을 고쳐서 실패 처리했습니다." > "$failfile"
  elif [ "$worker_rc" -ge 124 ] && [ "$worker_rc" -le 142 ]; then
    echo "시도당 시간 상한(${TRY_TIMEOUT_SECONDS}s)을 넘겨 실패 처리했습니다." > "$failfile"
  else
    out="$(with_timeout "$CHECK_TIMEOUT_SECONDS" bash -c "$check" 2>&1)"; rc=$?
    if [ "$rc" -eq 0 ]; then
      if regression_check; then result=pass
      else echo "이번 수정이 이미 통과한 기능을 깨뜨렸습니다(회귀)." > "$failfile"; fi
    elif [ "$rc" -ge 124 ] && [ "$rc" -le 142 ]; then
      echo "검사 명령이 시간 상한(${CHECK_TIMEOUT_SECONDS}s) 안에 끝나지 않았습니다." > "$failfile"
    else
      printf '%s\n' "$out" | tail -40 > "$failfile"
    fi
  fi

  if [ "$result" = pass ]; then
    mark "$id" pass
    rm -f "$failfile"
    git add unity/Assets/Mallang/RR02 Artifacts/Handoff001 features.json >/dev/null 2>&1
    git commit -qm "worker: $id 통과" >/dev/null 2>&1 || true
  else
    revert_project
    mark "$id" fail
  fi
  log "$id try=$total result=$result worker_rc=$worker_rc"
done

rm -f "$SNAP"
if node -e 'process.exit(require(process.cwd()+"/features.json").features.some(x=>x.blocked)?0:1)'; then
  log "DONE blocked-exists"; exit 2
fi
log "DONE all-pass"; exit 0
