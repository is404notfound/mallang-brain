# 로컬 모델 접속 환경변수 (틀)
#
# 이 파일을 env.sh로 복사한 뒤 값을 채우세요. env.sh는 접속 키가 들어 있어
# 커밋하지 않습니다(.gitignore에 등록됨).
#
# 값을 채운 뒤에는 docs/environment.md의 "다시 확인할 목록"을 따라
# claude -p 파일 읽기·쓰기 도구 호출 검증을 통과해야 다음 단계로 갑니다 (필수).

# 로컬 모델 서버 주소. Anthropic Messages API 호환 끝점(endpoint)이어야 합니다.
export ANTHROPIC_BASE_URL=""

# 접속 키. 로컬 서버가 키를 검사하지 않으면 아무 값이나 넣어 자리만 채웁니다.
export ANTHROPIC_API_KEY=""

# 실행자가 쓸 모델 이름. 컨텍스트 길이 64K 이상이어야 합니다 (필수).
export ANTHROPIC_MODEL=""

# Claude Code가 보조 작업에 쓰는 작은 모델. 보통 위와 같은 모델을 넣습니다.
export ANTHROPIC_SMALL_FAST_MODEL=""

# 실행자 호출 명령. run.sh는 이 변수로만 실행자를 부릅니다.
# 스크립트를 시험할 때는 가짜 실행자로 바꿔 끼울 수 있습니다.
export WORKER_CMD="claude -p"

# --- Unity MCP (장면 레인에서 로컬 모델이 엔진을 조작할 때) ---
# export UNITY_MCP_CMD="uvx --from mcpforunityserver mcp-for-unity --transport stdio"
# export WORKER_ARGS_SCENE='--allowedTools Read Edit(project/**) Write(project/**) mcp__unity__* --permission-mode acceptEdits'

# --- 로컬 모델 컨텍스트 자동 컴팩션 (모델 윈도우에 맞춰 설정) ---
# 이 값은 모델의 "실제" 윈도우로 두세요. 더 낮추면 안 됩니다: Claude Code가 매 요청에
# 싣는 시스템 프롬프트+도구 정의와 출력 예약을 빼고 나면 입력 예산이 모자라, 작은 작업
# 프롬프트조차 "Prompt is too long"으로 거부됩니다(docs/local-agent-delegation.md 참고).
# headless(claude -p)는 자동 압축이 안 걸릴 수 있으니, 서버 보호는 숫자가 아니라 구조로
# 합니다(워커가 비싼 check를 스스로 돌리지 않게 하기 등 — playbook 참고).
# export CLAUDE_CODE_MAX_CONTEXT_TOKENS=65536
# export CLAUDE_CODE_AUTO_COMPACT_WINDOW=49152
