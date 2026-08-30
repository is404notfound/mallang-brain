# 말랑브레인음악대학 Gate 0 구현 계획

Source roadmap: [office-hours-result.md](office-hours-result.md)  
Review: `/plan-eng-review`, 2026-08-30  
Status: REVIEWED, READY TO IMPLEMENT

## Goal

콘텐츠와 백엔드를 만들기 전에 하나의 Unity 6 프로젝트가 Android 네이티브와 Apps in Toss Unity WebGL에서 리듬게임에 필요한 입력 반응성, 오디오 안정성, 로딩 시간, 프레임 안정성을 충족하는지 최대 7영업일 안에 검증한다.

Gate 0의 결과물은 출시 가능한 게임이 아니다. 같은 시나리오와 판정 정책으로 두 플랫폼을 측정하고 `통과`, `실패`, `측정 불가` 중 하나를 근거와 함께 기록하는 기술 스파이크다.

두 채널은 역할이 다르지만 모두 필수다.

- Android는 Google Play 자체등급분류와 배포 경로를 확보하는 채널이다.
- Apps in Toss는 실제 수익화를 검증할 핵심 채널이다.
- 한 채널만 통과한 상태로 Gate 1을 시작하지 않는다.

## Exit Decision and Timebox

```text
Day 0: 정책 동결 + Git/Unity 최소 초기화
                    │
Day 1~3: Android APK + Toss .ait 실제 실행
                    │
          ┌─────────┴─────────┐
          │                   │
      둘 다 실행됨       한쪽 패키징 불가
          │                   │
   측정 슬라이스 계속      부가 도구 중단
          │             원인·대안 판정 기록
          ▼
Day 7: 동일 정책으로 두 플랫폼 최종 판정
          │
   ┌──────┼──────────────┐
   │      │              │
둘 다 통과  한쪽 실패       측정 불가
   │      │              │
Gate 1   2일 이내 한 번만   증거와 막힌 조건 기록
계획 시작  수정 후 전체 재실행  새 기간은 별도 승인
          │
      재실패 시 Gate 1 중단
```

- 3일차까지 빈 공통 장면이 Android 실기기와 실제 Toss 샌드박스에서 실행돼야 한다.
- Gate 0 기본 기간은 최대 7영업일이다. 실패나 측정 불가도 유효한 종료 결과다.
- 한 번의 2일 수정 기회는 판정 정책을 바꾸지 않는다. 정책을 바꾸면 기존 결과를 폐기하고 전체 행렬을 다시 실행한다.
- 진단용 UI, 자동 리포트 꾸미기, 추가 계측은 Gate 지표 구현 뒤 시간이 남을 때만 한다.

## What Already Exists

- [office-hours-result.md](office-hours-result.md): 제품 가설, 6주 조건부 로드맵, 플랫폼 수용선의 원본이다. 제품 로드맵으로 계속 사용한다.
- [initial-game-spec.md](initial-game-spec.md): 초기 아이디어 기록이다. iOS 동시 출시와 선행 Supabase 구축 등 현재 결정과 충돌하므로 구현 기준으로 사용하지 않는다.
- [README.md](../README.md): 현재 단계, 두 플랫폼의 역할, 최신 문서 진입점을 제공한다.
- [.gitignore](../.gitignore): Unity 생성물, 로컬 빌드, 환경 파일을 제외한다. Unity `.meta` 파일은 커밋한다.
- [Gate 0 테스트 계획](/Users/easyoon/.gstack/projects/mallang-brain/easyoon-no-branch-eng-review-test-plan-20260830-153015.md): 27개 코드·사용자 경로와 5개 대상 기기 E2E 흐름을 정리했다.
- 구현 코드, Unity 프로젝트, 테스트 프레임워크, 빌드 파이프라인은 아직 없다.
- Ghost Dash의 코드와 분석 산출물은 이 저장소에 없으므로 재사용 가능하다고 가정하지 않는다.

## Scope

### In Scope

