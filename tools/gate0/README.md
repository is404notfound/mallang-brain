# Gate 0 T2: 공통 장면 실행 시험

이 도구는 **탭하면 화면색이 바뀌고 시험음이 나는 Unity 장면**을 Android APK와 Toss `.ait`로 만든다. 콘테스트, 리듬 판정, Gate 0 전체 통과 검증은 아직 범위 밖이다.

## 실행

저장소 루트에서 Node 24와 Unity 6000.3.23f1을 사용한다. 이 Mac에서는 `PATH=/opt/homebrew/opt/node@24/bin:$PATH`로 Node 24를 선택할 수 있다. 다른 머신은 `.nvmrc`를 따른다. Unity Editor에 같은 프로젝트가 열려 있으면 먼저 저장하고 닫는다. 명령은 동시에 실행하지 않는다.

```sh
npm ci
npm run gate0:prepare
npm run gate0:test-edit
npm run gate0:test-play
npm run gate0:build-android
npm run gate0:build-webgl
```

각 명령은 `Artifacts/T2/<UTC 시각>-<작업>/`에 로그와 `SmokeRecord.json`을 남긴다. 빌드는 APK 또는 `WebGL/` 디렉터리와 `BuildReport.json`을 만든다. 환경이 다르면 `UNITY_EDITOR`로 Unity 실행 파일의 절대 경로를 지정한다. 이전 `unity/Builds/Smoke/` 산출물은 변경하지 않는다.

성공한 WebGL 빌드의 경로를 그대로 지정해 Toss로 패키징한다.

```sh
npm run gate0:package-toss -- Artifacts/T2/<빌드 ID>/WebGL
```

콘솔 앱 아이디는 `mallang-brain`으로 고정되어 있다. 패키징 성공은 앱 등록이나 토스 실행 성공을 뜻하지 않는다.

`.ait`와 가로 화면 브리지가 추가된 WebGL 복사본은 새 작업 폴더에 보존된다. CLI가 생성한 루트 `.ait`도 남지만, 시험에는 작업 폴더의 파일을 사용한다. 이 명령은 업로드·배포하지 않는다. 원래 WebGL 산출물을 수정하지 않아 일반 브라우저 진단과 Toss 패키징을 구분할 수 있다.

앱인토스 콘솔의 연동 키는 저장소나 `.env`에 넣지 않는다. 로컬 CLI의 `mallang-brain` 프로필에 한 번 등록한 뒤, 패키징 작업 폴더의 번들을 명시해서 업로드한다. 경로를 필수 인자로 둔 것은 루트에 남은 이전 번들을 실수로 업로드하지 않기 위해서다.

```sh
npm exec -- ait token add mallang-brain
npm run gate0:deploy-toss -- Artifacts/T2/<패키징 ID>/mallang-brain.ait -m "Gate 0 기기 테스트"
```

업로드가 성공하면 출력된 테스트 스킴 또는 콘솔 QR을 사용한다. 정식 출시 전에는 `intoss://mallang-brain`이 아니라 배포 때 발급된 `intoss-private://...?_deploymentId=...` 스킴으로 시험한다.

## 실제 기기 확인

이 컴퓨터에서 완성된 빌드를 미리 보려면 아래 명령을 켜 둔 다음 해당 주소를 연다. `unity/Assets/WebGLTemplates/`의 HTML을 직접 열면 실행되지 않는다.

```sh
python3 -m http.server 8765 --bind 127.0.0.1 --directory Artifacts/T2
```

현재 산출물 미리보기: <http://127.0.0.1:8765/2026-09-09T21-43-05-039Z-package-toss/WebGL/>. 일반 브라우저이므로 토스 가로 설정 미확인 문구가 표시된다. 좌우 클릭으로 시험할 수 있으며 실제 토스 검증을 대신하지 않는다.

