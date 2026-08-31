# 말랑브레인음악대학 전체 개발 플랜

작성: 2026-08-31
범위: **개발 루프 안내 + 개발 환경 구성 + Gate 0 Day 0~7 실행 상세 + Gate 0 이후 마일스톤 개괄**
입력 문서: [Gate 0 구현 계획](gate-0-implementation-plan.md), [6주 제품 로드맵](office-hours-result.md), [TODOS](../TODOS.md)

이 문서는 기존 계획을 대체하지 않는다. Gate 0 구현 계획은 **무엇을 만들고 무엇으로 판정하는가**를 정의하고, 이 문서는 **이 맥북에서 어떤 순서로 실제로 실행하는가**를 정의한다. 두 문서가 충돌하면 Gate 0 구현 계획의 판정 정책이 우선하고, 이 문서는 실행 경로만 조정한다.

---

## 개발 루프 — 어디서 개발하고 어디서 테스트하는가

이 절은 Unity가 처음인 사람이 이 문서의 기기 행렬을 읽고 "모든 테스트를 실기기에서 하나"로 오해하는 것을 막기 위해 있다. **아니다. 하루의 90%는 실기기 없이 진행한다.**

### 핵심: Unity Editor가 시뮬레이터다

Unity Editor는 텍스트 에디터가 아니라 **게임 엔진이 실제로 돌고 있는 프로그램**이다. Play 버튼을 누르면 게임 런타임이 에디터 프로세스 안에서 그대로 실행된다. 브라우저를 따로 띄우는 게 아니라 이미 켜져 있는 상태에 가깝다. 그래서 별도 시뮬레이터 단계가 필요 없다.

### React/Next 감각과의 대응

