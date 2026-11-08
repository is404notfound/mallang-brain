# 인계 지도 (map.md) — specs/001-listen-and-repeat-core → unity/

## 개요

잠긴 패키지(RR02 8박 기억 합주)의 모든 FR/MCH/AC가 코드의 어디로 가는지 연결한 지도입니다. AC는 FR과 1:1 번호 대응이라(AC-001↔FR-001) FR 표에 함께 적습니다. 레인은 두 가지입니다 — **로직**(종료 코드 테스트로 포위 가능), **장면**(사람 판정 필요). 연결이 끊기거나 패키지만으로 알 수 없는 것은 [assumptions.md](assumptions.md)에 적었습니다.

## 코드 목적지 (신설 모듈)

기존 `Mallang.RhythmCore`(탭/홀드·콤보·Perfect/Good/Miss)는 FR-002가 요구하는 "기존 홀드 회귀 보존" 대상이라 **수정하지 않습니다** (필수). RR02 규칙은 새 어셈블리로 만듭니다.

- `unity/Assets/Mallang/RR02/Runtime/` (`Mallang.RR02.Runtime.asmdef`, 참조는 RhythmCore만 — stage-review 결정 1)
  - `Rr02Types.cs` — Friend(rabbit·capybara·fox·squirrel), 판정, 노트, 스냅샷
  - `Rr02Chart.cs` — 패키지 `design-tools/four-friend-chart.json`(고정 데이터) 로드 + 무한 구절 순환 + `Rr02Schedule`(구절 시작 절대시각 단일 진실 — 결정 3)
  - `Rr02Judge.cs` — 탭 전용 판정(±60/±120ms, 소유 구간 ±180ms, note_id 선택 규칙, character_id 비교)
  - `Rr02Run.cs` — 도전 상태기계(라운드 진행·성공/실패·무한·자격 상실·runAnimalScores)
  - `BrainScore.cs` — 동물별 최고 병합(max)·내 뇌 합산·뇌 단계 함수(0/200/400/700)
  - `Rr02Store.cs` — 저장 키 계약(brain:/reach:/badge:RR02:v1:*), max 병합·revision 직렬, IKeyValueStore 주입
  - `RankingRules.cs` — 순위 키 비교·공동 순위·N 구간(0/1~19/20+)·백분위·기준표 대체
  - `RankingSubmission.cs` — 제출 상태기계(대기→검증→확정/보류/거부), run_id 멱등, IRankingServer 주입(실서버 미구현)
- `unity/Assets/Mallang/RR02/Tests/EditMode/` (`Mallang.RR02.Tests.EditMode.asmdef`) — 위 전부의 수용 테스트
- 장면 레인: `unity/Assets/Mallang/RR02/Scenes/Rr02Prototype.unity` + `unity/Assets/Mallang/RR02/Screens/`(`Mallang.RR02.Screens.asmdef` — RR02.Runtime과 Gate0.Runtime 참조, 결정 1). 소리 안내·타이틀·플레이·결과·랭킹 화면이 여기 살고, 시계 보정은 Gate 0A에서 검증된 Clock 계열(ClockCalibrator·CalibrationState·RhythmClock)을 재사용합니다.

## FR/AC 지도