1. Android에 새 APK를 설치하고 실행한다.
2. 화면이 가로인지 확인한다. 좌우를 각각 눌러 해당 탭 횟수와 배경색이 바뀌는지 확인한다.
3. 첫 탭과 이후 탭에서 시험음이 들리는지 사람이 확인한다. `Tone requested`는 실제 스피커 출력을 확인했다는 뜻이 아니다.
4. 실제 Toss 샌드박스/테스트 QR로 해당 `.ait`를 실행하고 같은 항목을 확인한다. 상단 가로 요청 성공 문구 외에 실제 화면 방향도 확인한다.
5. 같은 빌드의 기기 모델, OS, Toss 버전, 네트워크, 결과와 화면 증거를 기록한다.

Toss API 미지원/시간 초과 시 상단에 미확인을 표시하고 다시 탭하면 재시도한다. Unity 로딩은 독립적으로 계속된다. 가로 설정이 실패한 빌드를 Toss smoke 통과로 판정하지 않는다.

## 후속 검증과 최신 시험 빌드

### T3 조건·정책 초안

```sh
node tools/gate0/run.mjs prepare-contracts
node tools/gate0/run.mjs test-edit
node tools/gate0/run.mjs validate-contracts
```

`Settings/Gate0Scenario.asset`과 `Gate0AcceptancePolicy.asset`을 생성한다. 기존 자산은 덮어쓰지 않는다. 시나리오는 30/60/100명×3조건, 각 180초와 결정적 입력 배열을 담으며 실제 부하 재생 구현은 T6이다. 정책은 기존 통과선과 외부 표본 수를 유지한다. 실측 기기 목록은 비워 둔 초안이며, 목록 작성·정책 동결 전 `validate-contracts`는 실패하는 것이 정상이다. 예제나 가상 기기를 실제 기기로 저장하지 않는다.

명령 기록은 기존 `Artifacts/T2` 작업 디렉터리를 재사용하되 `SmokeRecord.scope`에서 T3 구성 검증과 기기 측정을 구분한다. 이 파일명·위치는 T7 최종 기록 형식을 대신하지 않는다.

### 2026-09-11 상단 수정 시험

Toss 내비게이션은 투명 배경, 뒤로가기·홈·제목 숨김으로 설정했다. 더보기·닫기는 호스트가 유지한다. 자체 시험 안내는 하단의 **시험 정보**에서 열 수 있다. URL에 `?diagnostics=1`을 붙이면 처음부터 펼친다. 안내 컨트롤을 눌러도 게임 탭으로 처리하지 않는다.

새 토스 시험 링크:

```text
intoss-private://mallang-brain?_deploymentId=01a08dce-c8bb-7e44-895d-35cbc2ce6147&host=appsInTossHost
```

새 번들: `Artifacts/T2/2026-09-11T00-12-13-563Z-package-toss/mallang-brain.ait`. 테스트 업로드 완료이며 정식 출시는 하지 않았다. 실제 Toss에서 투명 메뉴·닫기·좌우 입력·소리를 재확인해야 한다.

## 기록의 한계

- `SmokeRecord.json`은 진단용이다. T7의 최종 `BuildManifest`나 `RunRecord`를 대체하지 않는다.
- `gateEligible`은 항상 `false`다. 시나리오·정책·외부 측정이 없는 빌드를 통과로 표시하지 않는다.
- 이 장면의 중단 처리에는 예약 노트나 리듬 판정이 없다. T5의 전체 lifecycle과 네 박자 재개 구현을 완료한 것으로 해석하지 않는다.
- 기존 Gate 0의 기간·기기 행렬·측정 횟수·통과선은 변경하지 않는다.

## 에셋 출처

화면은 단색과 Unity 기본 GUI만 사용한다. 시험음은 `SmokeTapScene.CreateTestTone`에서 사인파와 감쇠 곡선으로 생성하며 외부 음원을 포함하지 않는다. 최종 마림바 음색·음악·캐릭터 아트가 아니다.

## 참고

- [Toss 화면 방향 API](https://developers-apps-in-toss.toss.im/bedrock/reference/framework/%ED%99%94%EB%A9%B4%20%EC%A0%9C%EC%96%B4/screen-properties.html)
- 패키징 설정은 설치된 `@apps-in-toss/web-framework@3.1.1`의 config 타입과 CLI를 기준으로 작성했다.
- [Unity AudioClip.Create](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/AudioClip.Create.html)
