# RR02 데이터 출처

이 폴더의 JSON 두 개는 잠긴 기획 패키지 `specs/001-listen-and-repeat-core`(feature/game-planning 워크트리)의 `design-tools/`에서 **그대로 복사**한 것입니다 (필수: 내용 수정 금지). lock.json 봉인 해시와 일치함을 2026-11-07에 확인했습니다.

- `four-friend-chart.json` — RR02 고정 차트(라운드 1~9 시퀀스·숨김 위치·targetTicks·캐릭터). sha256 `26f7f2cc…`
- `ranking-benchmark.json` — 초기 기준표(RR02-rubric-v1, 공식 집계 미포함). sha256 `dac8820d…`

차트를 바꿔야 한다면 패키지 개정(재감사·재잠금)이 먼저입니다. 코드에서 임의로 고치지 마세요.
