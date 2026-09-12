# Gate 0 T4~T7 실행 기록 — 2026-09-12

> 2026-09-13 후속: 사용자 코드 검토로 문제 4개(시계 보정 무효화, 오디오 대표성, 정책 placeholder, 원자료)가 드러났고 모두 수정했다. Gate 0A/0B 분리도 이때 문서화했다. 최신 상태는 [Gate 0A 준비 기록](gate0a-fixes-2026-09-13.md)을 따른다. 아래는 9월 12일 당시 기록이다.

## 현재 판정

**T4~T7 코드를 실제 화면에 연결하고, 자동 시험과 브라우저 실제 조작으로 확인했다. Gate 0 통과 아님. 실제 장비 3분 측정·외부 A/V는 아직 미실행.**

이번 작업은 리듬게임의 핵심 판정·복구·부하·기록 뼈대를 코드로 만들고, 같은 날 후속으로 **실제 `SpikeScene`에 연결해 눈으로 확인 가능한 화면**으로 만든 뒤, Unity 자동 시험과 **직접 브라우저를 조작해 실제로 눌러보는 방식**으로 확인한 기록이다. 실제 안드로이드 기기와 실제 토스 앱에서의 정식 측정(3분 행렬, 240fps 외부 A/V)은 아직 하지 않았다. 자동 시험과 브라우저 확인을 통과했다고 Gate 0 전체 통과로 처리하지 않는다. 장비 측정을 미룬 것을 통과로 바꾸지 않는다. 기존 판정 기준·표본 수·기간은 그대로 두었다.

바탕 기준 문서: [구현 계획](implementation-plan.md), [테스트 계획](test-plan.md). 직전 기록: [T2 실행 기록](t2-execution-2026-09-10.md).

## 이번에 만든 것

네 가지를 요청 순서대로 만들었다.

### 1. 누른 시간과 음악 시간 맞추기 (계획 T4)

- 두 개의 시계를 다룬다. `RealtimeSeconds`는 입력이 들어온 시각(`InputEvent.time`과 같은 시간축), `DspSeconds`는 소리 일정 시각(`AudioSettings.dspTime`)이다.
- `ClockCalibrator`가 두 시계의 짝 표본을 모아 중앙값 차이(offset)를 구한다. 이 offset으로 "누른 시각"을 "소리 시각"으로 바꾼다.
- 시계가 뒤로 가거나(역행), 갑자기 튀거나(점프), 보정 오차가 5ms를 넘으면 그 구간을 **무효**로 표시한다. 무효 구간은 결과에서 빼고 원자료는 남긴다. 잘못된 값으로 통과 판정을 내리지 않는다.
- `RawTimingProbe`가 누른 시각과 박자(cue)의 차이를 재서 **±60ms는 Perfect, ±120ms는 Good, 그 밖은 Miss**로만 분류한다. 점수·콤보·실패·난이도는 만들지 않는다(그건 Gate 1).
- 판정은 정수 마이크로초로 계산한다. 그래서 같은 입력·박자 배열은 안드로이드와 WebGL에서 **똑같은 band 배열**이 나온다(플랫폼 100% 일치 기준).
- 같은 손으로 아주 짧은 간격(기본 50ms) 안에 두 번 누르면 중복으로 보고 버린다. 중복 효과나 표본을 만들지 않는다.
- 파일: `Runtime/Clock/RhythmClock.cs`, `Runtime/Clock/ClockCalibration.cs`, `Runtime/Input/RawTimingProbe.cs`

### 2. 잠깐 멈췄다가 돌아오는 동작 (계획 T5)

- 상태 하나로 관리한다: `Boot → Idle → CountIn → Playing → Completed`, 그리고 중단 시 `Interrupted → Recalibrating → CountIn`. 표에 없는 전이는 거부한다(안전하게 실패).
- 소리나 앱이 멈추면 **입력 판단을 멈추고** 예약된 소리를 끊으며 대기 입력·홀드를 비운다. 그 구간은 Gate 집계에서 뺀다.
- 돌아오면 시계를 다시 보정하고, 소리가 준비된 뒤 **네 박자를 새로 센 다음** 다시 플레이한다.
- 소리 재준비에 실패하면 `Error`에 머물고, 다시 시도(retry)해서 성공하면 네 박자 카운트로 복구한다.
- 플랫폼 신호를 하나로 합친다. 안드로이드의 pause/focus가 겹치거나 순서가 뒤집혀 와도, WebGL에서 소리만 멈춰도 **중단은 한 번, 네 박자 재시작도 한 번**만 일어난다.
- 파일: `Runtime/Lifecycle/LifecycleController.cs`, `InterruptCoalescer.cs`, `LifecycleDriver.cs`, `Platform/Android/AndroidLifecycleAdapter.cs`, `Platform/WebGL/WebGLLifecycleAdapter.cs`

