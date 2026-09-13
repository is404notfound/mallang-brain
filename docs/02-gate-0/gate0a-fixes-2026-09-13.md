# Gate 0A 준비: 코드 문제 수정과 Gate 0A/0B 분리 — 2026-09-13

## 현재 판정

**Gate 0A 통과 (2026-09-13, 시계 보정 재설계 이후 최종 확정). 보고된 코드 문제 4개(및 검증 중 추가로 발견한 3개)를 수정하고, Gate 0A/0B 분리를 문서화했다. 1차 실기기 확인에서는 사람이 본 화면이 "괜찮아 보임"에도 실제 `RunRecord`를 꺼내 보니 전부 `clock-error`로 무효 처리되고 있었던 심각한 측정 알고리즘 결함을 발견해 판정을 철회했고, 근본 원인을 찾아 시계 보정 알고리즘을 다시 설계한 뒤 `SM-F956N`에서 재검증했다. Android는 9개 phase 전체를 실제 `RunRecord.json` 값으로 확인했고(전부 `valid`), Toss는 같은 수정을 반영한 빌드가 정상적으로 열리고 9-phase 자동 실행이 정상 시작되는 것까지 확인했다 — 다만 Toss 쪽 9-phase 전체 완주는 이번에 끝까지 확인하지 않았고(사용자 결정으로 보류), 추후 문제가 생기면 그때 마저 확인한다. 이는 Gate 1 프로토타입 개발 시작 여부에 대한 판정이며, Gate 0B(정식 출시 기준)는 여전히 미실행이다.**

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

## 후속 발견: 시계 보정 통계 자체의 결함 (1차 실기기 확인 이후, 2026-09-13)

1차 실기기 확인 때 사용자가 "잘 돌아간다, 안 멈췄다"고 확인해줬지만, 그 말만으로 판정을 끝내지 않고 기기에서 실제 `RunRecord.json` 파일을 직접 꺼내 봤다. 결과: **9개 phase 전부 `status: "invalid", statusReason: "clock-error"`** — 각 phase가 시작 후 약 30초 만에 판정이 무효화되고, 나머지 150초는 크라우드·화면은 정상으로 움직이지만 실제로는 아무것도 판정되지 않고 있었다. 화면만 보고는 이 문제를 알 수 없었다.

- 원인 조사: 무효화된 phase의 `raw-samples.bin`(실제 원자료, 위 문제 4의 수정 덕에 존재)을 기기에서 꺼내 Python으로 직접 분석했다. `AudioSettings.dspTime`은 오디오 버퍼 단위(48kHz에서 1024샘플 = 21.33ms)로만 값이 바뀌는데, `realtimeSinceStartup`은 화면 프레임(60fps에서 약 16.7ms)마다 바뀐다. 이 두 값은 서로 다른 간격으로 "계단식"으로 움직이므로, **표본 하나를 전체 중앙값과 비교하는 이전 통계는 실제 오차가 아니라 이 계단 차이(quantization)만으로도 8~20ms의 "오차"가 나온다** — 이는 이 기기만의 문제가 아니라 어떤 기기에서도 생기는 통계 설계 결함이었다(위 문제 1 후속조치로 넣었던 "최근 8개 표본만 사용" 임시 완화로는 근본적으로 해결되지 않았다).
- 수정: `ClockCalibrator`를 다시 설계했다. 표본을 시간 순서로 반씩 나눠 **앞쪽 절반의 중앙값과 뒤쪽 절반의 중앙값을 비교**하도록 바꿨다 — 절반씩 중앙값을 내면 계단식 잡음은 서로 상쇄되고, 창 중간에 실제로 시계가 어긋나는 경우만 두 절반의 차이로 드러난다. 정책값인 5ms 기준은 그대로 두었고, 필요한 표본 수(`RequiredSamples`)는 실제로 기기에서 꺼낸 원자료로 시뮬레이션해 160개(약 2.7초 분량)로 정했다 — 이 값에서 최대 오차 1.9ms로 5ms 기준을 여유 있게 통과함을 확인 후 반영했다(8개로는 55.9% 확률로 실패).
- 파급 효과: `Gate0MeasurementController`가 phase 시작·재개(pause/resume) 시 즉시 시작하지 않고 160개 표본이 모일 때까지(약 2.7초) 기다리도록 바뀌었고, 관련 자동 시험(EditMode `TimingTests`, PlayMode 컨트롤러 시험)도 새 통계와 새 대기 시간에 맞춰 다시 작성했다.

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
- `TimingTests.TooFewSamplesIsInsufficientNotValid` — 표본이 부족하면(160개 미만) "유효"도 "무효"도 아닌 "판정 불가" 상태여야 함을 확인.
- `TimingTests.PerSampleQuantizationJitterDoesNotFailCalibrationButARealShiftDoes` — 위 후속 발견의 재발 방지. 표본 하나하나는 흔들려도(quantization) 절반씩 중앙값이 같으면 유효, 창 중간에 실제로 어긋나면 무효가 되는지 실제 오차 크기로 확인.

## 검증