- Git 저장소 초기화, Unity 기준선 커밋
- Unity 6 프로젝트와 Android·WebGL 빌드 프로필
- Android와 Apps in Toss에서 공유하는 하나의 `SpikeScene`
- 실제 Android APK와 Toss 샌드박스용 `.ait`의 조기 수직 빌드
- DSP 메트로놈, 좌·우 탭, 즉시 VFX, 화면 플래시와 오디오 클릭 측정 신호
- 입력 시계와 DSP 시계의 명시적 보정·변환 계약
- 기술 검증용 `±60ms/±120ms` 원시 판정 프로브
- 테스트에서 시간을 수동 제어하는 작은 `IRhythmClock`
- 결정적 30·60·100개 군중 부하와 합성 최악 조건
- Android와 WebGL 중단 신호를 통합한 lifecycle 상태 머신
- Gate 지표, 진단 지표, 240fps 외부 표본 측정
- 불변 `BuildManifest`, 실행별 `RunRecord`, 원자료
- Unity Edit Mode, Play Mode, 대상 기기 E2E 테스트
- Gate 결과 보고서와 다음 단계 결정

## NOT in Scope

- 프로덕션 리듬 규칙: 점수, 콤보, 실패, 난이도 곡선은 Gate 1 계획에서 구현한다. Gate 0 판정 프로브는 점수나 게임 상태를 만들지 않는다.
- Supabase, nonce, 공식 랭킹, 백분위: 기술·재미 게이트 전에는 사용자 가치가 없고 Gate 0 실패 시 폐기된다.
- PostHog production 연동: Gate 0은 로컬 `RunRecord`만 사용한다.
- 범용 `IPlatformBridge`, `IBackendService`, `IAnalyticsService`: 실제 두 번째 소비자가 생기기 전에는 만들지 않는다.
- Toss 로그인·결제·저장 런타임 API: `.ait` 패키징과 샌드박스 실행만 Gate 0에서 확인하고, 수익 기능 통합은 Gate 1의 P1 TODO로 관리한다.
- 학점, 뇌 등급, 성적표, 외형 성장, 최종 아트: Gate 1 재미 통과 뒤에 연다.
- 광고 SDK, IAP 상품, 공유 카드, deep link: 공개 소프트런치 준비 단계로 미룬다.
- Poki, CrazyGames, iOS, Steam: 두 필수 채널의 기술 판단과 무관하다.
- CI/CD와 스토어 자동 배포: Gate 0은 재현 가능한 로컬 빌드까지만 만든다. 후속 P2 TODO로 관리한다.
- 원격 설정·실험 프레임워크: 버전이 있는 로컬 자산 두 개로 충분하다.

## System Boundary

```text
                         Shared Unity project
┌─────────────────────────────────────────────────────────────────────┐
│ SpikeScene                                                          │
│  ├─ Gate0Scenario ── deterministic phases                           │
│  ├─ Gate0AcceptancePolicy ── frozen pass thresholds                 │
│  ├─ ClockCalibration ── InputEvent.time ↔ dspTime                  │
│  ├─ RawTimingProbe ── offset only, no score/combo                   │
│  ├─ LifecycleController ◄── common interrupt/resume events          │
│  ├─ LoadDriver                                                      │
│  └─ MeasurementRecorder ── RunRecord + raw samples                  │
└─────────────────────────────────────────────────────────────────────┘
           ▲                         ▲                         ▲
           │                         │                         │
  Android lifecycle adapter   WebGL lifecycle adapter    Editor build scripts
  pause/focus coalescing       visibility/audio context   APK/AAB + WebGL/.ait
```

Toss 의존성은 두 종류로 분리한다.

1. **패키징 의존성:** Apps in Toss Unity 패키지와 `.ait` 생성 도구. Gate 0에 필요하지만 공통 런타임 코드에 연결하지 않는다.
2. **런타임 의존성:** 로그인·결제·브리지·저장 API. Gate 0에서는 만들지 않는다.

따라서 공통 스파이크 코드에는 Toss 인터페이스도 Android no-op 구현도 없다. 실제 런타임 호출이 생길 때만 WebGL 전용 assembly definition을 추가한다.

## Suggested Project Shape

```text
Assets/Mallang/Gate0/
├─ Runtime/
│  ├─ Clock/
│  ├─ Input/
│  ├─ Lifecycle/
│  ├─ Measurement/
│  └─ Workload/
├─ Platform/
│  ├─ Android/
│  └─ WebGL/
├─ Scenes/SpikeScene.unity
├─ Settings/
│  ├─ Gate0Scenario.asset
│  └─ Gate0AcceptancePolicy.asset
├─ Editor/Build/
└─ Tests/
   ├─ EditMode/
   └─ PlayMode/

Artifacts/
├─ Builds/<build_id>/
└─ Runs/<run_id>/
```