### 3. 사람이 많을 때의 시험 장면 (계획 T6)

- 이미 동결된 시나리오의 30·60·100명 × idle·simultaneous_vfx·mass_elimination 9단계를 그대로 부하로 만든다.
- `WorkloadSchedule`이 박자마다 할 일(캐릭터 수, 동시 VFX 수, 제거 수)을 **결정적으로** 정한다. 같은 시나리오면 두 플랫폼에서 같은 부하가 나온다. VFX 단계는 박자마다 캐릭터 수만큼 일회성 VFX를, 제거 단계는 박자마다 전체를 제거·재생성해 최악 부하를 유지한다.
- `LoadDriver`가 이 계획을 실제 장면으로 만든다(캐릭터 프록시 + 스프라이트 2겹 + 박자별 VFX/제거). 최종 아트가 아니라 측정용 상한 대리 부하다.
- `FrameStatsCollector`가 평균 FPS, 프레임 시간 p95·p99, 33ms·50ms 초과 프레임 수, 최장 연속 멈춤을 계산한다. 중단된 구간의 프레임은 넣지 않는다.
- 파일: `Runtime/Workload/WorkloadSchedule.cs`, `Runtime/Workload/LoadDriver.cs`, `Runtime/Measurement/FrameStatsCollector.cs`

### 4. 시험 결과 저장하기 (계획 T7)

- `RunRecord`(실행마다 새 파일)와 `BuildManifest`(빌드마다 한 번)를 만들었다. 시나리오·정책 해시, 기기·OS·주사율·네트워크·cold/warm/cache 상태, 유효/무효/실패 사유, 외부 측정 파일 경로, 원자료 checksum을 담는다.
- 필수 항목이나 checksum이 빠지면 **비교 가능한 결과로 인정하지 않는다**(예외로 막음).
- `BuildManifest`는 dirty(작업 중 변경) 빌드거나 checksum이 없으면 **최종 통과 후보가 될 수 없다**. 진단용으로만 쓴다.
- `MeasurementWriter`는 기존 기록을 **덮어쓰지 않는다**. 이미 있으면 거부한다. 저장 전에 원자료 SHA-256을 기록에 새기고 검증한다.
- 파일: `Runtime/Measurement/RunRecord.cs`, `BuildManifest.cs`, `MeasurementWriter.cs`
- 참고: 실제 빌드 파이프라인(`SmokeBuild`)에 `BuildManifest` 자동 생성은 아직 연결하지 않았다. 이번엔 스키마·검증·저장 규칙과 시험까지 준비했다. 실제 값 채우기는 장비 측정(T9) 때 연결한다.

## 실제 화면에 연결 (같은 날 후속)

T4~T7 코드는 처음엔 화면에 연결되지 않은 클래스로만 존재했다. 이 상태에서는 사람이 눌러볼 게 없었다. 그래서 같은 `SpikeScene`에 `Gate0Measurement`라는 오브젝트를 새로 추가해 다음을 실제로 동작하게 만들었다.

