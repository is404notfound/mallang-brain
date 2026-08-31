# TODOS After Gate 0

## 먼저 읽는 쉬운 설명

이 문서는 **Gate 0이라는 첫 기술 시험을 통과한 뒤** 할 일을 적어 둔 목록이다.

- Gate 1에서는 게임의 가장 중요한 부분인 리듬 판정, 점수, 콤보, 길게 누르기, 탈락 규칙을 제대로 만든다.
- Apps in Toss에서는 로그인·결제·저장처럼 실제 서비스에 필요한 기능을 연결한다.
- 나중에는 Android와 Apps in Toss용 파일을 자동으로 만들고 배포하는 과정도 준비한다.

`P1`은 먼저 해야 하는 일이고, `P2`는 그다음 일이다. `Depends on`은 **이 일을 시작하기 전에 끝나야 하는 조건**이다. 아래의 원래 계획은 이 뜻을 더 자세히 적어 둔 것이다.

## Gate 1

### 리듬 코어 구현 계획 작성

**What:** Gate 0 통과 후 판정 윈도우, 점수, 콤보, 홀드, 탈락 규칙을 프로덕션 규칙으로 설계하고 구현 순서를 정한다.

**Why:** Gate 0의 `±60ms/±120ms` 판정 프로브는 플랫폼 간 시간 일치 여부만 확인하므로 실제 게임 규칙으로 사용할 수 없다.

**Context:** [Gate 0 구현 계획](docs/02-gate-0/implementation-plan.md)의 원시 타이밍 프로브와 측정 기록을 입력으로 사용한다. 기술 실험 코드를 그대로 확장하지 말고, [제품 비전과 6주 로드맵](docs/01-product/product-vision-and-roadmap.md)의 코어 규칙 기본값과 최소 수용 테스트를 별도 Gate 1 계획으로 옮긴다.

**Effort:** M  
**Priority:** P1  
**Depends on:** Android와 Apps in Toss Gate 0 통과 및 최종 `RunRecord` 확정

### Apps in Toss 런타임 API 통합

**What:** 로그인, 결제, 브리지, 저장 기능 중 제품에 필요한 Toss 런타임 API를 선정하고 샌드박스부터 통합한다.

**Why:** Apps in Toss가 핵심 수익 채널이지만 Gate 0은 `.ait` 패키징과 실행만 확인하고 실제 수익 기능은 연결하지 않는다.

**Context:** Gate 0의 Apps in Toss 의존성은 패키징 도구에 한정한다. Gate 1 시작 시 상품과 수익 모델을 먼저 확정하고, 공통 스파이크 코드에 SDK가 퍼지지 않도록 WebGL 전용 경계에서 필요한 API만 연결한다.

**Effort:** L  
**Priority:** P1  
**Depends on:** Apps in Toss Gate 0 통과, 결제 상품과 수익 모델 정의

## Infrastructure

### Android·Apps in Toss 빌드 및 배포 자동화

**What:** Android APK/AAB와 Toss `.ait`를 자동 생성하고 테스트 결과, 체크섬, 빌드 아티팩트를 보관하는 CI 파이프라인을 구축한다.

**Why:** 두 필수 채널을 반복 출시할 때 수동 빌드 누락과 설정 차이를 줄인다.

**Context:** Gate 0에서는 재현 가능한 로컬 빌드와 불변 `BuildManifest`까지만 만든다. 프로젝트 구조가 안정된 뒤 같은 명령을 CI로 옮기고 서명 키와 배포 계정을 비밀 저장소에 연결한다.

**Effort:** L  
**Priority:** P2  
**Depends on:** 양쪽 로컬 빌드 통과, Unity 프로젝트 구조 안정화, 서명·배포 계정 준비

## Completed

_아직 완료된 항목이 없습니다._
