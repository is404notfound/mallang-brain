# Gate 0A 준비: 코드 문제 수정과 Gate 0A/0B 분리 — 2026-09-13

## 현재 판정

**보고된 코드 문제 4개(및 검증 중 추가로 발견한 3개)를 수정했다. Gate 0A/0B 분리를 문서화했다. 자동 시험 전체 통과. 실기기 측정은 아직 하지 않았다 — Gate 0A 통과 아님.**

직전 기록: [T4~T7 실행 기록](t4-t7-execution-2026-09-12.md). 기준 문서: [구현 계획](implementation-plan.md)(Gate 0A/0B 분리 절 추가).

## 보고된 문제 4개와 수정 내용

사용자가 코드를 검토해 4개 문제를 짚었다. 실제로 모두 재현 가능한 문제였다.

### 1. 시계 보정 실패가 이전 정상값을 덮지 못함

이전 코드는 `if (result.Valid) calibration = result;` 형태였다. 보정이 실패하면 `calibration` 변수가 그대로 남아 오래된 값으로 계속 판정할 수 있었다. `Runtime/Clock/CalibrationState.cs`를 새로 만들어 `Apply(next)`가 **성공이든 실패든 항상 `Current`를 덮어쓰도록** 고쳤다.

- 이 과정에서 추가로 발견: `Gate0MeasurementController.cs`에 옛 필드 `calibration.Valid`를 그대로 참조하는 줄이 남아 있어 **컴파일이 안 되는 상태**였다(`calibrationState.Current.Valid`로 수정).
- 또 발견: 재보정용 시계 표본을 마지막 정리 이후 계속(수 초씩) 누적하는 구조라, 실기기가 아닌 배치 모드 시험 환경에서 오디오 클록이 매끄럽게 안 늘어나 **정상 상황에서도 5ms 기준을 넘겨 재보정이 실패**했다(`InterruptDuringPlayingResetsCrowdAndSessionThenResumeStartsFresh` PlayMode 시험이 3회 연속 재현). 5ms 기준(정책 고정값)은 낮추지 않고, `ClockCalibrator`가 **가장 최근 표본 8개만** 평가하도록 창을 제한해 해결했다.

### 2. 8개 오디오가 동시에 재생되지 않음 → 애초에 대표성이 없는 부하였음

사용자가 직접 지적: "합주에서는 내 소리만 들리게 할 것"이므로 8개 동시 voice는 실제 제품을 대표하지 않는다. 단순히 동시 재생을 고치는 대신 **워크로드 자체를 바꿨다.** `Gate0Scenario.Phase.audioVoiceCount` 기본값을 8→1로 바꾸고, 컨트롤러는 오디오 voice 풀 대신 **플레이어 마림바 소리 하나**(`playerSoundSource`)만 재생한다. UI 효과음(박자 클릭)은 기존 메트로놈 로직을 그대로 쓴다.

- 발견: 이미 만들어 둔 `Settings/Gate0Scenario.asset`은 옛 기본값(8)으로 봉인돼 있어 새 코드의 검증(`!= 1`)과 어긋나 **Awake에서 예외가 발생**했다(브라우저 실제 실행으로 발견). 아직 동결 전 초안이므로 삭제 후 `prepare-contracts`로 재생성해 해결했다.

### 3. 동결되지 않은 정책도 최종 후보가 될 수 있었음

`BuildManifest.IsFinalGateEligible`이 `dirty == false`만 확인했다. `policyHash`가 `"unset-draft"` 같은 placeholder여도(정책 미동결) 커밋만 깨끗하면 통과 후보로 보일 수 있었다. `scenarioSchema/policySchema > 0`, `scenarioId/scenarioHash/policyId/policyHash`가 placeholder(`"unset-"`로 시작하거나 비어있음)가 아닌지까지 확인하도록 고쳤다.

### 4. 원자료가 실제 원자료가 아니었음

`raw-samples.bin`이 `RunRecord` 요약 JSON을 그대로 복사한 것이었다. `Runtime/Measurement/RawMeasurementTrace.cs`를 새로 만들어 **입력 시각, 프레임 시간, 박자 시각, 판정 결과(offset·band·accepted 등)를 실제로 기록**하고, 사람이 읽을 수 있는 JSON으로 저장한다.

## 부수 발견: WebGL jslib 함수 하나가 구현 없이 선언만 있었음

`Gate0GetDevicePixelRatio`(dprMilli 필드를 WebGL의 실제 `devicePixelRatio`로 채우기 위한 함수)가 C# 쪽에 `[DllImport("__Internal")]`로 선언만 있고 `.jslib` 쪽 구현이 없었다. WebGL 빌드는 성공했겠지만 링크 시 undefined symbol 오류가 날 수 있었다. `Gate0AudioContextBridge.jslib`에 구현을 추가해 실제 WebGL 빌드로 링크 성공을 확인했다.

## 개발용 Gate와 출시용 Gate 분리 (Gate 0A / Gate 0B)