- 화면 하단 "◀ Prev / Next ▶"로 9개 phase 중 하나를 고르고 "Start phase"를 누르면 실제 네 박자 카운트인(진짜 클릭음 4번)이 나온다.
- 카운트인이 끝나면 실제로 해당 phase의 인원(30·60·100명)이 화면에 생성되어 박자에 맞춰 움직인다.
- 좌우를 탭하면 실제로 Perfect/Good/Miss/중복이 화면 좌상단에 실시간으로 집계된다.
- 앱이 중단됐다 돌아오면 실제로 중단 상태가 됐다가 시계를 다시 맞추고 네 박자를 다시 센다.
- phase가 끝나면 실제로 기기 저장 공간에 `RunRecord`가 저장된다(평균 FPS, 판정 개수 포함).
- 기존 좌우 탭·색·소리 화면(`SmokeTapScene`)은 그대로 남겨뒀다. 새 오브젝트는 그 옆에 추가한 것이다.
- 새 파일: `Runtime/Clock/BeatScheduler.cs`, `Runtime/Input/JudgementTally.cs`, `Runtime/Workload/PhaseSession.cs`, `Runtime/Measurement/Gate0MeasurementController.cs`. `RunRecord`에 실측 지표 필드(평균 FPS, 프레임 p95/p99, 판정 개수 등)를 추가했다.
- 이 시점의 한계(모두 아래 3차 작업에서 해결): 탭 시각을 매 프레임 폴링으로만 쟀고, WebGL AudioContext만 끊기는 경우를 못 잡았고, `buildId`는 placeholder였다.

## 판정 정밀화·전체 부하·기록 자동화 (같은 날 3차 작업)

사용자가 오디오 버그 확인 뒤 남은 작업 목록을 전달했고, 그중 실기기·카메라가 필요 없는 코드 작업만 골라 진행했다(실측 매트릭스·240fps 촬영·T10 판정·Gate 1 제작은 하드웨어가 필요하거나 Gate 0 통과 전이라 건너뛰었다).

- **시계 보정 실패 시 즉시 무효화** — `PhaseSession.Invalidate(reason)`을 추가했다. 실행 중 30초 재보정이 실패하면 그 즉시 판정을 멈추고(이후 입력은 `segment-invalidated`로 거부), phase 종료 시 `RunRecord.status="invalid"`로 사유와 함께 저장한다. 이전에는 재보정 실패 시 이전 값으로 조용히 계속 판정했다.
- **중단·재개·재시도 시 전체 초기화** — `StartAttempt()`/`AbortAttempt()`로 나눴다. 중단(Interrupted) 진입 시 크라우드·세션·집계·예약 오디오·화면 플래시를 전부 버리고, 재개 성공 시 크라우드 재생성·새 `PhaseSession`으로 완전히 새로 시작한다. 이전에는 중단 전 데이터가 재개 후에도 섞여 남아 있었다.
- **`InputEvent.time` 기반 입력** — `InputSystem.onEvent`를 구독해 실제 입력 이벤트 시각을 읽는다(매 프레임 폴링 제거). 터치 `phase`가 `Canceled`인 입력은 판정에서 제외한다(OS의 palm rejection 등). 판정이 accept된 입력마다 좌/우 화면에 반투명 플래시로 즉시 시각 피드백을 준다(외부 240fps 탭→화면 지연 측정용).
- **고정 시나리오의 전체 부하 구현** — 크라우드에 실제 `Animator`를 연결했다(에디터 스크립트 `CrowdAnimatorSetup`이 회전 애니메이션 컨트롤러를 생성, 없으면 기존 수동 회전으로 대체). `audioVoiceCount`만큼 오디오 소스 풀을 만들어 박자마다 겹쳐 재생한다(`PlayOneShot`이라 팝 버그 재발 없음). `uiUpdatesPerBeat`(4개) 값을 박자마다 갱신해 화면에 표시한다. 시나리오의 `syntheticInputUs` 고정 입력 배열을 실행 중 자동으로 재생·판정한다 — 이제 사람이 3분 내내 탭하지 않아도 플랫폼 간 band 배열 비교(Gate 지표)가 매 실행마다 채워진다.
- **`BuildManifest` 자동 생성** — `SmokeBuild.Build()`가 커밋 해시·dirty 여부·시나리오/정책 해시·아티팩트 및 BuildReport의 SHA-256을 계산해 `BuildManifest.json`을 매 빌드마다 자동으로 쓴다. 정책이 아직 동결되지 않아도(현재 상태) 빌드가 막히지 않도록 `policyHash`는 `"unset-draft"`로 대체하며, 이 경우 `IsFinalGateEligible`이 여전히 최종 통과로 인정하지 않는다.
- **WebGL AudioContext 중단 감지 브리지** — `Assets/Plugins/WebGL/Gate0AudioContextBridge.jslib`를 추가했다. Unity 내부의 `Module.WEBAudio` 객체는 이번에 직접 읽어보다가 **브라우저가 응답 없이 멈추는 것을 실제로 확인**해 포기했고, 대신 독립적인 감시용 `AudioContext`를 만들어 그 `state`를 감시하는 방식으로 바꿨다(브라우저는 페이지 단위로 오디오 정책을 적용하므로 신뢰할 수 있는 대리 신호다). 실제 브라우저에서 행(hang)·오류 없이 빌드·로딩되는 것은 확인했지만, 진짜 브라우저 백그라운드 억제 상황(가짜 `visibilitychange` 이벤트로는 재현 불가)에서 정확히 동작하는지는 사람이 실제로 탭을 전환하거나 최소화해 봐야 확인된다.