공통 런타임, 각 플랫폼 어댑터, 테스트에는 별도 `.asmdef`를 둔다. `Artifacts/Builds`의 바이너리는 Git에 커밋하지 않고 최종 manifest와 Gate 결과만 보존한다.

## Reproducibility Contracts

### Gate0Scenario

실행 조건만 담는 버전 있는 ScriptableObject다. 판정선을 넣지 않는다.

```text
Gate0Scenario
├─ schema_version
├─ scenario_id
├─ random_seed
├─ logical_resolution
├─ quality_level
├─ target_refresh_mode
├─ phases[]
│  ├─ phase_id
│  ├─ duration_seconds
│  ├─ crowd_count
│  ├─ animator_count / sprite_layers
│  ├─ vfx_spawn_count / elimination_schedule
│  ├─ audio_voice_count / ui_update_rate
│  └─ synthetic_input_timestamps
└─ content_hash
```

기본 부하는 30·60·100명 각각에 `idle`, `simultaneous_vfx`, `mass_elimination` 단계를 순서와 길이까지 고정한다. 100명 조건은 뇌마다 Animator 1개와 투명 스프라이트 최대 2장, 같은 박자의 최대 100개 일회성 VFX, 최대 8개 동시 오디오 voice, 박자당 4개 UI 값 갱신을 상한 대리 부하로 시작한다. 실제 Gate 1 장면이 생기면 같은 지표로 비교해 대리 부하를 교정한다.

### Gate0AcceptancePolicy

통과에 사용하는 수치와 표본 규칙만 담는 별도 ScriptableObject다.

```text
Gate0AcceptancePolicy
├─ schema_version / policy_id / content_hash
├─ required_devices_and_networks
├─ valid_run_count: 5
├─ clock_calibration_error_ms: 5
├─ android: cold_app_ready_s 5 / mean_fps 50 / tap_p95_ms 60
├─ toss: cold_playable_s 10 / mean_fps 50 / tap_p95_ms 60
├─ av_offset_abs_ms: 60
├─ av_drift_abs_ms: 20
├─ av_jitter_p95_ms: 20
├─ internal_external_gap_ms: 16.7
├─ external_sample_counts
└─ invalidation_rules
```

- Android Gate 기기는 중급 기기 1대다. 저·고사양 각 1대도 전 행렬을 실행하지만 결과는 진단 지표로 분리한다.
- Apps in Toss Gate 조건은 실제 Toss 앱 샌드박스의 cache-cleared Wi-Fi 실행이다. warm과 대표 모바일 네트워크 결과는 진단 지표로 분리한다.
- 로딩은 유효 5회 모두, FPS와 탭 지연은 각 workload의 유효 5회 모두 해당 수용선을 만족해야 한다.
- 시나리오나 정책 해시가 바뀌면 기존 측정과 합치지 않고 전체 행렬을 다시 실행한다.
- 자산이 없거나 schema가 맞지 않으면 기본값으로 진행하지 않고 시작을 막는다.

## Clock, Input, and Judgement Contract

Unity Input System의 `InputEvent.time`은 `Time.realtimeSinceStartup`과 같은 timeline의 초 단위 시각으로 취급한다. 오디오 일정은 `AudioSettings.dspTime`을 기준으로 한다.

```text
segment start / every 30s / resume / audio reconfiguration
                         │
                         ▼
 sample realtimeSinceStartup + dspTime → ClockCalibration(offset, error)
                         │
InputEvent.time ─────────┴─► DSP-domain timestamp
                         │
              invalid/non-monotonic/error > 5ms?
                    ┌────┴────┐
                   yes        no
                    │          │
         invalidate segment    ├─► immediate VFX
                               ├─► raw offset probe
                               └─► measurement sample
```

