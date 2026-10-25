# Game Planning 재시작 전 보관 기록

보관일: 2026-10-25  
보관 브랜치: `archive/pre-game-planning-2026-10-25`  
기준 커밋: `ab29e12`

## 보관 목적

이 브랜치는 `game-planning`으로 제품 기획을 다시 시작하기 전에 기존 Gate 1 실험을 보존한다. 여기의 코드와 문서는 새 제품 방향의 기준이 아니다. 새 기획에서 다시 채택하기 전까지 참고 자료로만 사용한다.

## 포함한 작업

- 3스테이지·20명 미니 콘테스트와 로컬 실행 기록
- 듣고 따라 치는 튜토리얼, 10개 패턴, 세트 콤보와 뇌 크기 변화
- 튜토리얼 장면, 그림, 소리와 관련 EditMode·PlayMode 시험 코드
- 당시 제품 범위 변경안, 구현·시험 계획, 화면·소리 반박 리뷰
- Gate 1 WebGL 및 Android 빌드 진입점

## 정리한 항목

- 현재 장면보다 오래된 `Assets/_Recovery` 복구 장면
- 프로젝트 실행에 필요하지 않은 Unity MCP 패키지와 자동 시작 코드
- 실수로 켜진 Unity Services 연결 설정
- Unity Library, 로컬 빌드, 로그, `node_modules`, `.ait` 같은 다시 만들 수 있는 로컬 생성물
- 비어 있는 로컬 폴더

과거 Gate 측정 자료가 들어 있는 `Artifacts`는 이번 정리에서 삭제하지 않았다. 실제 장비 측정과 자동 시험을 섞지 않으며, 기존 Gate 기준도 완화하지 않는다.

## 검증 상태

- `git diff --check`: 실행 대상
- Unity EditMode 시험: 2026-10-25에 실행을 시도했으나 Unity Licensing Client 재연결 실패로 시험 시작 전에 중단
- Unity PlayMode 시험: EditMode 시험이 시작되지 않아 실행하지 않음
- 실제 Android·Apps in Toss 장비 측정: 이번 보관 작업에서 진행하지 않음

따라서 이 브랜치를 Gate 통과나 출시 가능 상태로 해석하지 않는다. 새 기획은 깨끗한 `main`에서 만든 별도 브랜치에서 시작한다.
