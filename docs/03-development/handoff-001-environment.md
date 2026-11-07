# 001-listen-and-repeat-core 구현 환경 명세

## 개요

잠긴 기획 패키지 `specs/001-listen-and-repeat-core`를 구현하는 작업(game-build 절차)의 환경 명세입니다. 2026-11-07 사용자와 함께 확정했습니다. 이 문서는 game-build 1단계(선행 조건 확인)의 산출물이며, 로직 레인의 check 명령과 운영 규칙의 기준이 됩니다.

## 선행 조건 확인 결과 (2026-11-07)

- **패키지 잠금**: `lock.json`에 파일별 해시 봉인(커밋 3ad0969). 4차 감사 `Verdict: ready`.
- **기술 게이트**: Gate 0A 통과([기록](../02-gate-0/gate0a-fixes-2026-09-13.md)) — 실기기(SM-F956N)에서 소리·입력·판정이 확인되었습니다.
- **감사가 공개한 한계(구현 중 승계)**: 시각 확인(브라우저 렌더) 19건 pending, FR-026(영상/GIF 저장)의 WebGL 기술 확인 미수행, design.assets 비어 있음. 이 항목들은 구현의 `assumptions.md`와 미확인 목록으로 승계합니다.

## 확정 환경

- **엔진**: Unity 6000.3.23f1 (`/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity`). `UNITY_EDITOR` 환경변수로 덮어쓸 수 있습니다.
- **패키지 위치**: `feature/game-planning` 브랜치의 워크트리 `/Users/easyoon/mallang-brain/specs/001-listen-and-repeat-core`. 패키지는 읽기만 합니다 (필수).
- **구현 위치·브랜치**: 이 워크트리(palolo)의 `feature/001-listen-and-repeat-impl`. 실행 하네스가 기능 통과마다 자동 커밋을 만들므로 main에서 직접 작업하지 않습니다.
- **테스트 어셈블리 관례**: 기존 패턴(`Mallang.<모듈>.Tests.EditMode` / `.PlayMode` asmdef)을 따릅니다. 로직 레인 테스트는 EditMode를 기본으로 합니다.

## 테스트 실행 명령 (로직 레인 check)

```
node tools/handoff/run-tests.mjs <edit|play> [testFilter]
```

- 러너가 Unity 배치모드(화면 없이 명령 한 줄로 엔진을 돌리는 실행 방식)로 `-runTests`를 실행하고, 결과 파일 `TestResults.xml`을 직접 확인해 종료 코드로 알립니다.
- **종료 코드 0의 조건**: 테스트가 1개 이상 실행되고 전부 통과. 필터에 걸린 테스트가 0개면 실패(1)로 처리합니다 (필수). Unity 종료 코드만 믿지 않고 XML을 재확인하는 이유는, 테스트가 하나도 안 돌았는데 통과처럼 보이는 일을 막기 위해서입니다.
- `testFilter`는 Unity `-testFilter`에 그대로 전달됩니다(테스트 전체 이름에 대한 부분 일치, 세미콜론으로 여러 개).
- 증거는 `Artifacts/Handoff001/<시각>-<플랫폼>-<필터>/`에 남습니다(RunRecord.json·TestResults.xml은 커밋, Unity.log는 로컬만).

검증 기록 (2026-11-07):

- `node tools/handoff/run-tests.mjs edit "Mallang.RhythmCore"` → 55개 테스트 통과, 종료 코드 0.
- `node tools/handoff/run-tests.mjs edit "NoSuchNamespace.Nothing"` → 0개 실행, 종료 코드 1.
- 1회 실행에 약 2분(에디터 기동 포함)이 걸립니다. check 시간 상한은 900초를 권장합니다.

## 운영 규칙

- **배치모드 실행 중에는 Unity 에디터를 닫습니다** (필수). 같은 프로젝트를 연 에디터가 있으면 배치모드가 프로젝트를 열지 못합니다. 장면 레인(Unity MCP, 에디터 필요)과 로직 레인(배치모드)은 같은 시간에 돌리지 않습니다.
- **실행자 비용 계층(B 방식)**: 로직 레인 작업은 전부 로컬 모델(Strata)에 먼저 보내고, 막히는 것만 위로 올립니다. 승격 사다리는 **로컬(Strata) → Haiku → Sonnet → Opus**입니다(2026-11-08 사용자 지시로 Opus까지 확장). GPT 호스트라면 **Luna → Tera → Sol** 순입니다. 로컬 상한은 2회이고, 상한에 닿은 기능만 다음 티어로 한 칸씩 올립니다. 판단·계획·감사는 계층과 무관하게 세션이 합니다.
- **하네스**: `~/orca/workspaces/apprentice-harness/driftfish/harness`의 run.sh 계열을 이 저장소로 복사해 씁니다. 복사하면서 `project/` 전제 경로를 `unity/Assets/Mallang/` 구조에 맞게, 회귀 재검사를 에디터 기동 비용에 맞게(통과 기능의 필터를 세미콜론으로 합쳐 1회 실행) 고칩니다. env.sh(접속 키 포함)는 이 컴퓨터에만 두고 커밋하지 않습니다 (필수).

## 용어 설명

- **배치모드(batchmode)**: 에디터 화면 없이 명령 한 줄로 Unity를 돌려 결과를 종료 코드와 파일로 받는 실행 방식입니다.
- **로직 레인 / 장면 레인**: 기계(종료 코드)로 판정할 수 있는 작업(판정·점수·저장 등)과 사람이 봐야 판정되는 작업(씬·연출·소리)을 나눈 작업 흐름입니다.
- **check**: 기능 하나의 통과 여부를 종료 코드 0/1로만 판정하는 명령입니다. 이 프로젝트에서는 위 러너가 그 역할을 합니다.
- **B 방식(승격형 위임)**: 작업을 선별하지 않고 전부 가장 싼 실행자(로컬 모델)에 먼저 보낸 뒤, 상한에 닿은 작업만 더 비싼 실행자로 올리는 방식입니다.