- `ClockCalibration`은 두 clock의 기준점, 단위, offset, 샘플 시각, 오차 추정치를 가진다.
- segment 시작, 30초 간격, resume, `AudioSettings.OnAudioConfigurationChanged` 뒤에 다시 보정한다.
- 음수 지연, 시계 역행, 5ms 초과 보정 오차가 생기면 해당 segment를 무효화하고 원자료를 보존한다.
- `IRhythmClock`은 시간 읽기만 책임진다. Edit Mode에서는 `ManualRhythmClock`으로 원하는 시각과 점프를 재현한다.
- `RawTimingProbe`는 cue 대비 입력 offset과 `±60ms`, `±120ms`, 그 밖의 band만 기록한다. 점수, 콤보, 실패, 난이도는 만들지 않는다.
- 같은 seed와 입력 timestamp 배열은 Android와 WebGL에서 같은 band 배열을 만들어야 한다.

## Lifecycle Contract

플랫폼별 신호를 공통 이벤트로 바꾸고 게임 상태 머신은 하나만 둔다.

```text
Boot ─► Idle ─► CountIn ─► Playing ─► Completed
  │       │         │          │
  │       └─────────┴──────────┴─ interrupt ─► Interrupted
  │                                              │ resume
  │                                              ▼
  └─ init/config error ─► Error ◄─ failure ─ Recalibrating
                              │ retry          │ success
                              └───────────────►┴─► CountIn
```

- Android adapter는 `OnApplicationPause`와 `OnApplicationFocus`의 중복·역순 신호를 coalesce해 공통 `Interrupt/Resume`을 한 번만 보낸다.
- WebGL adapter는 document visibility와 Web Audio `AudioContext` suspend/resume을 각각 감시한 뒤 공통 이벤트로 바꾼다.
- `CountIn` 도중 중단, 연속 중단, resume 중 재중단, 오디오 초기화 실패, retry를 모두 유효 전이로 정의한다.
- `Interrupted` 진입 시 입력 수집과 예약 오디오를 중단하고, 활성 hold와 대기 입력을 비우며, 현재 segment를 Gate 집계에서 제외한다.
- 복귀 시 clock을 재보정하고 오디오 준비가 끝난 뒤 4박 `CountIn`을 새로 시작한다.
- 상태 전이 표와 위 ASCII 다이어그램을 lifecycle 테스트 파일 상단 주석에도 유지한다.

## Measurement Architecture

### Gate Metrics

첫 판정을 직접 결정하는 지표다. 임계값이 없는 지표는 Gate 판정에 사용할 수 없다.

- Android 중급 기기 cold app-ready: 유효 5회 모두 5초 이하
- Toss cache-cleared Wi-Fi cold playable: 유효 5회 모두 10초 이하
- 각 플랫폼의 모든 30/60/100 × 3 workload 런: 평균 FPS 50 이상
- 입력→첫 실제 화면 변화: 내부 및 240fps 외부 p95 60ms 이하
- 화면 플래시와 오디오 클릭: 절대 offset 60ms 이하, 3분 drift 20ms 이하, jitter p95 20ms 이하
- 같은 표본에서 내부·외부 지연 p95 차이: 16.7ms 이하
- 동일 timestamp 배열의 판정 band: 플랫폼 간 100% 일치

### Diagnostic Metrics

원인을 찾고 다음 최적화를 고르지만 첫 Gate의 통과·실패를 단독으로 바꾸지 않는다.

- warm loading과 Apps in Toss 모바일 네트워크 단계별 loading
- frame time p95·p99, 33ms·50ms 초과 프레임 수, 최장 연속 멈춤
- 입력→VFX p50·p99와 모든 외부 원표본
- 프레임당 GC allocation
- 메모리 high-water와 3분 증가량
- Android 저·고사양 성능 곡선
- package/download/decompression/Unity initialization 구간 시간
- 중단, 재초기화, 잘못된 clock 변환 횟수

Gate 지표와 진단 지표는 `Gate0AcceptancePolicy`에서 구분한다. 진단 결과가 나빠 보여도 임의로 Gate를 뒤집지 않고 후속 정책 변경 제안으로 기록한다.

### External Measurement Protocol

- 탭→화면 변화는 기준 기기마다 240fps 영상으로 시작 20회와 3분 시점 20회를 채집한다.
- 같은 DSP cue에서 전체 화면 플래시와 짧은 오디오 클릭을 내보내 외부 카메라·마이크 한 파일에 담는다.
- A/V 표본은 시작 10회와 3분 시점 10회를 파형과 영상 frame으로 판독한다.
- 시작 offset, 종료 offset, drift, jitter, 표본 누락을 `RunRecord`에 함께 기록한다.
- 내부 timestamp와 외부 실제 onset의 p95 차이가 16.7ms를 넘으면 측정 정의 또는 clock 변환 오류로 보고 pass를 금지한다.
- 내부 DSP drift만으로 스피커 실제 출력 안정성을 주장하지 않는다.