| FR(=AC) | 요지 | 레인 | 목적지 | 검증 |
| --- | --- | --- | --- | --- |
| FR-001 | 좌우 카드 26%·중앙 48% 배치 | 장면 | Screens/PlayScreen | 사람(렌더) + 배치 수치 PlayMode 보조 |
| FR-002 | 탭 전용, 기존 홀드 회귀 보존 | 로직 | RhythmCore 불변 + RR02는 홀드 없음 | 기존 RhythmCore 테스트 유지 + RR02 차트에 홀드 0 테스트 |
| FR-003 | 8박 듣기→8박 따라치기 쌍 | 혼합 | Rr02Run(구조)=로직 / 연출=장면 | EditMode + 사람 |
| FR-004 | 소리 주인+박 동시 판정 | 로직 | Rr02Judge | EditMode (AC-004 시나리오 그대로) |
| FR-005 | ±60/±120ms 경계 | 로직 | Rr02Judge | EditMode (offset 경계 전수) |
| FR-006 | 동물별 뇌 점수, 콤보 미적용 | 로직 | Rr02Run 점수 집계 | EditMode (AC-006: 270점 예시) |
| FR-007 | 150BPM 타이밍 산수 | 로직 | Rr02ChartFactory 상수 | EditMode (76.8초 합산 검증) |
| FR-008 | 실패해도 구간 끝까지, 점수 포함 | 로직 | Rr02Run | EditMode (AC-008) |
| FR-009 | 시계 보정·중단 자격 | 로직 | Gate0 Clock 재사용 + Rr02Run 자격 | 기존 Gate0 테스트 + 자격 전환 EditMode |
| FR-010 | 결과→다음→랭킹, 대기 없음 | 장면 | Screens/ResultScreen | 사람 + 이동 로직 EditMode 보조 |
| FR-011 | 숨김 0→8 규칙 | 로직 | Rr02ChartFactory.hiddenIndices | EditMode (R1=0…R9=8) |
| FR-012 | 몸짓 연출·소리 즉시 | 장면 | Screens/PlayScreen + 캐릭터 연출 | 사람(실기기 소리는 주장 안 함) |
| FR-013 | All 뒤 무한, 가속 없음 | 로직 | Rr02Run + ChartFactory 무한 순환 | EditMode (AC-013) |
| FR-014 | 부분 장애 대체·연습 전환 | 혼합 | Rr02Run 자격/대체 상태=로직, 안내 UI=장면 | EditMode + 사람 |
| FR-015 | 동물 최고·내 뇌 합산 | 로직 | BrainScore | EditMode (AC-015 수치 그대로) |
| FR-016 | 성장 연출 3초·스킵·배지 | 장면 | Screens/ResultScreen 연출 | 사람 / 배지 조건은 BrainScore EditMode |
| FR-017 | 저장 키·max 병합·revision | 로직 | Rr02Store | EditMode (역순 쓰기·중복 포함) |
| FR-018 | 숨긴 정답 누출 금지 | 장면 | Screens/PlayScreen(?칸) | 사람 + hiddenIndices 단일 기준은 로직 테스트 |
| FR-019 | 효과음 선정 기준 | 장면(사람) | 에셋 요청서 + PT | 사람 청취(이 환경에서 통과 주장 안 함) |
| FR-020 | 16:9 고정 1280×720 | 장면 | Rr02Prototype 카메라/캔버스 | 사람 + 해상도 3종 수치 보조 |
| FR-021 | 순위 키·공동 순위 | 로직 | RankingRules | EditMode (AC-021) |
| FR-022 | 기준표/실순위/백분위 구간 | 로직 | RankingRules + benchmark 데이터 | EditMode (N=0/5/20 경계) |
| FR-023 | 랭킹 화면 배치·상태 구분 | 장면 | Screens/RankingScreen | 사람 |
| FR-024 | 제출 멱등·검증 상태기계 | 로직 | RankingSubmission(+스텁 서버) | EditMode (중복 run_id·타임아웃) |
| FR-025 | 뇌 단계(0/200/400/700) 표시 | 혼합 | BrainScore 단계 함수=로직, 머리 위 표시=장면 | EditMode + 사람 |
| FR-026 | 공유 카드·영상/GIF 저장 | 장면 | Screens/ShareActions (placeholder) | 사람 / WebGL 녹화는 미확인 목록 |
| FR-027 | 소리 안내 화면 | 장면 | Screens/SoundNoticeScreen | 사람 + 1.5~2초/탭 이동 PlayMode 보조 |
| FR-028 | 감성 연출·움직임 줄이기 | 장면 | Screens 공통 연출 | 사람 |

## MCH 지도

| MCH | 요지 | 레인 | 목적지 |
| --- | --- | --- | --- |
| MCH-001 | 친구 카드 탭 판정 | 로직 | Rr02Judge (FR-004·005와 동일 테스트) |
| MCH-002 | 8박 시연-재현 루프 | 혼합 | Rr02Run=로직, 현재박/큰 동작=장면 |
| MCH-003 | 숨김 기억 재생 | 로직 | Rr02ChartFactory + Judge (FR-011) |
| MCH-004 | 무한 반복 +1 | 로직 | Rr02Run (FR-013) |
| MCH-005 | 결과 집계 연출 | 장면 | ResultScreen (FR-016) |
| MCH-006 | 랭킹 열람·재도전 | 혼합 | RankingRules=로직, RankingScreen=장면 |
| MCH-007 | 도움/중단 대체 선택 | 로직 | Rr02Run 자격·재개 규칙 (FR-014) |
| MCH-008 | 시연 모사(음원 검증용) | 장면(사람) | 에셋 선정 PT 도구 — 프로토타입 범위 밖, 에셋 요청서에 기록 |

## 용어 설명

- **소유 구간**: 첫 목표음 −180ms부터 마지막 목표음 +180ms까지(양끝 포함), 입력을 이 구절의 것으로 보는 시간 범위입니다.
- **ghost(헛입력)**: 어떤 열린 목표에도 속하지 않는 입력입니다. RR02에서는 0점 처리와 단계 실패를 일으킵니다.
- **기준표(benchmark)**: 참여자가 없을 때 비교용으로 쓰는 고정 등급표(`design-tools/ranking-benchmark.json`)로, 공식 집계에 포함되지 않습니다.