### 새로 발견한 문제와 바로 고친 것

- 첫 구현에서 EditMode `BeatSchedulerTests`가 실패했다 — 박자 0은 경과시간 0에서 "바로" 발생하는 게 맞는 동작인데 테스트 기대값이 반대로 적혀 있었다(코드가 아니라 테스트를 고쳤다).
- `RealKeyboardTapDuringPlayingIsJudged` 테스트가 "예상 1, 실제 2"로 실패했다 — 버그가 아니라 새로 추가한 시나리오 자동 입력 재생이 같은 순간 배경에서 판정 1건을 더 만든 것이었다(디버그 로그로 실제 키보드 이벤트는 정확히 1번만 발생함을 확인). 테스트를 "정확히 1"이 아니라 "증가했는지"로 고쳤다.
- `BuildManifest`의 `buildId`가 WebGL 빌드마다 전부 `"WebGL"`로 똑같이 나오는 것을 빌드 후 직접 파일을 열어 확인했다(WebGL 출력 폴더가 한 단계 더 중첩돼 있어 폴더 이름만 따면 고유하지 않았다). 빌드 대상+타임스탬프 조합으로 고쳤다.

### 검증

- EditMode 50/50 통과(신규 3개: 무효화, 무효화 후 자동 재생 중단, 자동 재생 시간 경과 처리).
- PlayMode 11/11 통과 × 3회 반복(신규 1개: 중단 시 크라우드·세션 리셋 후 재개 시 완전히 새로 시작하는지 확인).
- WebGL 빌드 3회(레이아웃 수정, BuildManifest 추가, jslib 추가) 모두 성공·오류 0·경고 0.
- 브라우저 실제 조작: Start phase → 카운트인 → Playing 진입 후 `document.hidden` 토글에도 크래시·행 없이 유지되는 것을 스크린샷으로 확인. 단, 가짜 visibility 이벤트로는 Unity의 실제 중단 콜백이나 진짜 AudioContext 억제를 재현하지 못하므로 이 확인은 "안 깨진다"만 보증하고 "정확히 감지한다"는 실기기 확인이 필요하다.

## 브라우저로 직접 조작해 확인하고 고친 문제

자동 시험(Edit/Play Mode)은 로직이 맞는지 확인하지만 화면이 실제로 어떻게 보이는지는 확인하지 못한다. 그래서 Chrome을 직접 조작하는 도구(puppeteer-core, 이미 설치된 Chrome 사용)를 설치해 실제 WebGL 빌드를 열고 실제로 클릭해봤다. 그 과정에서 자동 시험이 잡지 못한 문제 3개를 발견해 고쳤다.

1. **버튼이 기존 문구와 겹침** — "Start phase" 버튼이 "DEVICE LAUNCH CHECK" 문구 위에 그려져 눈으로 보기 전엔 몰랐던 배치 문제였다. 버튼 줄을 빈 공간(y=270)으로 옮겼다.
2. **상태 문구가 안 바뀜** — Playing으로 넘어가도 화면엔 "Count-in..."이 계속 표시됐다. 상태 전이 시 문구를 갱신하도록 고쳤다.
3. **드물게 시작 보정이 실패로 판정됨** — PlayMode 테스트를 반복 실행하다 발견했다. 오디오 클록이 씬 시작 직후 아직 안정되기 전 표본이 섞여 보정 오차가 5ms를 넘는 경우가 있었다. phase 시작 보정은 그 순간의 단일 표본으로 확정하도록 고쳐 해결했다(재현: 3회 반복 실행 후 안정 확인).