## Experiment Protocol

1. 정책에 기기 모델, OS, 화면 주사율, 논리 해상도, DPR, Unity quality, 네트워크를 먼저 동결한다.
2. 배터리 절전 모드를 끄고 충전량 50% 이상에서 시작한다. 각 시나리오 전 워밍업 1회를 실행하고 결과에서 제외한다.
3. 9개 workload의 실행 순서를 기기별로 순환해 후반 발열이 특정 조건에만 몰리지 않게 한다.
4. 각 조건에서 유효 측정 5회를 실행한다. 중앙값과 최악값을 모두 보고하며 원래 실패한 런을 삭제하지 않는다.
5. 전화, 알림 오버레이, 수동 탭 실수, 네트워크 단절, lifecycle interrupt, clock 보정 오류는 사전 정의한 무효 사유다. 무효 원자료를 보존하고 같은 조건을 다시 실행한다.
6. 정상 완료됐지만 임계값을 넘긴 런은 무효화하지 않는다. 실패 표본으로 집계한다.
7. loading은 cold와 warm을 분리한다. Apps in Toss는 cache-cleared/warm 및 Wi-Fi/대표 모바일 네트워크를 각각 기록한다.

## Build, Packaging, and Records

기준선 다음 첫 구현 작업인 T2부터 실제 전달물을 만든다.

```text
build-android ──► APK/AAB ──► Android device launch smoke
build-webgl   ──► WebGL ──► Apps in Toss package tool ──► .ait sandbox smoke
       │
       └─► Unity BuildReport JSON + immutable BuildManifest

same build_id + device/protocol ──► immutable RunRecord + raw samples
```

명령 이름은 Unity 프로젝트 생성 시 실제 Editor build method로 확정한다. GUI에서만 바꿀 수 있는 숨은 설정을 허용하지 않는다.

### BuildManifest

빌드마다 한 번 생성하고 수정하지 않는다.

- `build_id`, source commit hash, `dirty` 상태
- Unity, Input System, Apps in Toss packaging SDK 버전
- target, build profile, scripting backend, quality, logical resolution
- scenario와 acceptance policy의 ID·schema·SHA-256
- artifact와 BuildReport의 SHA-256
- UTC build timestamp, build command, build result

최종 Gate pass용 빌드는 알려진 commit의 clean working tree여야 한다. dirty 빌드는 진단에는 쓸 수 있지만 최종 pass에는 사용할 수 없다.

### RunRecord

실행할 때마다 새 파일을 만들고 기존 기록을 덮어쓰지 않는다.

- `run_id`, `build_id`, scenario/policy hash
- 기기, OS, Toss 앱 버전, DPR, refresh rate, 네트워크
- cold/warm/cache 상태, phase 순서, 시작·종료 UTC
- 유효·무효·실패 상태와 사유
- Gate 지표, 진단 지표, 외부 측정 파일 경로
- 원자료 checksum과 recorder schema 버전

WebGL build script도 Unity `BuildReport`를 JSON으로 변환해 `.ait`와 같은 build directory에 보존한다. 필수 필드나 checksum이 빠지면 비교 가능한 Gate 결과로 인정하지 않는다.

## Test Strategy

Unity Test Framework를 사용한다.

```text
Edit Mode
  config/policy validation ─ clock conversion ─ aggregation ─ record validation
                                  │
Play Mode                         │
  scene/count-in/input/VFX ─ lifecycle transitions ─ visible errors
                                  │
Target-device E2E                 ▼
  build → install/sandbox → launch → 3-minute matrix → record → external sample
```

- Edit Mode: scenario·policy schema, deterministic phase order, clock 보정·역행·jump, band 경계, 집계, manifest/run record 검증, lifecycle 전이 표
- Play Mode: 장면 시작, count-in 입력 차단, 좌·우·멀티터치, cancellation, 즉시 VFX, Android/WebGL 공통 중단 이벤트, recount, 오류·retry UI
- Target-device E2E 5개: Android build/launch, Toss `.ait` sandbox launch, Android 3분 측정, Toss 3분 측정, 외부 A/V 표본과 내부 기록 대조
- 상세 입력과 27개 경로는 [Gate 0 테스트 계획](/Users/easyoon/.gstack/projects/mallang-brain/easyoon-no-branch-eng-review-test-plan-20260830-153015.md)을 따른다.

