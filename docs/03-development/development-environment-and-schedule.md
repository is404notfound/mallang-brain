# 개발 환경과 Day 0~7 실행 계획

이 문서는 게임을 실제로 만들 때의 **하루하루 작업 순서표**다. 가장 중요한 약속은 간단하다. 기능을 많이 만들기 전에, 이 게임이 Android와 Apps in Toss에서 정말 실행되는지 먼저 확인한다.

1. Unity에서 게임을 만들고, Play 버튼으로 대부분을 바로 시험한다.
2. Android와 Apps in Toss 두 곳에서 빈 게임 화면이라도 먼저 실행한다.
3. 두 곳 모두에서 실행된 뒤에만 리듬 판정, 일시정지 처리, 성능 측정, 기록 저장을 만든다.
4. 정한 기준을 통과하지 못하면 다음 단계로 넘어가지 않는다.

문서에 나오는 `Gate 0`은 “이 기술로 게임을 계속 만들어도 되는가?”를 확인하는 첫 관문이다. `T1~T10`은 할 일 번호이고, `D1~D2`는 먼저 결정해야 할 문제 번호다. `STOP`은 반드시 확인하고 넘어가야 하는 멈춤 지점이다. 아래에는 같은 내용을 개발자가 바로 실행할 수 있도록 자세히 적어 두었다.

작성: 2026-08-31
범위: **개발 루프 안내 + 개발 환경 구성 + Gate 0 Day 0~7 실행 상세 + Gate 0 이후 마일스톤 개괄**
입력 문서: [Gate 0 구현 계획](../02-gate-0/implementation-plan.md), [제품 비전과 6주 로드맵](../01-product/product-vision-and-roadmap.md), [TODOS](../../TODOS.md)

이 문서는 기존 계획을 대체하지 않는다. Gate 0 구현 계획은 **무엇을 만들고 무엇으로 판정하는가**를 정의하고, 이 문서는 **이 맥북에서 어떤 순서로 실제로 실행하는가**를 정의한다. 두 문서가 충돌하면 Gate 0 구현 계획의 판정 정책이 우선하고, 이 문서는 실행 경로만 조정한다.

---

## 개발 루프 — 어디서 개발하고 어디서 테스트하는가

### 핵심: Unity Editor가 시뮬레이터다

Unity Editor는 텍스트 에디터가 아니라 **게임 엔진이 실제로 돌고 있는 프로그램**이다. Play 버튼을 누르면 게임 런타임이 에디터 프로세스 안에서 그대로 실행된다. 브라우저를 따로 띄우는 게 아니라 이미 켜져 있는 상태에 가깝다. 그래서 별도 시뮬레이터 단계가 필요 없다.

### React/Next 감각과의 대응