기존 Gate 0의 9개 통과 조건([구현 계획](implementation-plan.md)의 Acceptance Criteria)은 하나도 바꾸지 않았다. 대신 그 앞에 "Gate 1 프로토타입 개발을 시작해도 되는지"만 판단하는 **Gate 0A**를 새로 추가했다. 상세 조건과 표는 [구현 계획](implementation-plan.md) 맨 아래 "Gate 0A / Gate 0B" 절에 있다. 요약:

- **Gate 0A 통과 시**: 홀드·판정·점수·콤보·탈락·12패턴·20스테이지·99명 연습생·전체 흐름·임시 캐릭터/배경 등 Gate 1 **프로토타입** 제작 시작.
- **Gate 0B(기존 Gate 0)**: 중급·저사양 기기, 240fps 외부 지연 실측, 전체 네트워크·캐시 조건까지 갖춰야 하며 **출시·최종 성능 보장·공식 순위·광고는 Gate 0B 통과 전에 진행하지 않는다.**
- 저사양 기기 문제는 우선 군중·VFX·오디오 voice 수를 낮추는 것으로 대응하고, 게임 규칙 재설계는 최후 수단으로 남긴다.

## 새로 추가한 자동 시험

- `CalibrationStateTests.ApplyAlwaysReplacesCurrentRegardlessOfValidity` — 문제 1 재발 방지. 유효한 보정 뒤 실패한 보정을 적용하면 `Current`가 실제로 무효로 바뀌는지 확인(이전 유효값이 남지 않음).
- `TimingTests.OnlyTheMostRecentSamplesAreEvaluated` — 오래된 표본이 최근 재보정 결과를 오염시키지 않는지 확인.
- `RawMeasurementTraceTests.CapturesRealObservationsNotJustASummaryCopy` — 문제 4 재발 방지. 직렬화된 JSON에 실제 offset·band 값이 있고 `meanFps`/`perfectCount` 같은 요약 필드는 없는지 확인.
- `RecordsTests.BuildManifestEligibilityRules`(갱신) — 문제 3 재발 방지. `policyHash`가 placeholder("unset-draft")면 커밋이 깨끗해도 최종 후보가 될 수 없는지 확인.
- `AutoPhaseRunnerTests` — 9개 phase 자동 순회·정지 상태 기계 확인.
- PlayMode `StartAutoRunBeginsFirstPhaseAndCancelStopsIt`, `ManualBeginPhaseCancelsAnActiveAutoRun` — 자동 실행 버튼의 실제 MonoBehaviour 연결 확인.

## 검증

| 확인 | 결과 |
| --- | --- |
| Unity Edit Mode 시험 | 55/55 통과 |
| Unity Play Mode 시험 | 13/13 통과 × 5회 연속(타이밍 안정성 확인) |
| WebGL 플레이어 빌드 | 성공, 오류 0, 경고 0 (시나리오 재생성 전 1회 실패 → 재생성 후 성공) |
| BuildManifest 자동 생성 | `policyHash: "unset-draft"`로 정확히 기록, 정책 동결 전까지 최종 후보 불가 확인 |
| 브라우저 실제 확인 | Start phase → Playing → 탭 판정(Perfect/Good/Miss/Dup) 실시간 반영, "Auto run all 9 phases" 버튼 노출 확인 |

## 한 버튼으로 9개 phase 자동 실행

이미 구현되어 있었다(`AutoPhaseRunner`, `Gate0MeasurementController.StartAutoRun/CancelAutoRun`, 화면의 "Auto run all 9 phases" 버튼). 각 phase가 실제로 180초라 전체 자동 실행을 자동 시험으로 끝까지 검증하는 것은 비현실적(27분+)이라, 시작·취소 연결부만 PlayMode로 확인했고 phase 전환 상태 기계 자체는 EditMode에서 별도로 확인했다.

## 아직 확인하지 않은 것

- 실기기(안드로이드·Toss)에서 이번에 고친 코드로 실제 9개 phase 자동 실행.
- 실기기에서 심각한 끊김·중단복구 실패·기록 손상이 없는지.
- Gate 0A의 나머지 조건(깨끗한 커밋에서 새 빌드, 고사양 기기 실행)은 사용자 손이 필요한 다음 단계다.
- Gate 0B(정식 출시 기준: 중급·저사양 기기, 240fps 외부 측정, 전체 네트워크·캐시 조건)는 여전히 미실행.

## 다음 단계

1. 이 변경사항을 검토 후 깨끗한 커밋 생성 (사용자 승인 필요).
2. 새 Android APK, Toss `.ait` 빌드.
3. 고사양 기기(`SM-F956N`)에서 Android·Toss 양쪽 9개 phase 자동 실행 — **여기서부터 사용자 손이 필요하다.**
4. 두 결과를 비교해 Gate 0A 판정.
5. 통과하면 Gate 1 구현 계획 작성 후 프로토타입 제작 시작.