필수 경계 사례:

- count-in 중 입력과 중단
- 빠른 연타, 두 손가락 동시 입력, touch cancellation, press/release 역순
- Idle, CountIn, Playing, active hold의 중단
- 연속 focus/visibility 변화와 Android 중복 callback
- Web Audio context만 중단된 경우
- resume 도중 재중단, 오디오 재초기화 실패와 retry
- clock forward/backward jump와 보정 오차 초과
- 중단된 segment 제외, 실패 런 보존
- missing/corrupt scenario·policy, incomplete manifest/run record

## Failure Modes

| Path | Production failure | Test | Handling | User-visible result |
|---|---|---|---|---|
| Scenario/policy load | missing, incompatible, changed hash | Edit Mode | startup blocked | asset와 schema를 명시한 오류 |
| Android build | profile 또는 SDK 누락 | build smoke | non-zero exit, report 저장 | 실패한 설정과 command 출력 |
| Toss packaging | package tool 또는 `.ait` 생성 실패 | Toss build smoke | non-zero exit, partial artifact 거부 | 재현 명령이 있는 오류 |
| Clock calibration | negative, non-monotonic, error >5ms | Edit Mode | segment invalidated | 측정 오류, false pass 금지 |
| Raw judgement | platform별 band 불일치 | Edit + device E2E | Gate failure | 다른 offset 배열과 환경 표시 |
| Input | duplicate/cancelled touch | Edit + Play Mode | deduplicate or discard | 중복 VFX·표본 없음 |
| Android lifecycle | pause/focus 중복·역순 | Play + device E2E | adapter coalescing | 한 번의 4박 recount |
| WebGL lifecycle | hidden tab 또는 AudioContext만 suspend | Play + Toss E2E | common interrupt, audio reinit | 4박 recount 또는 retry |
| Audio resume | device init failure | Play + device E2E | Error 유지, retry 제공 | 복구 가능한 오류 화면 |
| Measurement | interrupted/invalid data aggregated | Edit Mode | segment 제외, raw 보존 | report에 무효 사유 표시 |
| Workload | phase 순서나 seed가 달라짐 | Edit + E2E | run rejected | 재실행 요구 |
| External capture | frame/audio sample 누락 | manual protocol check | run incomplete | 부족한 표본 수 표시 |
| BuildManifest | dirty/unknown source 또는 checksum 누락 | Edit Mode | final Gate eligibility 거부 | 진단 빌드로만 표시 |
| RunRecord | overwrite, missing build link | Edit Mode | immutable new ID, reject | 비교 불가 사유 표시 |
| Runtime load | FPS/latency threshold breach | device E2E | Gate failure | 원자료와 병목 지표 표시 |

계획된 모든 경로에는 테스트와 명시적 처리, 사용자 또는 측정자에게 보이는 실패 결과가 있다. 현재 무음 실패 critical gap은 0개다.

## Implementation Tasks

Synthesized from this review's findings. 각 작업은 선행 작업이 끝난 뒤 체크한다.

- [ ] **T1 (P1, human: ~2h / CC: ~20min)** — Repository — Git과 Unity 6 최소 프로젝트를 초기화한다
  - Surfaced by: Code Quality — 저장소와 테스트 런타임이 없음
  - Files: `.git/`, `.gitignore`, `ProjectSettings/`, `Packages/`, `Assets/Mallang/Gate0/`
  - Verify: clean baseline commit, Unity project opens without import/config errors
- [ ] **T2 (P1, human: ~6h / CC: ~2h)** — Packaging — 빈 공통 장면을 Android와 실제 Toss `.ait`로 빌드한다
  - Surfaced by: Outside Voice — 실제 Toss 패키징이 계획 후반에 있어 핵심 위험 발견이 늦음
  - Files: `Assets/Mallang/Gate0/Scenes/`, `Assets/Mallang/Gate0/Editor/Build/`, `ProjectSettings/`
  - Verify: Android device launch and Apps in Toss sandbox launch by Day 3