고친 뒤 브라우저에서 다시 확인한 결과: Start phase 클릭 → 4박 카운트인(실제 클릭음) → Playing 전환 → 30명 크라우드가 실제로 화면에 나타나 회전 → 좌우 탭 시 Miss/Perfect/Good이 실시간 반영 — 전부 스크린샷으로 확인했다.

## 토스 배포

브라우저 확인까지 반영한 최신 빌드로 다시 패키징(`gate0:package-toss`)해 `mallang-brain` 프로필로 앱인토스에 배포(`ait deploy`)했다. 이는 로컬 패키징·배포이며 Gate 0 정식 측정이 아니다. `SmokeRecord.json`은 이 빌드를 `dirty: true`(커밋 전 변경 포함), `gateEligible: false`로 정확히 기록한다.

- 1차 패키징 근거: `Artifacts/T2/2026-09-12T09-28-34-243Z-package-toss/`, 배포 ID `01a094f2-7d32-79bd-8110-e7e04470e46c`

### 사용자 실기기 확인과 오디오 버그 발견·수정

사용자가 토스 앱에서 1차 배포본을 직접 실행해 확인했다. 카운트인·크라우드·판정(배경색 변화·카운트 증가·소리)은 정상 동작했다. 다만 **박자마다 스피커에서 "탁탁탁탁" 하는 팝 소리가 반복**됐고(탭으로 나는 소리는 정상), 이는 토스 앱(실기기) 기준 확인이다.

원인: 메트로놈 클릭을 매 박자 **같은 하나의 `AudioSource`**에 `PlayScheduled`로 재예약했다. Unity의 `AudioSource`는 한 번에 한 재생만 유지하므로, 직전 클릭이 채 끝나기 전(클립 길이 약 200ms, 박자 간격 500ms라 이론상 안 겹쳐야 하지만 실기기 스케줄링 오차로 겹칠 수 있음)에 새로 예약하면 이전 재생이 끊기며 팝 소리가 난다. 탭 소리는 `PlayOneShot`(즉시 재생, 재예약 없음)이라 영향이 없었다.

수정: `AudioSource` 두 개를 박자마다 번갈아 쓰도록 바꿔, 같은 소스가 최소 한 박자(500ms) 간격을 두고만 재사용되게 했다(카운트인 클릭과 phase 클릭도 같은 큐를 공유하도록 통일). `Runtime/Measurement/Gate0MeasurementController.cs`.

- 수정 후 EditMode 47/47, PlayMode 10/10 (2회 반복) 통과.
- 재빌드·재패키징·재배포: `Artifacts/T2/2026-09-12T09-50-29-427Z-build-webgl/`, `Artifacts/T2/2026-09-12T09-51-45-955Z-package-toss/`, 배포 ID `01a09507-b8a2-7308-b35b-56a3900332b0`.
- 새 테스트 스킴: `intoss-private://mallang-brain?_deploymentId=01a09507-b8a2-7308-b35b-56a3900332b0&host=appsInTossHost`
- **사용자가 토스 앱에서 재확인**: "소리 확인해봤는데 이제 깨끗하게 들려" — 팝 소리 해결을 실기기에서 직접 확인했다. 이는 해당 기기·해당 실행에서의 청취 확인이며, 아래 "아직 확인하지 않은 것"의 정식 A/V 측정(오차 수치 계측)을 대체하지 않는다.

## 검증 근거

Unity가 설치된 로컬(6000.3.23f1)에서 batchmode 자동 시험을 직접 실행했다. Node가 24 미만이라 `tools/gate0/run.mjs` 대신 Unity를 직접 호출했다(단, `build-webgl`/`package-toss`/`deploy`는 Node 24 경로로 정상 실행). 자동 시험과 브라우저 조작 확인 모두 장비 측정을 대신하지 않는다.