| React/Next 세계 | Unity 세계 |
|---|---|
| VS Code / Cursor | VS Code / Rider — **그대로 쓴다** (C# 파일 편집) |
| `npm run dev` + 브라우저 | **Unity Editor의 Play Mode** |
| Chrome DevTools | Unity Inspector + Profiler |
| Vitest / Jest | Unity Test Framework (Edit Mode / Play Mode) |
| Storybook | Scene + Prefab |
| 브라우저 반응형 뷰 | Device Simulator 패키지 |
| **iOS/Android 시뮬레이터** | **이 칸은 비어 있다. 실기기로 직행한다.** |
| Vercel 프리뷰 배포 | APK 빌드 / `.ait` 빌드 |

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

이 프로젝트는 특히 유리하다. Gate 0에서 만드는 코드의 대부분 — clock 보정, 판정 프로브, lifecycle 상태 머신, recorder — 는 **화면이 필요 없는 순수 C#**이다. 계획서가 `IRhythmClock`으로 시간을 추상화하고 판정 입력을 `int64 microseconds` 정수로 정규화한 이유가 이것이다. 렌더링 없이, Play Mode 없이 **Edit Mode 테스트만으로 검증된다.**

### 실기기가 필요한 지점 (여기만)

| 층 | 어디서 | 무엇을 검증 | Gate 0 태스크 |
|---|---|---|---|
| 반복 개발 | Unity Editor Play Mode | 판정 로직, 상태 전이, 결정성 | T3·T4·T5·T6·T7 대부분 |
| 자동 테스트 | Edit/Play Mode `-batchmode` | 경계값, 테스트 경로 전체 | T8 대부분 |
| 화면 레이아웃 | Device Simulator | 16:9 가로, 세이프에어리어 | 상시 |
| 웹 채널 기능 | 데스크톱 Chrome + WebGL 빌드 | WebGL 동작, `AudioContext` | T2 일부 |
| 웹 채널 실환경 | 실제 토스앱 | `.ait` 실행, 가로 고정, 오디오 unlock | T2 |
| **Gate 측정** | **실기기만** | FPS, 탭 지연, cold load, A/V drift | **T2 smoke, T9** |

Part 3의 일정과 대조하면 **실기기를 만지는 날은 Day 2~3과 Day 6~7뿐**이다. Day 0·4·5는 기기 근처에 가지 않는다.

### Android 에뮬레이터를 쓰지 않는 이유

쓸 수 있지만 쓸 이유가 없다.

1. **에디터가 이미 그 일을 한다.** 에뮬레이터를 거치려면 APK를 빌드해야 하는데, 그러면 실기기에 넣는 것과 비용이 같다. 같은 비용에 가짜 환경을 고를 이유가 없다.
2. **리듬게임에는 무의미하다.** 에뮬레이터 오디오는 호스트 macOS를 경유해 지연이 수십~수백 ms 붙고, 터치는 마우스이며, GPU는 에뮬레이션이다. Gate 0이 재려는 탭 지연 60ms·A/V offset 60ms·drift 20ms가 전부 측정 대상이 아니라 에뮬레이터의 인공물이 된다.
3. **아키텍처가 갈라진다.** 릴리스 빌드는 ARM64인데 에뮬레이터는 별도 이미지가 필요해 빌드 설정이 하나 더 생긴다. `BuildManifest` 재현성 계약과 반대 방향이다.
4. **Toss 채널은 애초에 무관하다.** `.ait`는 실제 토스앱이 실행한다.

### React Native는 어디에 있는가

Unity와 React Native는 서로 대체 관계이므로 "Unity가 RN을 지원한다"는 관계가 아니다. 다만 이 프로젝트에서 RN이 실제로 등장하는 자리가 있다.

```text
앱인토스 플랫폼 = React Native(granite) 기반      ← RN은 여기 있다
   └ @apps-in-toss/web-framework = 웹 번들을 WebView로 감싸는 경로
        └ 우리 콘텐츠 = Unity WebGL 출력물         ← RN 코드는 한 줄도 쓰지 않는다
```

`@apps-in-toss/web-framework`의 의존성 트리에 `@granite-js`와 `@react-native-*`가 들어 있는 것으로 확인했다(Ghost Dash 프로젝트 기준). **RN은 우리 코드가 아니라 우리를 담는 껍데기 쪽에 있다.** Ghost Dash가 Phaser 웹게임을 이 경로로 올렸고, 우리는 그 자리에 Unity WebGL을 넣는다.

Unity를 네이티브 앱의 일부 화면으로 심는 Unity as a Library(UaaL) 방식도 존재하지만, 복잡도만 늘고 이 프로젝트에는 쓰지 않는다.

---

## Part 0. 환경 스캔 결과 (2026-08-31 실측)

| 항목 | 상태 | Gate 0 영향 |
|---|---|---|
| Mac M1 Pro / 32GB / macOS 14.4.1 / 여유 661GB | OK | 없음 |
| Unity `6000.4.4f1` | OK | 엔진 확보 |
| Web Build Support (WebGL) 모듈 | OK | Toss 채널 빌드 가능 |
| **Android Build Support 모듈** | **미설치** | **T2의 Android 절반 실행 불가 (P0)** |
| Android SDK / NDK / OpenJDK / `adb` | 미설치 | 실기기 install·launch smoke 불가 (P0) |
| Node (기본 PATH) | v23.7.0 | AIT 툴체인은 LTS 기준 → `.nvmrc`로 20 고정 필요 |
| nvm (default 20.19.4 보유) | OK | 고정만 하면 됨 |
| `ait` CLI | 프로젝트 미설치 | Ghost Arcade에는 로컬 devDep로 존재 |
| `ffmpeg` | 미설치 | T9의 240fps 영상 프레임 판독 도구 없음 |
| Unity MCP | 미등록 | **Gate 0 이후 검토로 결정됨** |
| Unity 프로젝트 | 없음 (문서만, 커밋 1개) | T1이 실제 0번 작업 |
| `.gitignore` | Node/Next.js용 | Unity `Library/` 수 GB 커밋 위험 (P0) |

### 이미 확보된 자산 (재사용 가능)

Ghost Dash(`~/Desktop/projects/ghost-arcade`)에 검증된 Apps in Toss 툴체인이 있다. 이것이 이 플랜의 리스크를 크게 낮춘다.

- `@apps-in-toss/web-framework@3.0.2` + 로컬 `ait` CLI (`node_modules/.bin/ait`)
- `apps-in-toss.config.ts` — `webBundleDir: 'dist'` 로 **임의의 정적 웹 번들**을 `.ait`로 패키징
- `scripts/ait-sandbox-dev.sh` — 샌드박스 연결 절차(LAN + `intoss://<appName>` 스킴, Android는 `adb reverse`)
- `.ait` 24MB 배포 선례 — Unity WebGL 용량 판단의 기준점
- GRAC 자체등급분류 증빙 문서 2건 — Milestone 2에서 재사용
- Supabase / PostHog / 광고 그룹 계정 — Gate 1 이후 재사용

### 보유 기기 및 계정 (사용자 확인)

- Android 실기기: **중급 1대만 보유.** 저·고사양 기기 없음.
- Apps in Toss: 계정 보유. Ghost Dash로 샌드박스 동작 확인됨. **mallang-brain용 앱 등록·환경 구성은 미완.**

---

## Part 1. Gate 0 시작 전에 반드시 고칠 계획 결함 5건

Gate 0 구현 계획은 ENG CLEARED 상태지만, 실제 환경과 대조하면 **지금 그대로는 통과 판정이 불가능한 조항**이 있다. Day 0에 정책을 동결할 때 아래를 같이 처리한다. 정책 동결 뒤 수정하면 전체 행렬을 재실행해야 하므로 순서가 중요하다.

### D1 (P0) — Android 기기 행렬이 보유 현실과 불일치

- 현재 문서: Acceptance Criteria 4번 "Android **3대**와 Apps in Toss의 정해진 workload·loading 행렬을 완료한다."
- 현실: 중급 1대만 보유.
- 조치: `Gate0AcceptancePolicy.required_devices_and_networks`를 **중급 1대 = Gate 판정 기기**로 동결하고, 저·고사양 진단 곡선은 `측정 안 함(기기 미보유)`로 **명시적으로 기록**한다. Acceptance Criteria 4번도 같이 수정한다.
- 근거: 원 문서도 "최소 지원 판단은 중급 기기로 한다"고 이미 정했다. 저·고사양은 처음부터 **진단 지표**였으므로 Gate 판정 자체는 성립한다. 다만 "3대 완료"를 남겨두면 8번(판정 기록) 단계에서 절대 통과할 수 없다.
- 리스크 기록: 저사양 곡선을 모르는 상태로 Gate 1에 진입한다. Week 5 Android 폐쇄 알파에서 저사양 크래시가 처음 드러날 수 있다. 이 리스크는 승인 사항으로 남긴다.

### D2 (P0) — Toss 측정 환경이 실제로 가로 게임을 지원하는지 미확인

- 현재 문서: Gate 조건 = "실제 Toss 앱 **샌드박스**의 cache-cleared Wi-Fi cold playable ≤ 10초".
- 발견: Ghost Dash의 샌드박스 스크립트 주석에 "인앱 광고·**가로 게임은 샌드박스 미지원**"이라고 적혀 있고, 그 프로젝트는 광고 검증을 `ait deploy` → 콘솔 테스트 QR → **실제 토스앱**으로 우회했다.
- 이 주석은 2026-08 Ghost Dash 시점 경험이므로 **현재도 유효한지 단정하지 않는다.** Day 0의 첫 검증 태스크로 확인한다.
- 분기:
  - 샌드박스가 가로를 지원하면 → 문서 그대로 진행.
  - 지원하지 않으면 → Gate 측정 환경을 **`ait deploy` + 콘솔 테스트 배포(실제 토스앱)** 로 교체하고, `cold playable`의 캐시 제어 방법(앱 데이터 삭제 절차)을 정책에 새로 동결한다. 이때 `required_devices_and_networks`에 "Toss 앱 버전"을 반드시 기록한다.
- 이 결정은 Gate 지표의 정의를 바꾸므로 **T2 착수 전에 닫아야 한다.**

### D3 (P0) — Unity WebGL → `.ait` 경로가 문서의 가정과 다름

- 현재 문서: "Apps in Toss **Unity 패키지**와 `.ait` 생성 도구"를 패키징 의존성으로 가정.
- 현실: 사용자에게 검증된 경로는 `@apps-in-toss/web-framework`의 **웹 번들 패키징**(`webBundleDir`)이다. Unity WebGL도 정적 디렉터리(`index.html` + `Build/*.wasm|.data|.js`)를 출력하므로 **Unity WebGL 출력 폴더를 `webBundleDir`로 지정**하는 브리지가 유력한 경로다.
- 조치: T2를 "Unity WebGL 빌드 → 출력 폴더를 `webBundleDir`로 지정 → `ait build` → `.ait` 실행"으로 재정의한다. 별도 Unity 전용 SDK를 전제하지 않는다.
- 하위 리스크 (T2에서 순서대로 제거):
  1. Unity WebGL 압축(Brotli/Gzip)은 `Content-Encoding` 헤더 서빙이 필요하다. **1차 시도는 압축 Disabled**로 통과시키고, 통과 뒤 용량 최적화를 별도 단계로 둔다.
  2. `.ait` 용량 상한 확인. Ghost Dash 24MB 선례가 상한 근처인지 여유인지 모른다.
  3. WebGL 오디오는 사용자 제스처 후에만 `AudioContext`가 열린다. 첫 CTA 탭을 오디오 unlock 지점으로 설계한다.
  4. `SharedArrayBuffer`/threads 미지원 가능성 → WebGL 빌드는 단일 스레드 전제로 시작.

### D4 (P1) — T8의 입력 문서가 이 머신에 없음

- Gate 0 계획 T8이 참조하는 27개 테스트 경로 문서 경로는 `/Users/easyoon/.gstack/projects/mallang-brain/...eng-review-test-plan-20260830-153015.md`다. 이 머신에 존재하지 않는다(현재 홈은 `mbx_n1102002`).
- 조치: T8 착수 전에 **테스트 경로 목록을 재작성**한다. Gate 0 계획서의 `Test Strategy` + `Failure Modes` 표(15행)가 이미 경로의 대부분을 담고 있으므로, 그 두 절을 입력으로 `docs/gate-0-test-plan.md`를 새로 만든다. 이 작업을 **T0.5**로 삽입한다.
- 부수 조치: 문서 내 `/Users/easyoon/...` 절대경로 참조를 상대경로 또는 현재 머신 경로로 정정한다.

### D5 (P1) — 7영업일 타임박스와 작업량 추정치의 불일치

- T1~T10 사람 기준 추정 합계는 약 47시간 + T9의 2일 ≈ **8~9영업일**이다. 7영업일에 들어가지 않는다.
- CC(Claude Code) 기준 추정 합계는 약 20시간이므로, **사람이 감독하고 CC가 구현하는 분담**을 전제할 때만 7일이 성립한다.
- 조치: Part 5의 실행 주체 분담을 계획의 일부로 확정한다. 실기기 측정·240fps 촬영·Toss 콘솔 조작은 사람만 가능하므로 **Day 6~7은 사람의 측정 시간으로 비워둔다.**

---

## Part 2. 개발 환경 구성 (Day 0 오전, 목표 3시간)

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
2. `apps-in-toss.config.ts`를 Ghost Dash 것을 베이스로 작성한다. **핵심 차이는 `webBundleDir`를 Unity WebGL 출력 폴더로 지정하는 것**이다.
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

## Part 3. Gate 0 Day 0~7 실행 플랜

원 문서의 T1~T10을 그대로 쓰되, **T0.5를 삽입**하고 D1~D5 조치를 일자에 배치했다. 각 일자 끝의 `STOP`은 통과하지 못하면 다음 일자로 넘어가지 않는 조건이다.

### Day 0 — 환경 구성 + 정책 동결 (T1, D1~D4)

| 오전 | Part 2의 S1~S7 전체 |
|---|---|
| 오후 | D1·D2·D3 결정 반영, T1 저장소·Unity 프로젝트 초기화 |

오후 상세:
1. **D1 반영** — `Gate0AcceptancePolicy`의 기기 행렬을 중급 1대로 동결. 저·고사양은 `측정 안 함(미보유)` 기록. Gate 0 계획서 Acceptance Criteria 4번 수정.
2. **D2 반영** — S6에서 확인한 Toss 측정 환경을 정책에 동결. 샌드박스/실앱 중 어느 것인지, 캐시 제어 절차가 무엇인지 명시.
3. **D3 반영** — Gate 0 계획서의 "Apps in Toss Unity 패키지" 문구를 실제 툴체인(`web-framework` + `webBundleDir`)으로 정정.
4. **T1** — Unity 6 프로젝트 생성(Assets/Mallang/Gate0 골격), `.asmdef` 3종(공통 런타임 / 플랫폼 어댑터 / 테스트), 기준선 커밋.
5. **D4 준비** — `docs/gate-0-test-plan.md` 재작성(T0.5). Gate 0 계획서의 `Test Strategy` + `Failure Modes` 15행을 입력으로 경로를 열거한다.

**STOP:** Unity 프로젝트가 import·config 오류 없이 열리고, clean baseline 커밋이 있고, `git status`가 오염되지 않았다. 정책 자산 2종(`Gate0Scenario`, `Gate0AcceptancePolicy`)의 **필드 정의가 확정**되었다(값 채우기는 T3).

### Day 1~3 — 양 채널 실제 빌드 (T2) — **최대 리스크 구간**

원 문서가 T2를 가장 앞에 둔 이유는 패키징 실패가 가장 늦게 발견되면 7일을 다 태우기 때문이다. 이 구간에는 기능을 만들지 않는다. **빈 공통 장면만** 두 채널에서 실행시킨다.

**Day 1 — Android 수직 관통**
- `SpikeScene`(빈 장면 + 화면 전체 단색 + 탭 시 색 변경) 하나만 만든다.
- Editor build script로 APK 생성 → `adb install` → 실기기 launch.
- 명령은 GUI가 아니라 **Editor build method**로 확정한다(원 문서 요구). GUI에서만 바꿀 수 있는 숨은 설정을 허용하지 않는다.
- **STOP:** APK가 실기기에서 실행되고 탭에 반응한다.

**Day 2 — Toss WebGL 수직 관통 (D3의 4개 하위 리스크 순서대로)**
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
- **T8** — Edit/Play Mode 배치 실행 + 대상 기기 smoke. 입력은 Day 0에 재작성한 `docs/gate-0-test-plan.md`.
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

## Part 4. Gate 0 이후 마일스톤 개괄

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

## Part 5. 실행 주체 분담

D5에서 확인한 대로, 7영업일은 **CC가 구현하고 사람이 측정·판정할 때만** 성립한다.

| 작업 | 주체 | 근거 |
|---|---|---|
| S1·S3·S4·S5 (모듈·Node·ffmpeg·gitignore) | CC | 명령 실행 + 검증 |
| S2 기기 연결, S6 Toss 콘솔·앱 등록 | **사람** | 물리 연결, 계정 권한 |
| D1~D4 정책 결정 | **사람이 결정**, CC가 문서·자산 반영 | 판정선 변경은 제품 판단 |
| T1·T3·T4·T5·T6·T7 구현 | CC | 코드·테스트 |
| T0.5 테스트 계획 재작성 | CC | 기존 문서 2절을 입력으로 |
| T2 빌드 스크립트 | CC | Editor build method |
| T2 실기기 install·launch 확인 | **사람** | 물리 기기·Toss 앱 |
| T8 배치 테스트 실행 | CC | `-batchmode` 실행 |
| T9 측정 행렬 (9칸 × 5회 × 2플랫폼) | **사람** | 실기기 조작, 발열·순서 통제 |
| T9 외부 240fps 촬영·판독 | **사람** (판독은 CC가 ffmpeg 보조) | 촬영 필요 |
| T10 판정 | **사람이 결정**, CC가 보고서 작성 | Gate 판정은 제품 판단 |
| 메시지 테스트 10명 | **사람** | 인터뷰 |

**사람의 시간이 병목인 구간은 Day 2(Toss 실기기), Day 6~7(측정)이다.** 이 3일은 다른 일정을 넣지 않는다.

---

## Part 6. 이 플랜의 미확인 가정 (Day 0에 검증)

계획을 단정으로 쓰지 않기 위해 분리한다. 각 항목은 Day 0 안에 사실로 바꾸거나 리스크로 기록한다.

1. **Toss 샌드박스의 가로 게임 지원 여부** (D2). Ghost Dash 시점 주석이 근거이고, 현재도 유효한지 확인하지 않았다.
2. **Unity WebGL 출력이 `webBundleDir`로 그대로 패키징되는지** (D3). 논리적으로 유력하나 실행으로 확인하지 않았다.
3. **`.ait` 용량 상한**. 24MB 선례만 있고 상한을 모른다.
4. **Unity Hub CLI `install-modules`의 실제 동작**. 문법은 이 머신의 `help`로 확인했으나 실행은 하지 않았다.
5. **Gate 기기의 모델·OS·주사율**. S2에서 확정한다. 정책 동결의 필수 입력이다.
6. **240fps 촬영 기기 보유 여부**. 없으면 외부 측정 프로토콜이 성립하지 않고, Gate 지표 중 "내부·외부 p95 차이 ≤16.7ms"를 판정할 수 없다 → 이 경우 해당 지표를 `측정 불가`로 기록해야 하며, 원 문서 기준으로는 **완전한 pass가 불가능**하다. Day 0에 반드시 확인한다.

---

## 용어 한 줄 정리

- **Gate 0 (기술 게이트):** 재미나 콘텐츠를 만들기 전에, 두 플랫폼이 리듬게임에 필요한 반응성·오디오·프레임을 낼 수 있는지만 판정하는 최대 7일 실험.
- **`.ait` (앱인토스 번들):** 정적 웹 번들을 앱인토스가 실행할 수 있는 형태로 감싼 패키지 파일. `ait build`가 만든다.
- **DSP 시계(`AudioSettings.dspTime`):** 오디오 하드웨어가 세는 시간. 화면 프레임과 별개로 흘러서, 리듬 판정의 기준점으로 쓴다.
- **clock 보정(ClockCalibration):** 입력이 찍힌 시간과 소리가 나는 시간이 서로 다른 자를 쓰기 때문에, 두 자의 눈금 차이(offset)를 주기적으로 재는 일.
- **Play Mode:** 빌드 없이 Unity Editor 안에서 게임을 바로 돌리는 모드. Unity 반복 개발의 본진이다.
- **AOT 컴파일** (ahead-of-time, 미리 번역): 실행 전에 기계어로 번역해두는 방식. Unity Android가 이걸 쓰기 때문에 Expo식 핫 리로드가 불가능하다.
- **granite:** 토스의 React Native 프레임워크. 앱인토스 플랫폼의 토대이고, 우리는 그 위 WebView 칸만 쓴다.
- **`RunRecord` / `BuildManifest` (불변 기록):** "이 빌드로, 이 조건에서, 이 결과가 나왔다"를 나중에 못 고치게 박아두는 파일. 측정 결과를 신뢰할 수 있게 만드는 장치.
