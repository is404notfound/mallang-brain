# Slack 알림·승인 도구 — 새 세션 인계서

Strata(로컬 모델) 작업이 오래 걸려서, **작업 완료를 슬랙으로 알리고, 다음 작업 승인(approve/stop)을 받고, 간단한 대화도 주고받는** 도구를 새 세션에서 만들기 위한 인계서입니다.

## 새 세션에 그대로 붙여 넣을 프롬프트

> Strata 로컬 모델 작업 루프(apprentice-harness의 run.sh)가 오래 걸려서, 사람이 자리를 비워도 ① 작업 완료를 슬랙으로 알림받고 ② 다음 작업 승인(approve/stop)을 슬랙에서 결정하고 ③ 간단한 질의·대화도 슬랙에서 주고받는 도구를 만들고 싶어. 먼저 superpowers:brainstorming으로 뭘 만들지 좁히고(아래 "정해야 할 것" 참고), 설계가 서면 writing-plans로 계획을 세운 뒤 구현해줘. 참고 맥락은 `docs/03-development/slack-notifier-handoff.md`와 `~/orca/workspaces/apprentice-harness/driftfish/`(run.sh·env.sh·docs/local-agent-delegation.md)에 있어.

## 맥락 (왜 필요한가)

- 게임 구현을 game-build 절차로 하는데, 로직 레인 실행자를 **Strata(집 컴퓨터의 64K 로컬 모델)**에 맡깁니다. `claude -p` 워커가 기능을 하나씩 구현하고, 한 기능에 수십 분씩 걸립니다(상태기계 같은 큰 기능은 30~60분).
- 루프 엔진은 `~/orca/workspaces/apprentice-harness/driftfish/harness/run.sh`입니다. 기능이 통과하면 자동 커밋하고, 전부 통과/막힘이면 종료 코드(0/2/3)로 끝납니다.
- 지금은 사람이 터미널 `progress.log`를 들여다보며 끝나길 기다리고, 끝나면 다음 단계(재위임·승격·다음 레인)를 사람이 판단합니다. 이 "기다림 + 판단 요청"을 슬랙으로 옮기고 싶습니다.

## 이 도구가 할 일 (초안 — brainstorming에서 확정)

1. **완료 알림**: run.sh(또는 그걸 감싼 러너)가 끝나면 슬랙으로 결과 요약을 보냅니다(통과/blocked 기능 목록, 소요 시간, 다음 제안).
2. **승인 받기**: "다음 작업(예: 재위임 / 승격 / 다음 레인)을 진행할까요?"를 슬랙에 올리고 **approve/stop** 버튼(또는 답장)을 받아 그 결정을 반환합니다.
3. **간단한 대화**: 슬랙 스레드에서 짧은 질의·지시를 받아 처리(예: "지금 몇 개 통과했어?", "L3만 다시 돌려줘").

## 정해야 할 것 (brainstorming 질문)

- **전송 방식**: Slack Incoming Webhook(단방향 알림, 가장 간단) vs Slack App + Bot token(양방향 버튼·대화 필요 시). 양방향 승인·대화를 원하면 Bot(Events API 또는 Socket Mode)이 필요합니다.
- **승인 수신 방식**: 버튼(Block Kit) 콜백을 받으려면 공개 엔드포인트나 Socket Mode가 필요합니다. "간단하게"가 목표면 ① 알림은 Webhook ② 승인은 사람이 터미널/세션에 돌아와서 한다 — 로 쪼개는 것도 방법입니다.
- **대화 범위**: 상태 조회 같은 읽기 전용만? 아니면 작업 트리거(재실행·승격)까지? 트리거까지면 권한·안전장치가 필요합니다.
- **실행 위치**: run.sh를 감싸는 래퍼가 완료 시 알림을 쏘는 구조 vs 별도 데몬이 progress.log를 감시하는 구조.
- **비밀값**: Slack 토큰·웹훅 URL은 env(비커밋)에 둡니다. 어느 채널/워크스페이스인지.

## 오피스아워 / 플랜 엔지니어 리뷰, 필요할까?

- **오피스아워: 불필요**(이건 사람 멘토링 세션 성격이라 이 도구와 무관).
- **plan-eng-review(계획 심사): 선택 — 가벼운 도구면 생략 가능.** 단방향 Webhook 알림만이면 리뷰 없이 brainstorming→writing-plans→구현으로 충분합니다. **다만 양방향(버튼 콜백·작업 트리거)으로 가면** 공개 엔드포인트·권한·토큰 보안이 생기므로 그때는 간단한 계획 심사(또는 보안 점검)를 한 번 받는 걸 권합니다.
- 요약: **단방향 알림 = 리뷰 생략, 양방향 제어 = 가벼운 리뷰 권장.**

## 참고 파일

- 루프 엔진: `~/orca/workspaces/apprentice-harness/driftfish/harness/run.sh`, `env.sh`(비밀값 틀은 `env.example.sh`)
- 로컬 위임 플레이북: `~/orca/workspaces/apprentice-harness/driftfish/docs/local-agent-delegation.md`
- 진행 로그 형식: run.sh가 쓰는 `progress.log`(한 줄에 `<시각> <기능id> try=N result=pass|fail worker_rc=…`)