| 확인 | 결과 | 근거 |
| --- | --- | --- |
| Unity Edit Mode 시험 (화면 연결 전) | 41/41 통과 | `Artifacts/T4-T7-local/20260912T171603-test-edit/TestResults.xml` |
| Unity Play Mode 시험 (화면 연결 전) | 6/6 통과 | `Artifacts/T4-T7-local/20260912T171724-test-play/TestResults.xml` |
| Unity Edit Mode 시험 (화면 연결·버그 수정 후 최종) | 47/47 통과 (신규 32 + 기존 15) | `Artifacts/T8-local/*-test-edit-final2/TestResults.xml` |
| Unity Play Mode 시험 (화면 연결·버그 수정 후, 3회 반복) | 10/10 통과 × 3회 연속 | `Artifacts/T8-local/*-test-play-run{1,2,3}/TestResults.xml` |
| WebGL 플레이어 빌드 (최종) | 성공, 오류 0, 경고 0 | `Artifacts/T2/2026-09-12T09-25-24-567Z-build-webgl/WebGL/BuildReport.json` |
| 브라우저 실제 조작 확인 | Start phase → 카운트인 → Playing → 크라우드 생성 → 탭 판정까지 스크린샷으로 확인 | `/tmp/gate0-browser-check/` (세션 임시 경로, 저장소 밖) |
| 전체 스크립트 컴파일 | 오류 0 (시험·빌드 성공이 증거) | 각 폴더 `Unity.log` |

신규 시험이 덮은 테스트 계획 경로:

- E05 시계 보정 중앙값, E06 역행 무효화, E07 점프 무효화, E08 보정 오차 5ms 초과 무효화
- E09 판정 경계(-121·-120·-60·0·+60·+120·+121ms), E10 고정 배열 band 일치·결정성, E11 같은 손 중복 제거
- E13 dirty/누락 빌드는 최종 후보 불가, E14 필수 필드·checksum 누락 기록 거부, 덮어쓰기 거부
- P01 부팅 흐름, P02 카운트인 입력 차단, P06 중단→네 박자 재개, P07 오디오 실패→retry
- 부하 결정성과 30·60·100 카운트, 프레임 지표(평균 FPS·p95·p99·초과 프레임·연속 멈춤)
- 안드로이드 pause/focus 중복·역순 합치기, WebGL 오디오 컨텍스트 중단·retry(어댑터 MonoBehaviour 실측)

## 아직 확인하지 않은 것 (미측정 그대로 유지)

- 실제 안드로이드 기기·실제 토스 앱에서 **이 새 측정 화면**을 직접 눌러본 확인. 브라우저 확인은 로컬 PC Chrome 기준이며 실기기 확인을 대신하지 않는다.
- 실제 3분 측정(D01~D04): 9개 phase × 유효 5회, 안드로이드 3대(중급/저사양/고사양)·토스(캐시 삭제 Wi-Fi/warm/모바일망) 전체 행렬.
- 240fps 외부 영상·마이크로 잰 탭→화면, A/V offset·drift·jitter, 내부-외부 p95 차이 16.7ms 기준(D05).
- 하드웨어 입력 지연, 스피커 실제 소리 출력, cold/warm 로딩 시간.
- `Gate0AcceptancePolicy`의 실측 기기 목록은 여전히 빈 초안이라 정책이 동결되지 않았다. 컨트롤러와 `BuildManifest` 모두 이 상태를 감지해 `"unset-draft"`로 표시하고 진단 모드로 계속 동작한다.
- WebGL AudioContext 브리지가 진짜 브라우저 백그라운드 억제 상황에서 정확히 동작하는지(사람이 실제로 탭 전환·최소화해서 확인).
- 이 값들은 코드나 브라우저 확인으로 대신할 수 없다. 자동 시험·브라우저 확인 통과는 이 항목들의 통과를 뜻하지 않는다.

## 다음 진행 조건

1. 두 플랫폼(안드로이드·Apps in Toss)의 실제 실행·측정 증거가 모두 갖춰지고, 동결된 같은 시나리오·정책 해시로 Gate 지표를 전부 만족해야 Gate 0 통과다.
2. 홀드·점수·마림바 음색·캐릭터를 갖춘 콘테스트 제작은 **두 플랫폼의 기술 검증을 모두 통과한 뒤**에 시작한다(Gate 1).
3. 한쪽이라도 실패하거나 측정 불가면 Gate 1을 시작하지 않는다. 장비 측정을 미룬 것은 통과가 아니다.