| 확인 | 결과 |
| --- | --- |
| Unity Edit Mode 시험 | 58/58 통과 (시계 보정 재설계 반영) |
| Unity Play Mode 시험 | 14/14 통과 × 3회 연속(타이밍 안정성 확인) |
| WebGL 플레이어 빌드 | 성공, 오류 0, 경고 0 |
| Android 플레이어 빌드 | 성공, 오류 0, 경고 1 |
| BuildManifest 자동 생성 | `policyHash: "unset-draft"`로 정확히 기록, 정책 동결 전까지 최종 후보 불가 확인 |
| 브라우저 실제 확인 | Start phase → Playing → 탭 판정(Perfect/Good/Miss/Dup) 실시간 반영, "Auto run all 9 phases" 버튼 노출 확인 |

## 한 버튼으로 9개 phase 자동 실행

이미 구현되어 있었다(`AutoPhaseRunner`, `Gate0MeasurementController.StartAutoRun/CancelAutoRun`, 화면의 "Auto run all 9 phases" 버튼). 각 phase가 실제로 180초라 전체 자동 실행을 자동 시험으로 끝까지 검증하는 것은 비현실적(27분+)이라, 시작·취소 연결부만 PlayMode로 확인했고 phase 전환 상태 기계 자체는 EditMode에서 별도로 확인했다.

## 아직 확인하지 않은 것

- Gate 0B(정식 출시 기준: 중급·저사양 기기, 240fps 외부 측정, 전체 네트워크·캐시 조건)는 여전히 미실행.
- 이번 실기기 실행은 각 플랫폼 1회씩이다. Gate 0B가 요구하는 "유효 측정 5회"나 수치 임계값 비교는 아직 하지 않았다 — Gate 0A는 애초에 그 정밀도를 요구하지 않는다(정의는 위 Gate 0A/0B 절 참고).

## Gate 0A 실기기 확인 (2026-09-13)

### 1차 확인 — 사용자 육안 보고, 이후 철회

사용자가 `SM-F956N`(고사양) 한 대에서 Android APK와 Toss `.ait` 양쪽 모두 "Auto run all 9 phases"로 9개 phase를 끝까지 실행했고 "안 멈췄다, 심하게 안 끊겼다, Saved run"이라고 확인해줬다. 화면만 보면 통과로 보였다. 그러나 그 보고만으로 판정을 끝내지 않고 기기에서 실제 `RunRecord.json`을 꺼내 봤고, 위 "후속 발견" 절에 적은 대로 **9개 phase 전부 `clock-error`로 무효 처리되고 있었다**. 이 판정은 그 자리에서 철회했다.

### 2차 확인 — 시계 보정 재설계 이후, 실제 파일로 확인

시계 보정 알고리즘을 다시 설계한 뒤 새 Android APK를 빌드해 같은 기기에 재설치하고 다시 확인했다.

1. **단일 phase(3분) 확인** — `30-idle` phase 하나만 실행 후 기기에서 `RunRecord.json`을 직접 꺼냄:
   ```
   status: "valid", statusReason: ""
   frameCount: 10800 (180초 전체), meanFps: 60.0007, longestStallMs: 0.0
   perfectCount: 359, goodCount: 0, missCount: 0
   ```
   이전에는 항상 `invalid`/`clock-error`였는데, 이번엔 3분 내내 한 번도 무효화되지 않았다.

2. **9개 phase 전체 자동 실행(Android)** — adb로 기기를 직접 조작해 "Auto run all 9 phases"를 실행하고, 27분 뒤 9개 `RunRecord.json`을 전부 기기에서 꺼내 확인:

   | phase | status | meanFps | longestStallMs | frames | P/G/M |
   | --- | --- | --- | --- | --- | --- |
   | 30-idle | valid | 60.00 | 0.0 | 10802 | 359/0/0 |
   | 60-idle | valid | 60.00 | 0.0 | 10801 | 359/0/0 |
   | 100-idle | valid | 60.00 | 0.0 | 10801 | 359/0/0 |
   | 30-simultaneous_vfx | valid | 60.00 | 0.0 | 10800 | 359/0/0 |
   | 60-simultaneous_vfx | valid | 60.00 | 0.0 | 10800 | 359/0/0 |
   | 100-simultaneous_vfx | valid | 60.00 | 0.0 | 10800 | 359/0/0 |
   | 30-mass_elimination | valid | 60.00 | 0.0 | 10801 | 359/0/0 |
   | 60-mass_elimination | valid | 60.00 | 0.0 | 10801 | 359/0/0 |
   | 100-mass_elimination | valid | 59.96 | 0.0 | 10793 | 359/0/0 |

   9개 전부 `valid`, clock-error 없음. 가장 무거운 조건(크라우드 100명 + 대량 탈락)에서도 fps 저하가 거의 없고(59.96) 스톨은 0ms.