- [ ] **T3 (P1, human: ~4h / CC: ~1h)** — Configuration — 실행 시나리오와 판정 정책을 분리하고 hash 검증을 구현한다
  - Surfaced by: Architecture + Outside Voice — workload와 pass line 혼합, 재현 조건 누락
  - Files: `Assets/Mallang/Gate0/Runtime/Configuration/`, `Assets/Mallang/Gate0/Settings/`, `Assets/Mallang/Gate0/Tests/EditMode/`
  - Verify: Edit Mode schema/hash/determinism tests pass; changed policy rejects old runs
- [ ] **T4 (P1, human: ~8h / CC: ~2h)** — Timing — clock 보정, 즉시 VFX, 원시 판정 프로브를 구현한다
  - Surfaced by: Architecture + Tests + Outside Voice — clock epoch와 플랫폼 판정 일치 계약이 불완전함
  - Files: `Assets/Mallang/Gate0/Runtime/Clock/`, `Assets/Mallang/Gate0/Runtime/Input/`, `Assets/Mallang/Gate0/Tests/`
  - Verify: boundary/jump/recalibration tests pass and fixed timestamp arrays produce identical bands
- [ ] **T5 (P1, human: ~6h / CC: ~2h)** — Lifecycle — 공통 상태 머신과 Android/WebGL signal adapter를 구현한다
  - Surfaced by: Architecture + Tests + Outside Voice — 중단 상태와 플랫폼 신호가 섞여 있음
  - Files: `Assets/Mallang/Gate0/Runtime/Lifecycle/`, `Assets/Mallang/Gate0/Platform/`, `Assets/Mallang/Gate0/Tests/`
  - Verify: full transition matrix passes, including CountIn interrupt, duplicate callbacks, AudioContext suspend, retry
- [ ] **T6 (P1, human: ~8h / CC: ~3h)** — Workload — 결정적 합성 부하와 Gate/diagnostic 계측을 구현한다
  - Surfaced by: Performance + Outside Voice — 캐릭터 수만으로 최종 부하를 대표하지 못함
  - Files: `Assets/Mallang/Gate0/Runtime/Workload/`, `Assets/Mallang/Gate0/Runtime/Measurement/`, `Assets/Mallang/Gate0/Tests/`
  - Verify: same seed produces same phase/input schedule; 9-cell recorder outputs required raw metrics
- [ ] **T7 (P1, human: ~5h / CC: ~90min)** — Records — 불변 BuildManifest와 실행별 RunRecord를 만든다
  - Surfaced by: Code Quality + Outside Voice — 빌드·실행 metadata 혼합과 dirty/checksum 규칙 누락
  - Files: `Assets/Mallang/Gate0/Editor/Build/`, `Assets/Mallang/Gate0/Runtime/Measurement/`, `Artifacts/`
  - Verify: BuildReport JSON exported; dirty build cannot pass; records never overwrite and all SHA-256 checks validate
- [ ] **T8 (P1, human: ~8h / CC: ~3h)** — Tests — Edit Mode, Play Mode, 대상 기기 smoke suite를 완성한다
  - Surfaced by: Test Review — 27개 현재 gap과 5개 E2E 경로
  - Files: `Assets/Mallang/Gate0/Tests/EditMode/`, `Assets/Mallang/Gate0/Tests/PlayMode/`, device checklist
  - Verify: Unity batch Edit/Play tests pass and both target-device launch smokes are recorded
- [ ] **T9 (P1, human: ~2d / CC: ~4h)** — Experiment — 고정 프로토콜로 내부·외부 측정 행렬을 실행한다
  - Surfaced by: Performance + Outside Voice — 표본 수, 실행 순서, 외부 실제 출력 측정 규칙 누락
  - Files: `Artifacts/Runs/`, external 240fps video/audio samples
  - Verify: warmup + 5 valid runs per condition, required sample counts, no missing policy/build links
- [ ] **T10 (P1, human: ~4h / CC: ~1h)** — Decision — 두 필수 채널의 Gate 결과를 닫는다
  - Surfaced by: Scope + Outside Voice — 플랫폼 역할과 stop rule이 불명확함
  - Files: Gate result report, `Artifacts/Builds/`, `Artifacts/Runs/`
  - Verify: explicit pass/fail/unmeasurable verdict by Day 7; Gate 1 starts only if both pass