| React/Next 세계            | Unity 세계                                       |
| -------------------------- | ------------------------------------------------ |
| VS Code / Cursor           | VS Code / Rider — **그대로 쓴다** (C# 파일 편집) |
| `npm run dev` + 브라우저   | **Unity Editor의 Play Mode**                     |
| Chrome DevTools            | Unity Inspector + Profiler                       |
| Vitest / Jest              | Unity Test Framework (Edit Mode / Play Mode)     |
| Storybook                  | Scene + Prefab                                   |
| 브라우저 반응형 뷰         | Device Simulator 패키지                          |
| **iOS/Android 시뮬레이터** | **이 칸은 비어 있다. 실기기로 직행한다.**        |
| Vercel 프리뷰 배포         | APK 빌드 / `.ait` 빌드                           |

### 하루의 루프

```text
VS Code에서 C# 저장
      │  Unity Editor가 자동 감지, 수 초 컴파일
      ▼
Play Mode 실행 — 마우스 클릭이 좌·우 말렛 탭 대신
      │
      ▼
Edit/Play Mode 테스트 배치 실행 (분 단위)
      │
──────── 여기까지가 하루의 90% ────────
      │
      ▼
(판정하는 순간에만) APK / .ait 빌드 → 실기기
```

### 보유 기기 및 계정 (사용자 확인)

- Android 실기기: 고급, **중급 1대만 보유.** 저사양 기기 없음.
- Apps in Toss: 계정 보유. Ghost Dash로 샌드박스 동작 확인됨. **mallang-brain용 앱 등록·환경 구성은 미완.**

---

## Part 1. 개발 환경 구성

각 단계는 검증 명령이 통과해야 다음으로 넘어간다. `Verify`가 실패하면 다음 단계를 시작하지 않는다.

### S1. Unity Android Build Support 설치 (약 40분, 다운로드 대기 포함)

Unity Hub CLI 문법은 이 머신에서 확인했다.

```bash
HUB="/Applications/Unity Hub.app/Contents/MacOS/Unity Hub"
"$HUB" -- --headless install-modules --version 6000.4.4f1 -m android --childModules
```

`--childModules`가 Android SDK & NDK Tools, OpenJDK, platform-tools를 함께 설치한다. CLI가 실패하면 Unity Hub GUI의 `Installs → 6000.4.4f1 → Add modules`로 동일 작업을 한다.

**Verify:**

```bash
ls "/Applications/Unity/Hub/Editor/6000.4.4f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"
ls "/Applications/Unity/Hub/Editor/6000.4.4f1/PlaybackEngines/AndroidPlayer/OpenJDK/bin/java"
```

### S2. `adb` PATH 등록 + 실기기 인식 (약 15분)

Unity 번들 SDK의 platform-tools를 쓴다. 별도 Android Studio를 설치하지 않는다(SDK 버전이 두 개로 갈라지면 빌드 재현성이 깨진다).

```bash
echo 'export ANDROID_SDK_ROOT="/Applications/Unity/Hub/Editor/6000.4.4f1/PlaybackEngines/AndroidPlayer/SDK"' >> ~/.zshrc
echo 'export PATH="$ANDROID_SDK_ROOT/platform-tools:$PATH"' >> ~/.zshrc
```

기기에서 개발자 옵션 → USB 디버깅을 켜고 USB 연결 후 인증 팝업을 승인한다.

**Verify:** `adb devices`에 기기 1대가 `device` 상태로 나온다. 동시에 **모델명·OS 버전·화면 주사율**을 기록한다 (D1의 정책 동결 입력).

```bash
adb shell getprop ro.product.model; adb shell getprop ro.build.version.release
adb shell dumpsys display | grep -i "fps\|refresh" | head -3
```

### S3. Node 버전 고정 및 AIT 툴체인 설치 (약 15분)

```bash
cd ~/Desktop/projects/mallang-brain
echo "20" > .nvmrc
source "$HOME/.nvm/nvm.sh" && nvm use
npm init -y
npm i -D @apps-in-toss/web-framework@^3.0.2
```

**Verify:** `node -v`가 v20.x, `npx ait --help`가 정상 출력.

> 기본 PATH의 Node는 v23.7.0이다. `.nvmrc` 없이 작업하면 툴체인 버전이 세션마다 달라진다. 빌드 재현성 계약(`BuildManifest`)의 일부로 취급한다.

### S4. 측정 도구 설치 (약 10분)

```bash
brew install ffmpeg
```

`ffmpeg`는 T9의 240fps 영상에서 프레임을 뽑아 탭→화면변화 지연을 판독하는 데 쓴다. 240fps 촬영은 iPhone 슬로모(240fps) 등 보유 기기로 한다. **촬영 기기와 fps를 정책에 기록**한다.

**Verify:** `ffmpeg -version`

### S5. Unity용 `.gitignore` 교체 (약 10분, P0)

현재 `.gitignore`는 Node/Next.js용이다. 이대로 T1을 하면 `Library/`(수 GB)가 커밋된다.

포함해야 할 항목: `[Ll]ibrary/`, `[Tt]emp/`, `[Oo]bj/`, `[Bb]uild/`, `[Bb]uilds/`, `[Ll]ogs/`, `[Uu]ser[Ss]ettings/`, `*.csproj`, `*.sln`, `*.unitypackage`, `.vs/`, `Artifacts/Builds/`, `node_modules/`, `.env*`.

**보존해야 할 항목** (Gate 0 판정 증거): `Artifacts/Runs/`, `Artifacts/Builds/**/BuildManifest.json`, `Artifacts/Builds/**/BuildReport.json`. 바이너리는 제외하고 manifest·report·run record는 커밋한다.

**Verify:** T1 직후 `git status --short | wc -l`이 수백 줄이 아니라 프로젝트 설정 수준(수십 줄)이다. `git count-objects -vH`의 size가 급증하지 않는다.

### S6. Apps in Toss `mallang-brain` 앱 환경 구성 (약 40분, 사람 전용)

1. Toss 개발자 콘솔에 `mallang-brain`(또는 확정 appName) 앱을 신규 등록한다.
2. `apps-in-toss.config.ts`를 Ghost Dash 것을 베이스로 작성한다. **핵심 차이는** `webBundleDir`**를 Unity WebGL 출력 폴더로 지정하는 것**이다.
3. 가로 잠금은 Ghost Dash와 마찬가지로 설정 파일이 아니라 부팅 시 `setDeviceOrientation(landscape)` 호출로 보증한다. Unity WebGL에서는 이 호출을 JS 브리지로 넣어야 하므로 **WebGL 전용 경계**에 둔다(공통 런타임에 넣지 않는다).
4. **D2 확인:** 샌드박스가 가로 게임을 지원하는지 실제로 시도한다. 미지원이면 측정 경로를 `ait deploy` + 콘솔 테스트 QR로 확정하고 D2 분기를 닫는다.

**Verify:** 콘솔에 앱이 존재하고, 최소한 하나의 정적 HTML 번들이 `.ait`로 패키징되어 실기기에서 실행된다. (Unity 빌드 전에 "Hello" 정적 페이지로 먼저 경로를 통과시키는 것을 권장한다 — 실패 원인을 Unity와 Toss 중 하나로 격리할 수 있다.)

### S7. (선택) `gh` CLI

`brew install gh`. Gate 0에서는 필수가 아니다. PR·이슈 자동화가 필요해지는 Gate 1 이후에 설치해도 된다.

### 환경 구성 완료 게이트

아래 4개가 모두 통과해야 Day 0 오후(정책 동결)로 넘어간다.

- [ ] `adb devices`에 Gate 기기 1대 인식 + 모델·OS·주사율 기록
- [ ] Unity Editor에서 Android / Web 두 빌드 타깃이 모두 선택 가능
- [ ] `npx ait --help` 정상 + `.nvmrc` = 20
- [ ] 정적 "Hello" 번들이 `.ait`로 패키징되어 실기기 실행 (D2 분기 확정)

---

## Part 2. Gate 0 Day 0~7 실행 플랜

원 문서의 T1~T10을 그대로 쓰되, D1~D2 조치를 일자에 배치했다. 각 일자 끝의 `STOP`은 통과하지 못하면 다음 일자로 넘어가지 않는 조건이다.

### Day 0 — 환경 구성 + 정책 동결 (T1, D1~D2)

| 오전 | Part 1의 S1~S7 전체                                 |
| ---- | --------------------------------------------------- |
| 오후 | D1·D2 결정 반영, T1 저장소·Unity 프로젝트 초기화 |

오후 상세:

1. **D1 반영** — `Gate0AcceptancePolicy`의 기기 행렬을 중급 1대로 동결. 저·고사양은 `측정 안 함(미보유)` 기록. Gate 0 계획서 Acceptance Criteria 4번 수정.
2. **D2 반영** — S6에서 확인한 Toss 측정 환경을 정책에 동결. 샌드박스/실앱 중 어느 것인지, 캐시 제어 절차가 무엇인지 명시.
3. **T1** — Unity 6 프로젝트 생성(Assets/Mallang/Gate0 골격), `.asmdef` 3종(공통 런타임 / 플랫폼 어댑터 / 테스트), 기준선 커밋.

**STOP:** Unity 프로젝트가 import·config 오류 없이 열리고, clean baseline 커밋이 있고, `git status`가 오염되지 않았다. 정책 자산 2종(`Gate0Scenario`, `Gate0AcceptancePolicy`)의 **필드 정의가 확정**되었다(값 채우기는 T3).

### Day 1~3 — 양 채널 실제 빌드 (T2) — **최대 리스크 구간**

원 문서가 T2를 가장 앞에 둔 이유는 패키징 실패가 가장 늦게 발견되면 7일을 다 태우기 때문이다. 이 구간에는 기능을 만들지 않는다. **빈 공통 장면만** 두 채널에서 실행시킨다.

**Day 1 — Android 수직 관통**

- `SpikeScene`(빈 장면 + 화면 전체 단색 + 탭 시 색 변경) 하나만 만든다.
- Editor build script로 APK 생성 → `adb install` → 실기기 launch.
- 명령은 GUI가 아니라 **Editor build method**로 확정한다(원 문서 요구). GUI에서만 바꿀 수 있는 숨은 설정을 허용하지 않는다.
- **STOP:** APK가 실기기에서 실행되고 탭에 반응한다.

**Day 2 — Toss WebGL 수직 관통 (패키징 하위 리스크 순서대로)**

1. WebGL 빌드, **압축 Disabled**, 단일 스레드, 로그 활성.
2. 출력 폴더를 `webBundleDir`로 지정 → `npx ait build` → `.ait` 생성.
3. 실기기에서 `.ait` 실행 (S6에서 확정한 경로: 샌드박스 또는 콘솔 테스트 배포).
4. 오디오 unlock: 첫 탭에서 `AudioContext`가 열리는지 확인.
5. 가로 고정이 실제로 적용되는지 확인.

- **STOP:** `.ait`가 실기기에서 실행되고, 가로이며, 탭에 반응하고, 소리가 난다.

**Day 3 — Day 3 체크포인트 (원 문서의 명시적 중단 지점)**

- 양쪽 launch smoke를 각각 기록으로 남긴다.
- 압축 활성화 시도 및 용량 측정(실패해도 Disabled로 Gate 0을 진행할 수 있다 — 단 `BuildManifest`에 압축 설정을 기록).
- **STOP (원 문서 규정):** 빈 공통 장면이 Android 실기기와 Toss 실환경에서 **둘 다 실행**되어야 한다. 한쪽이 패키징 불가면 부가 도구 작업을 중단하고 **원인·대안 판정을 기록**한다. 여기서 막히면 Day 4 이후를 진행하지 않는다.

### Day 4 — 재현성 계약 + 타이밍 (T3, T4)

- **T3** — `Gate0Scenario`(실행 조건) / `Gate0AcceptancePolicy`(판정선) 분리, `content_hash`(SHA-256) 검증, 자산 누락·schema 불일치 시 **기본값으로 진행하지 않고 시작 차단**.
- **T4** — `ClockCalibration`(`InputEvent.time` ↔ `AudioSettings.dspTime`, 8회 표집 median), `IRhythmClock` / `ManualRhythmClock`, 즉시 VFX, `RawTimingProbe`(±60/±120ms band만, **점수·콤보 없음**).
- Edit Mode 테스트를 이 시점부터 함께 쓴다(-121/-120/-60/0/+60/+120/+121ms 경계, clock 역행·jump, 5ms 초과 보정 오차 시 segment 무효화).
- **STOP:** 고정 timestamp 배열이 Editor에서 결정적으로 같은 band 배열을 만든다.

### Day 5 — Lifecycle + 부하·계측 (T5, T6)

- **T5** — 공통 상태 머신(`Boot→Idle→CountIn→Playing→Completed` + `Interrupted`/`Recalibrating`/`Error`), Android 어댑터(`OnApplicationPause`/`OnApplicationFocus` 중복·역순 coalescing), WebGL 어댑터(document visibility + `AudioContext` suspend/resume 독립 감시).
- **T6** — 결정적 합성 부하 9칸(30·60·100명 × `idle`/`simultaneous_vfx`/`mass_elimination`), Gate 지표·진단 지표 recorder.
- **STOP:** 전이 행렬 테스트 전체 통과(카운트인 중 중단, 연속 중단, resume 중 재중단, 오디오 초기화 실패·retry 포함). 같은 seed가 같은 phase·입력 스케줄을 만든다.

### Day 6 — 불변 기록 + 테스트 완성 (T7, T8) 그리고 측정 시작

- **T7** — `BuildManifest`(빌드당 1회, 불변, dirty working tree는 최종 pass 불가), `RunRecord`(실행마다 새 파일, 덮어쓰기 금지), Unity `BuildReport` JSON 내보내기, SHA-256 체인.
- **T8** — Edit/Play Mode 배치 실행 + 대상 기기 smoke. 입력은 [Gate 0 테스트 계획](../02-gate-0/test-plan.md)이다.
- 오후부터 **T9 측정 착수**(9칸 × 유효 5회는 시간이 오래 걸린다 — 워밍업 1회 제외, 순서 순환, 절전 해제, 충전 50% 이상).
- **STOP:** dirty 빌드가 최종 pass 후보가 되지 못하고, RunRecord가 절대 덮어써지지 않으며, 모든 checksum이 검증된다.

### Day 7 — 측정 마감 + 판정 (T9 잔여, T10)

- **T9 잔여** — Android·Toss 각각 3분 행렬 완료 + **외부 측정**: 240fps 영상으로 탭→화면변화 시작 20회·3분 시점 20회, A/V 플래시·클릭 시작 10회·3분 시점 10회.
- **T10** — Gate 결과 보고서 작성. `통과` / `실패` / `측정 불가` 중 하나를 **근거와 함께** 기록.
- **최종 게이트:** 내부·외부 p95 차이 ≤ 16.7ms. 이 조건이 깨지면 측정 정의 또는 clock 변환 오류로 보고 **pass를 금지**한다.
- **STOP:** Android와 Apps in Toss가 **둘 다 통과**하지 않으면 Gate 1 구현을 시작하지 않는다.

### 실패 시 분기 (원 문서 규정 유지)

- 한쪽 실패 → **2일 이내 한 번만** 수정 후 **양 플랫폼 전체 행렬 재실행**. 이때 **판정 정책을 바꾸지 않는다.** 정책을 바꾸면 기존 결과를 폐기하고 전체를 다시 실행한다.
- 재실패 → Gate 1 중단. 엔진·패키징·전달 구조를 재결정한다.
- 측정 불가 → 증거와 막힌 조건을 기록. 새 기간은 별도 승인 사항.

---

## Part 3. Gate 0 이후 마일스톤 개괄

Gate 0이 실패하면 아래는 폐기된다. 그래서 지금 상세화하지 않는다. **진입 조건과 산출물만** 정의한다.

### Gate 1 — 첫 재미 (로드맵 Week 2~3)

- **진입 조건:** Gate 0에서 Android·Toss **둘 다 통과**.
- **첫 작업:** `TODOS.md`의 P1 "리듬 코어 구현 계획 작성". Gate 0의 `RawTimingProbe`를 확장하지 않고, 로드맵의 코어 규칙 기본값(20스테이지, 80→140 BPM, Perfect ±60 / Good ±120, ghost_miss -30, 스테이지별 통과선, rank tuple)을 **별도 Gate 1 계획으로 옮긴다.**
- **핵심 산출물:** 좌·우 탭 + 홀드 + 정확도, 12개 패턴 블록과 20스테이지 조합표, 사용자 포함 100뇌 + 스테이지 탈락, 혼합 합주 순위 결과 화면, `다시 참가` CTA.
- **판정:** 진단 20명 → 한 번 수정 → **독립 검증 20명**. 첫 스테이지 완료율 ≥80% 이고 (재도전 ≥40% / 5판 ≥25% / 첫 세션 콘테스트 시간 기준) 중 2개 이상 통과 → Continue.
- **금지:** Supabase 공식 랭킹, 학년 전체, 다중 악기, 최종 아트, 연습실.
- **비고:** 이 단계의 상대는 공개된 스크립트 연습생 곡선이다. 실제 참가자 백분위는 아직 표시하지 않는다.

### Gate 1 통과 후 (로드맵 Week 4~5)

- 4A: aggregated 판정 검증, idempotency, 데일리 seed, Supabase 최고기록, official/practice 분리. **상위 기록 replay 재판정은 비동기·수동 운영.**
- 4B: 같은 RhythmCore를 쓰는 3분 무실패 연습실. 10명 행동 관찰. (10명 중 4명 이상이 연습실만 하거나 콘테스트 시작이 20% 이상 줄면 → 홈의 연습실 CTA를 결과 화면 안으로 내리고 학점은 contest-only로 출시)
- 4C: 4개 연주 프로필 + 1학년 12학점.
- Week 5: 제1합주실 한 공간, 캠퍼스 홈 한 장, 결과 도장. Android 폐쇄 알파 최대 40명. **여기서 저사양 기기 리스크(D1)가 처음 드러난다 — 알파 기기 분포를 반드시 수집한다.**
- 4A 지연 → 4B·4C 자동 이월. 한 주에 우겨 넣지 않는다.

### Milestone 2 — Gate 2 (6주 밖)

- Google Play 자체등급분류(**Ghost Dash의 GRAC 증빙 문서 재사용**), Apps in Toss 공개, `TODOS.md`의 Toss 런타임 API 통합(로그인·결제), CI/CD 자동화.
- 공개 신규 100명 이상에서 D1 ≥15% → Continue investing, ≥20% → 강한 신호.

### 코드 전에 해야 할 일 (로드맵 The Assignment)

로드맵은 **코드를 시작하기 전에** 10명에게 홈 화면을 5초 보여주는 메시지 테스트를 요구한다. 이것은 Gate 0(기술 검증)과 **병렬로 진행할 수 있고, 그래야 한다.** Gate 0의 7일은 대부분 빌드·측정 대기 시간이므로, 그 사이에 사람이 10명 인터뷰를 돌리는 것이 총 기간을 줄인다. 와이어프레임(`docs/music-university-wireframe.html`)은 이미 승인되어 있어 지금 바로 보여줄 수 있다.

---

## Part 4. 실행 주체 분담

D5에서 확인한 대로, 7영업일은 **CC가 구현하고 사람이 측정·판정할 때만** 성립한다.

| 작업                                     | 주체                                 | 근거                        |
| ---------------------------------------- | ------------------------------------ | --------------------------- |
| S1·S3·S4·S5 (모듈·Node·ffmpeg·gitignore) | CC                                   | 명령 실행 + 검증            |
| S2 기기 연결, S6 Toss 콘솔·앱 등록       | **사람**                             | 물리 연결, 계정 권한        |
| D1~D2 정책 결정                          | **사람이 결정**, CC가 문서·자산 반영 | 판정선 변경은 제품 판단     |
| T1·T3·T4·T5·T6·T7 구현                   | CC                                   | 코드·테스트                 |
| T2 빌드 스크립트                         | CC                                   | Editor build method         |
| T2 실기기 install·launch 확인            | **사람**                             | 물리 기기·Toss 앱           |
| T8 배치 테스트 실행                      | CC                                   | `-batchmode` 실행           |
| T9 측정 행렬 (9칸 × 5회 × 2플랫폼)       | **사람**                             | 실기기 조작, 발열·순서 통제 |
| T9 외부 240fps 촬영·판독                 | **사람** (판독은 CC가 ffmpeg 보조)   | 촬영 필요                   |
| T10 판정                                 | **사람이 결정**, CC가 보고서 작성    | Gate 판정은 제품 판단       |
| 메시지 테스트 10명                       | **사람**                             | 인터뷰                      |

**사람의 시간이 병목인 구간은 Day 2(Toss 실기기), Day 6~7(측정)이다.** 이 3일은 다른 일정을 넣지 않는다.

---

## Part 5. 이 플랜의 미확인 가정 (Day 0에 검증)

계획을 단정으로 쓰지 않기 위해 분리한다. 각 항목은 Day 0 안에 사실로 바꾸거나 리스크로 기록한다.

1. **Toss 샌드박스의 가로 게임 지원 여부** (D2). Ghost Dash 시점 주석이 근거이고, 현재도 유효한지 확인하지 않았다.
2. **Unity WebGL 출력이** `webBundleDir`**로 그대로 패키징되는지**. 실제 T2에서 검증한다.
3. `.ait` **용량 상한**. 24MB 선례만 있고 상한을 모른다.
4. **Unity Hub CLI** `install-modules`**의 실제 동작**. 문법은 이 머신의 `help`로 확인했으나 실행은 하지 않았다.
5. **Gate 기기의 모델·OS·주사율**. S2에서 확정한다. 정책 동결의 필수 입력이다.
6. **240fps 촬영 기기 보유 여부**. 없으면 외부 측정 프로토콜이 성립하지 않고, Gate 지표 중 "내부·외부 p95 차이 ≤16.7ms"를 판정할 수 없다 → 이 경우 해당 지표를 `측정 불가`로 기록해야 하며, 원 문서 기준으로는 **완전한 pass가 불가능**하다. Day 0에 반드시 확인한다.

---

## 용어 한 줄 정리

- **Gate 0 (기술 게이트):** 재미나 콘텐츠를 만들기 전에, 두 플랫폼이 리듬게임에 필요한 반응성·오디오·프레임을 낼 수 있는지만 판정하는 최대 7일 실험.
- `.ait` **(앱인토스 번들):** 정적 웹 번들을 앱인토스가 실행할 수 있는 형태로 감싼 패키지 파일. `ait build`가 만든다.
- **DSP 시계(**`AudioSettings.dspTime`**):** 오디오 하드웨어가 세는 시간. 화면 프레임과 별개로 흘러서, 리듬 판정의 기준점으로 쓴다.
- **clock 보정(ClockCalibration):** 입력이 찍힌 시간과 소리가 나는 시간이 서로 다른 자를 쓰기 때문에, 두 자의 눈금 차이(offset)를 주기적으로 재는 일.
- **Play Mode:** 빌드 없이 Unity Editor 안에서 게임을 바로 돌리는 모드. Unity 반복 개발의 본진이다.
- **AOT 컴파일** (ahead-of-time, 미리 번역): 실행 전에 기계어로 번역해두는 방식. Unity Android가 이걸 쓰기 때문에 Expo식 핫 리로드가 불가능하다.
- **granite:** 토스의 React Native 프레임워크. 앱인토스 플랫폼의 토대이고, 우리는 그 위 WebView 칸만 쓴다.
- `RunRecord` **/** `BuildManifest` **(불변 기록):** "이 빌드로, 이 조건에서, 이 결과가 나왔다"를 나중에 못 고치게 박아두는 파일. 측정 결과를 신뢰할 수 있게 만드는 장치.