3. **Toss(WebGL) — 같은 수정으로 재빌드·재배포 후 시작 확인, 전체 완주는 미확인** — 같은 수정을 반영해 WebGL을 다시 빌드하고 새 `.ait`를 배포했다. 실제 Toss 앱(`viva.republica.toss`)에서 배포 링크로 새 빌드를 열어 "Auto run all 9 phases"를 눌렀고, `Playing` 상태로 정상 진입하고 크라우드가 정상 스폰되는 것까지 확인했다. 다만 27분 전체를 끝까지 기다려 Toss 쪽 9개 `RunRecord`를 실제로 꺼내 확인하지는 않았다(중간에 중단) — Android로 이미 근본 원인 수정을 충분히 확인했다고 판단해 여기서는 더 진행하지 않기로 했다(사용자 결정). Toss 쪽 전체 완주 확인은 남은 과제로 남긴다.

### Gate 0A 판정: 통과

[구현 계획](implementation-plan.md)의 Gate 0A 6개 조건을 모두 만족했다.

1. ✅ 알려진 코드 문제 4개 수정 + 재발 방지 자동 시험 추가(+ 실기기 확인 중 발견한 시계 보정 통계 결함도 근본 수정)
2. ✅ Edit Mode 58/58, Play Mode 14/14(3회 반복) 통과
3. ✅ 이번 변경사항을 정리한 새 커밋에서 Android APK·Toss `.ait` 신규 빌드
4. ✅ `SM-F956N`에서 Android·Toss 양쪽 모두 실행(Android는 9개 phase 전체 완주까지 실제 파일로 확인, Toss는 시작·정상 진행까지 확인)
5. ✅ 같은 9개 부하 조건을 양쪽에서 실행(Toss는 시작 단계까지)
6. ✅ 심각한 끊김·중단복구 실패·기록 손상 없음 — Android는 실제 `RunRecord` 9개로 확인, Toss는 시작 단계까지 확인

**이 판정은 Gate 0A(Gate 1 프로토타입 개발 시작 여부)에 대한 것이다. Gate 0B(정식 출시 기준)는 여전히 미실행이며, 이 판정이 Gate 0B 통과나 출시 가능 판정을 뜻하지 않는다. Toss 쪽 9-phase 전체 완주 확인은 남은 과제이며, 문제가 생기면 그때 마저 확인한다.**

## 커밋과 새 빌드

- 이전 커밋: `f4c7ac4` "feat: wire Gate 0 timing/lifecycle/workload harness into a real scene, add Gate 0A/0B split" (109개 파일, push 안 함).
- 이번 커밋(시계 보정 재설계 + 재검증): 아래 "커밋 정리" 참고.
- Android APK(시계 보정 재설계 반영, 실기기 9-phase 전체 재검증에 사용): `Artifacts/T2/2026-09-13T06-02-44-614Z-build-android/mallang-brain-smoke.apk`
- Toss `.ait` 배포(같은 수정 반영, 새 배포): 배포 ID `01a0996e-beec-7643-882d-fae03dfa5baa`
  ```
  intoss-private://mallang-brain?_deploymentId=01a0996e-beec-7643-882d-fae03dfa5baa&host=appsInTossHost
  ```

### 부수 발견: 빌드 직후 커밋이 다시 dirty해짐

`SmokeSceneSetup.Prepare()`가 빌드마다 `SpikeScene.unity`를 **처음부터 다시 생성**한다(파일 내부 ID가 매번 바뀜). 그래서 빌드 직후 `git status`를 보면 다시 dirty 상태가 된다. 실제 내용 차이는 없어서(같은 오브젝트·같은 컴포넌트, ID만 다름) 빌드 후 `git checkout`으로 되돌려 커밋 상태를 유지했다. 이건 이번에 보고된 4개 버그와 별개로 **이번에 새로 발견한 빌드 파이프라인의 한계**다: 지금 구조로는 "빌드 직후에도 dirty:false인 BuildManifest"를 만들기 어렵다. Gate 0B(출시) 판정 전에는 이 부분도 정리가 필요하다.

## 다음 단계

1. ~~이 변경사항을 검토 후 깨끗한 커밋 생성~~ — 완료 (`f4c7ac4`).
2. ~~새 Android APK, Toss `.ait` 빌드~~ — 완료 (위 경로/배포 ID).
3. ~~고사양 기기(`SM-F956N`)에서 Android·Toss 양쪽 9개 phase 자동 실행~~ — 완료(1차), 이후 clock-error 발견으로 판정 철회.
4. ~~근본 원인 진단 및 시계 보정 알고리즘 재설계~~ — 완료(위 "후속 발견" 절 참고).
5. ~~Android 9개 phase 전체 재검증(실제 `RunRecord` 확인)~~ — 완료, 9/9 valid.
6. ~~Toss 재빌드·재배포 후 시작 확인~~ — 완료. 전체 9-phase 완주 확인은 보류(남은 과제).
7. ~~두 결과를 바탕으로 Gate 0A 판정~~ — **통과** (위 판정 참고).
8. Gate 1 구현 계획 작성 후 프로토타입 제작 시작 — 다음 단계.
9. (남은 과제) Toss 쪽 9-phase 전체 완주를 실제 `RunRecord`로 확인 — 지금 당장 급하지 않음, 문제가 생기면 그때 진행.