- [ ] **T11 (P3, human: ~1d / CC: ~3h)** — Gate 1 — 프로덕션 리듬 코어 계획을 작성한다
  - Surfaced by: Deferred TODO — 원시 판정 프로브를 실제 규칙과 분리
  - Files: `TODOS.md`, future Gate 1 plan
  - Verify: starts only after both Gate 0 channels pass
- [ ] **T12 (P3, human: ~3d / CC: ~1d)** — Infrastructure — Toss runtime API와 CI/CD 후속 범위를 실행한다
  - Surfaced by: Deferred TODO — 수익 API와 자동 배포는 Gate 0 바깥
  - Files: `TODOS.md`, future platform runtime and CI configuration
  - Verify: prerequisites in `TODOS.md` are satisfied before implementation

## Implementation Sequence

```text
T1 baseline
   │
T2 Android + Toss real build smoke ── Day 3 stop checkpoint
   │
T3 scenario/policy contracts
   │
T4 timing slice
   │
T5 lifecycle
   │
T6 workload/metrics
   │
T7 immutable records
   │
T8 automated/device tests
   │
T9 experiment matrix
   │
T10 decision ── Day 7 close
```

각 단계는 관련 테스트나 실제 전달물 smoke를 통과해야 다음 단계로 진행한다. T2는 다른 기능보다 앞서 두 플랫폼의 실제 패키징 위험을 제거한다.

## Parallelization

Sequential implementation, no parallelization opportunity. 초기 작업이 같은 Unity 프로젝트 설정, 공통 장면, 두 설정 자산, clock과 recorder 계약을 연속으로 바꾼다. 별도 worktree로 나누면 Unity scene·ProjectSettings·`.meta` 충돌 위험이 절약 시간보다 크다.

## Acceptance Criteria

Gate 0은 다음 조건을 모두 만족할 때만 통과한다.

1. clean commit에서 Android APK/AAB와 Toss `.ait`가 유효한 `BuildManifest`와 BuildReport JSON을 만든다.
2. Android 실기기와 실제 Toss 샌드박스에서 같은 `Gate0Scenario`와 `Gate0AcceptancePolicy` hash로 실행한다.
3. 모든 Edit Mode·Play Mode 테스트와 두 플랫폼 build/launch smoke가 통과한다.
4. Android 3대와 Apps in Toss의 정해진 workload·loading 행렬을 고정 프로토콜로 완료한다.
5. Gate metric의 모든 표본이 수치 임계값을 충족하고 내부·외부 p95 차이가 16.7ms 이하이다.
6. 같은 seed와 입력 timestamp 배열이 두 플랫폼에서 같은 `±60ms/±120ms` band 배열을 만든다.
7. 모든 실패·무효 원자료가 보존되고 최종 `RunRecord`가 빌드·시나리오·정책과 checksum으로 연결된다.
8. 7영업일 종료 시 `통과`, `실패`, `측정 불가` 중 하나와 근거를 기록한다.
9. Android와 Apps in Toss가 모두 통과하지 않으면 Gate 1 구현을 시작하지 않는다.

## GSTACK REVIEW REPORT

| Review | Trigger | Why | Runs | Status | Findings |
|--------|---------|-----|------|--------|----------|
| CEO Review | `/plan-ceo-review` | Scope & strategy | 0 | NOT RUN | — |
| Codex Review | `/codex review` | Independent 2nd opinion | 1 | RESOLVED | 14 findings, 14/14 folded into plan |
| Eng Review | `/plan-eng-review` | Architecture & tests (required) | 1 | CLEAR | 28 issues, 0 critical gaps |
| Design Review | `/plan-design-review` | UI/UX gaps | 0 | NOT RUN | — |
| DX Review | `/plan-devex-review` | Developer experience gaps | 0 | NOT RUN | — |

**CODEX:** 실제 양 플랫폼 빌드의 조기 검증, 설정·정책 및 빌드·실행 기록 분리, 외부 A/V 측정, 결정적 부하, lifecycle 분리, 7일 stop rule을 반영했다.

**VERDICT:** ENG CLEARED — Gate 0 구현을 시작할 준비가 됐다.

NO UNRESOLVED DECISIONS
