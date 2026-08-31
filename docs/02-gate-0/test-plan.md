# Gate 0 기술 검증: 테스트 계획

이 문서는 Gate 0의 T8에서 실행할 테스트 목록이다. 기준 문서는 [Gate 0 구현 계획](implementation-plan.md)이다. 모든 결과는 해당 `BuildManifest`, `RunRecord`, 원자료 checksum과 함께 남긴다.

## 실행 순서

1. Edit Mode 테스트를 통과한다.
2. Play Mode 테스트를 통과한다.
3. Android와 Apps in Toss에서 build/launch smoke를 통과한다.
4. 두 플랫폼의 3분 측정과 외부 A/V 표본을 대조한다.

앞 단계가 실패하면 다음 단계로 넘어가지 않는다. 실패·무효 원자료는 삭제하지 않는다.

## Edit Mode: 15개 경로

| ID | 확인할 일 | 입력 또는 실패 상황 | 통과 기준 |
| --- | --- | --- | --- |
| E01 | Scenario 로드 | 파일 누락 | 시작을 막고 파일명을 포함한 오류를 낸다. |
| E02 | Policy 로드 | schema 비호환 | 시작을 막고 schema 오류를 낸다. |
| E03 | hash 검증 | Scenario 또는 Policy hash 변경 | 기존 실행 기록을 거부한다. |
| E04 | phase 순서 | 같은 seed를 두 번 사용 | 같은 phase와 입력 일정이 나온다. |
| E05 | clock 보정 | 정상 표본 8회 | median offset이 정해진 방식으로 계산된다. |
| E06 | clock 역행 | 시간이 뒤로 감 | 해당 segment를 무효화하고 false pass를 막는다. |
| E07 | clock jump | 시간이 갑자기 크게 변함 | 해당 segment를 무효화한다. |
| E08 | 보정 오차 | 오차가 5ms 초과 | 해당 segment를 무효화한다. |
| E09 | 판정 경계 | -121, -120, -60, 0, +60, +120, +121ms | ±60ms는 Perfect, 그 밖의 ±120ms는 Good, 창 밖은 Miss가 된다. |
| E10 | 원시 판정 일치 | 고정 timestamp 배열 | Android와 WebGL에서 같은 band 배열이 나온다. |
| E11 | 입력 중복 | 같은 손의 중복 입력 | 첫 유효 입력만 사용하고 나머지는 중복 VFX나 표본을 만들지 않는다. |
| E12 | 집계 | 중단·무효 segment 포함 | 무효 segment는 결과에서 빼고 원자료는 보존한다. |
| E13 | BuildManifest | dirty source 또는 checksum 누락 | 최종 Gate pass 후보로 인정하지 않는다. |
| E14 | RunRecord | 필수 필드 또는 checksum 누락 | 비교 가능한 Gate 결과로 인정하지 않는다. |
| E15 | lifecycle 전이표 | 정의되지 않은 상태 전이 | 전이표 밖의 전이를 거부하거나 Error 상태로 기록한다. |

## Play Mode: 7개 경로

| ID | 확인할 일 | 입력 또는 실패 상황 | 통과 기준 |
| --- | --- | --- | --- |
| P01 | 장면 시작 | 정상 부팅 | `Boot → Idle → CountIn → Playing` 흐름으로 시작한다. |
| P02 | Count-in 입력 | 네 박자 카운트 중 탭 | 게임 입력으로 처리하지 않는다. |
| P03 | 멀티터치 | 좌·우 동시 입력 | 각 입력을 독립적으로 처리한다. |
| P04 | touch cancellation | 누르는 중 OS가 touch를 취소 | 해당 입력을 안전하게 닫고 중복 효과를 만들지 않는다. |
| P05 | 즉시 VFX | 유효 입력 | 판정과 같은 실행 흐름에서 VFX가 나온다. |
| P06 | 공통 중단 | Playing 또는 hold 중 중단 | 공통 interrupt 상태로 들어가고 재개 전 네 박자를 다시 센다. |
| P07 | 오류와 retry | 오디오 재초기화 실패 | Error를 유지하고 retry UI로 복구할 수 있다. |

## 대상 기기 E2E: 5개 경로

| ID | 확인할 일 | 통과 기준 |
| --- | --- | --- |
| D01 | Android build/launch smoke | APK/AAB가 BuildReport JSON과 유효한 BuildManifest를 만들고 실기기에서 실행된다. |
| D02 | Toss package/launch smoke | Unity WebGL 출력 폴더를 `webBundleDir`로 지정해 `ait build`가 `.ait`를 만들고 Toss 샌드박스 또는 확정된 테스트 경로에서 실행된다. |
| D03 | Android 3분 측정 | 9개 workload에서 워밍업 1회 뒤 유효 측정 5회를 기록한다. |
| D04 | Toss 3분 측정 | cache 상태와 네트워크를 기록하고 Android와 같은 시나리오·정책 hash로 측정한다. |
| D05 | 외부 A/V 대조 | 240fps 영상·오디오 표본과 내부 기록의 p95 차이가 16.7ms 이하이다. |

## 기기별 lifecycle 확인

| 플랫폼 | 실패 상황 | 통과 기준 |
| --- | --- | --- |
| Android | pause/focus callback 중복·역순 | 한 번의 중단으로 합치고 한 번만 네 박자를 다시 센다. |
| WebGL | hidden tab | 공통 interrupt로 처리한다. |
| WebGL | AudioContext만 suspend | 오디오를 다시 준비한 뒤 네 박자 재개 또는 retry를 보인다. |
| 양쪽 | 재개 중 다시 중단 | 이전 재개를 취소하고 안전하게 interrupt 상태로 돌아간다. |

## 기록 체크리스트

- [ ] 사용한 Scenario와 Policy의 ID, schema, SHA-256을 남겼다.
- [ ] 기기·OS·주사율·DPR·네트워크·cold/warm/cache 상태를 남겼다.
- [ ] 무효·실패 결과도 사유와 함께 남겼다.
- [ ] BuildManifest와 RunRecord를 덮어쓰지 않았다.
- [ ] 외부 측정 파일 경로와 원자료 checksum을 남겼다.

## 실패 처리

- Android 또는 Toss 중 한 곳이 실패하면, 2일 안에 한 번만 고친 뒤 두 플랫폼의 전체 행렬을 다시 실행한다.
- 판정 정책을 바꾸면 이전 결과를 폐기하고 전체를 다시 실행한다.
- 다시 실패하면 Gate 1을 시작하지 않고, 엔진·패키징·전달 방식을 다시 결정한다.
