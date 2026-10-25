# Gate 1 미니 브레인 콘테스트 실행 계획

작성일: 2026-09-24  
상태: 코드 구현 완료, Unity 자동 시험·빌드·실기 확인 대기  
대상: 현재 `main` 브랜치의 Gate 1 프로토타입

## 1. 한 줄 결정

현재의 `듣고 따라하기` 자동 반복 화면을 Gate 1의 대표 빌드로 쓰지 않는다. 기존 `RhythmCore`를 그대로 중심에 두고, **3스테이지·20명 미니 브레인 콘테스트**를 만든다.

이 결정은 Gate 1의 작은 재미 실험 범위를 바꾸는 것이다. 장기 제품도 반드시 20명으로 고정한다는 뜻은 아니다. 기존 문서의 100명 약속은 현재 제품 설명에서 내리고, Gate 1 결과가 나온 뒤 20명 유지·확대·축소를 다시 결정한다.

- 참가자는 총 20명이다.
- 구성은 `플레이어 1명 + 연습생 봇 기록 19명`이다.
- 연습생은 실제 사용자처럼 꾸미지 않는다.
- 결과에는 `20명 중 N위`를 보여준다.
- 실제 사용자 백분위, 서버 순위, 학점은 아직 만들지 않는다.
- 한 판은 약 45~60초다.
- 실패 또는 완주 뒤에는 사용자가 직접 `다시 참가`를 눌러야 한다.

이 단계의 질문은 하나다.

> 마림바를 치고, 연습생이 탈락하고, 내 순위를 확인하는 짧은 콘테스트가 “한 판 더”를 만드는가?

## 2. 왜 20명인가

### 선택: 20명

20명은 지금 실험에 가장 알맞다.

- 1등부터 20등까지 한 순위가 정확히 5% 간격이라 이해하기 쉽다.
- 19개의 연습생 슬롯은 가로 화면에서 `10칸 × 2줄`로 읽을 수 있다.
- 스테이지가 끝날 때 `20 → 12 → 6 → 3`으로 줄어들면 군중이 사라지는 변화가 눈에 잘 보인다.
- 100명보다 화면과 연출이 단순하지만, 8명이나 12명보다 “합주 경쟁” 느낌이 강하다.
- 나중에 수치를 바꾸더라도 참가자 수를 한 설정값으로 두면 16명과 쉽게 비교할 수 있다.

여기서 20명은 **한 판의 화면상 참가자 수**다. 제품 문서에 있는 “실제 사용자 기록 20명 이상일 때만 실제 백분위를 표시한다”는 기준과는 다른 숫자다. 이번 빌드에는 실제 백분위를 표시하지 않는다.

### 대안 1: 16명

장점은 `4 × 4` 배치와 `16 → 8 → 4` 구조가 깔끔하다는 점이다. 단점은 화면에서 작은 토너먼트처럼 보이고, 원래 제품의 “우글우글 합주”가 약해진다는 점이다.

판정: 첫 화면에서 20명이 너무 작거나 복잡하다는 사용성 문제가 확인될 때만 16명으로 내린다. 미리 줄이지 않는다.

### 대안 2: 24명 또는 25명

장점은 군중이 조금 더 풍성해 보인다는 점이다. 단점은 작은 캐릭터가 늘고 탈락 연출 시간이 길어지며, 20명보다 더 많은 재미 정보를 주지 않는다는 점이다.

판정: 이번 Gate 1에서는 쓰지 않는다.

## 3. 플레이 규칙

### 한 판의 구조

```text
조율 확인
  ↓
연습생 기록 19명 안내
  ↓
Stage 1: 기본 좌우 탭, 80 BPM, 통과선 50%
  ↓ 연습생 8명 탈락, 생존 시 12명 남음
Stage 2: 좌우 교대, 90 BPM, 통과선 60%
  ↓ 연습생 6명 탈락, 생존 시 6명 남음
Stage 3: 탭 + 홀드, 100 BPM, 통과선 70%
  ↓ 연습생 3명 탈락, 완주 시 3명 결과 경쟁
결과: 20명 중 N위 + 정확도 + 점수
  ↓
다시 참가 / 종료
```

각 스테이지 앞에는 네 박자 카운트를 둔다. 스테이지 종료 연출은 약 1초로 제한한다. 플레이어가 실패하면 다음 스테이지로 가지 않고 바로 결과로 간다.

### 탈락 규칙

플레이어의 생존은 지금 검증된 `ContestRun`의 스테이지 정확도 통과선으로 정한다. 순위가 낮다는 이유로 별도 탈락시키지 않는다. 이 규칙은 경쟁자 수가 바뀌어도 플레이 난이도가 바뀌지 않게 한다.

연습생 기록 19개의 분포는 다음처럼 고정한다.

| 결과           |  수 | 플레이어가 생존했을 때 남은 전체 인원 |
| -------------- | --: | ------------------------------------: |
| Stage 1 실패   |   8 |                                    12 |
| Stage 2 실패   |   6 |                                     6 |
| Stage 3 실패   |   3 |                                     3 |
| 3스테이지 완주 |   2 |        결과에서 플레이어와 1~3위 경쟁 |

### 순위 규칙

현재 `RunSnapshot`에 실제로 있는 값만 쓴다. 비교 순서는 다음과 같다.

```text
완주 여부
→ 마지막으로 통과한 스테이지
→ 전체 정확도
→ rank score
```

성능 값이 완전히 같으면 같은 순위로 표시한다. 내부 화면 배치만 안정적으로 만들기 위해 참가자 ID를 마지막 정렬값으로 쓰되, ID로 표시 순위를 갈라놓지 않는다.

## 4. 연습생 봇 기록 계약

연습생은 실시간으로 연주하는 AI가 아니다. 같은 차트와 규칙으로 미리 만든 **합성 기록**이다.

각 기록은 다음 값을 가진다.

```text
participant_id
display_name          // 연습생 01 ... 연습생 19
source                // trainee
chart_id              // gate1-mini-contest-v1
ruleset_version
rank_ruleset           // mini-contest-rank-v1
roster_version
end_reason
last_completed_stage
fail_stage
accuracy_basis_points
rank_score
```

필수 원칙:

- 화면에 `연습생 기록 19명 포함`을 항상 보인다.
- 사람 이름, 사람 사진, 접속 중 표시를 만들지 않는다.
- `실시간 대전`, `현재 접속자`, `실제 상위 %`라고 쓰지 않는다.
- 같은 빌드에서 재도전하면 같은 기록과 겨룬다. 재도전마다 약한 상대를 다시 뽑는 방식은 금지한다.
- 연습생 기록은 공식 사용자 수, 실제 백분위, Gate 1 테스트 참가자 수에 포함하지 않는다.
- 연습생 자료가 잘못돼도 리듬 플레이는 계속된다. 이때는 순위를 숨기고 `연습 기록 오류 · 순위 제외`를 보여준다.
- 실패 기록은 `last_completed_stage = fail_stage - 1`이어야 한다. 완주 기록은 `last_completed_stage=3`, `fail_stage=4`여야 한다.
- 정확도는 0~10000, 점수는 0 이상이어야 하며 chart·ruleset·roster 버전이 모두 맞아야 한다.

첫 버전은 코드 안에 고정된 19개 기록을 둔다. 네트워크, 파일 다운로드, 데이터베이스는 쓰지 않는다. 실제 사용자 기록으로 교체하는 작업은 Gate 1 통과 뒤에만 검토한다.

`mini-contest-rank-v1`은 프로토타입 전용이다. 제품 문서의 장기 공식 rank tuple을 구현한 것으로 취급하거나 기존 공식 기록과 섞지 않는다.

## 5. 구조와 데이터 흐름

```text
Unity Input System의 원본 이벤트 시각
  ↓  Gate 0에서 검증한 clock → DSP 변환 재사용
ContestPrototypeScreen
  ↓  정수 마이크로초 입력$
ContestRun
  ├─ 판정 이벤트 → 마림바 소리·건반 반응·판정 글자
  ├─ 스테이지 결과 → 연습생 탈락 연출·남은 인원
  └─ 최종 RunSnapshot
          ↓
MiniContestRoster의 고정 기록 19개와 비교
          ↓
결과 화면: 20명 중 N위·정확도·점수·다시 참가
```

화면 상태는 한 곳에서만 바꾼다.

```text
Calibrating → Ready → CountIn → Playing
                              ↓
                    StageResult ──→ CountIn (다음 스테이지)
                              ↓
                            Result → Retry
```

`StageResult`는 플레이 판정을 멈춘 뒤에만 시작한다. 탈락 애니메이션이 DSP 시계나 `ContestRun` 시간을 멈추게 해서는 안 된다.

## 6. 이미 있는 코드와 재사용 방법

| 기존 항목                              | 결정                                                                                                          |
| -------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| `ContestRun`                           | 판정, 점수, 콤보, 홀드, 스테이지 통과를 그대로 재사용한다.                                                    |
| `Gate1ChartFactory`                    | 12개 패턴과 컴파일 규칙을 재사용해 3스테이지 차트 생성 함수를 추가한다. 기존 20스테이지 차트는 바꾸지 않는다. |
| `ClockCalibrator`, `SystemRhythmClock` | 입력 시각을 DSP 시각으로 바꾸는 데 재사용한다.                                                                |
| Gate 0의 `InputSystem.onEvent` 처리    | `eventPtr.time`을 직접 읽는 방식을 옮겨 쓴다.                                                                 |
| 이전 콘테스트 화면 `4175e34`           | 화면 흐름과 리듬 레인 참고용으로만 쓴다. 파일 전체를 되돌려 덮어쓰지 않는다.                                  |
| 현재 듣고 따라하기 `ab29e12`           | Git 기록에 보존한다. Gate 1 대표 빌드와 지표에서는 제외한다.                                                  |
| `PatternCompiler`와 듣고 따라하기 패턴 | 삭제하지 않지만 이번 콘테스트에서 호출하지 않는다.                                                            |

확인된 수정점:

- `ContestRun`의 완주 `fail_stage`는 현재 20스테이지에 맞춘 `21` 고정값이다. 차트 길이가 3이면 `4`가 되도록 `chart.Stages.Count + 1`로 고친다. 기존 20스테이지 결과는 그대로 21이다.
- 현재 화면은 프레임에서 입력을 읽고 `InputState.currentTime`을 사용한다. 새 화면은 Gate 0처럼 입력 이벤트 자체의 시각을 쓴다.
- 터치 취소는 `ProcessCancel`로 전달한다.
- 탭을 뗄 때 생기는 `UnmatchedRelease`가 직전 Perfect/Good 표시를 지우지 않게 한다.
- 콘테스트는 `PatternCompiler`의 기본 통과선 0을 쓰지 않는다. 아무 입력이 없으면 Stage 1에서 반드시 실패해야 한다.

## 7. 화면, Game Feel, 오디오의 최소 범위

### 화면

- 16:9 가로 화면을 기준으로 한다.
- `Screen.safeArea` 안에 점수, 남은 인원, 재도전 버튼을 둔다.
- 군중은 넓은 화면에서 10칸 × 2줄, 좁은 가로 화면에서 5칸 × 4줄로 바꾼다.
- 플레이어는 테두리, 크기, `나` 글자로 구분한다. 색만으로 구분하지 않는다.
- 남은 인원 숫자는 캐릭터 연출이 실패해도 항상 맞아야 한다.
- 이번 프로토타입은 현재의 즉시 모드 GUI를 유지한다. uGUI 또는 UI Toolkit 전환은 하지 않는다.

### Game Feel

세 종류만 만든다.

1. **일반 타격:** 건반 8% 눌림, 90ms 안에 원위치, 판정 글자 짧은 팝, 한 번의 마림바 소리.
2. **스테이지 통과:** 결과 확정 뒤 0.6~1.0초 동안 연습생이 순서대로 작아지며 사라지고, 남은 인원 숫자가 함께 줄어든다.
3. **실패:** 플레이어 아이콘이 짧게 찌그러지고 내려앉는다. 1초 안에 결과와 재도전 버튼을 보여준다.

일반 타격에는 화면 흔들기와 시간 정지를 쓰지 않는다. 강한 효과가 리듬 가독성을 가리면 실패다. 모든 움직임은 원래 크기와 위치로 돌아와야 한다.

### 오디오

- 카운트와 반주 큐는 `AudioSettings.dspTime`과 `PlayScheduled`로 예약한다. 사용자가 누른 마림바 소리는 입력 승인 직후 재생한다.
- 연습생 19명의 소리는 재생하지 않는다. 플레이어 마림바, 카운트, 탈락 효과만 들린다.
- 왼손과 오른손은 음높이와 좌우 위치가 구분되게 한다.
- 현재의 단순 전자음은 외부 테스트 전에 따뜻한 마림바에 가까운 짧은 음색으로 교체한다. 권리가 확인된 자산이 없으면 기존 코드의 합성 마림바를 개선해 사용한다.
- 적응형 음악, 사이드체인, 복잡한 AudioMixer, 설정 화면은 이번 범위에서 만들지 않는다.
- 카운트, 타격, 탈락음이 동시에 날 때 찢어지는 소리가 없는지 확인한다. 실제 스피커·헤드폰 확인 전에는 오디오 통과로 기록하지 않는다.

## 8. 구현 순서

### P0. 범위와 회귀 시험 고정

- [x] 이 문서의 20명·3스테이지 규칙을 제품 문서와 Gate 1 문서에 반영한다.
- [x] 기존 20스테이지 차트와 순수 코어 시험이 그대로 통과하는지 먼저 고정한다. 현재 58개 통과.
- [x] 현재 듣고 따라하기는 Gate 1 대표 빌드가 아님을 문서에 표시한다.

완료 조건: 코드 변경 전후에 기존 20스테이지 결과가 달라지지 않는다.

### P1. 3스테이지 차트와 완주 sentinel

- [x] `Gate1ChartFactory.CreateMiniContest()`를 추가한다.
- [x] Stage 1은 기본 좌우, Stage 2는 교대, Stage 3은 홀드를 넣는다.
- [x] 완주 `fail_stage`를 차트 길이 기반으로 바꾼다.
- [x] 3스테이지 완주, 각 단계 실패, 무입력 실패 시험을 추가한다.

완료 조건: 입력이 없으면 Stage 1 실패, 정확한 입력이면 Stage 3 완주와 `fail_stage=4`가 나온다.

### P2. 연습생 기록과 순위

- [x] `MiniContestRoster` 한 모듈에 19개 기록, 검증, 순위 비교를 둔다.
- [x] 분포 `8/6/3/2`, 고유 ID, 정확도 범위, 결정성을 시험한다.
- [x] end reason, fail stage, last completed stage, chart/ruleset 버전이 서로 맞는지 검증한다.
- [x] 같은 성능은 같은 표시 순위가 되는지 시험한다.
- [x] 잘못된 roster에서는 순위만 끄고 리듬 플레이를 유지한다.

완료 조건: 같은 빌드와 재도전에서 항상 같은 상대와 같은 순위 결과가 나온다.

### P3. 콘테스트 화면과 정확한 입력

- [x] `ContestPrototypeScreen`을 3스테이지 상태 기계로 바꾼다.
- [x] `InputSystem.onEvent`와 `eventPtr.time`을 사용한다.
- [x] press, release, cancel을 한 번씩만 전달하고 종료 때 구독을 해제한다.
- [x] 스테이지 사이 탈락 연출과 결과·재도전 흐름을 만든다.
- [x] 결과 자동 진행을 없앤다.

완료 조건: 한 판을 시작부터 결과까지 진행하고, 결과에서만 재도전할 수 있다.

### P4. 최소 감각 보강

- [x] 타격, 통과, 실패 세 피드백 묶음을 구현한다.
- [~] DSP 예약 카운트와 합성 마림바를 구현했다. 실제 스피커·헤드폰 청취는 남았다.
- [x] 군중 20칸과 남은 인원 숫자를 safe area 안에 배치한다.
- [ ] 16:9, 20:9, 4:3 가로 화면을 확인한다.

완료 조건: 판정 글자를 가려도 소리와 움직임으로 성공·실패를 구분할 수 있다.

### P5. 자동 시험, 빌드, 수동 확인

- [~] Unity와 같은 컴파일러로 Edit Mode 코드를 컴파일하고 순수 시험 75개를 실행했다. 열린 Unity 프로젝트 때문에 Unity Test Runner 실행은 남았다.
- [~] Play Mode 시험 코드를 추가하고 컴파일했다. 실제 Unity Test Runner 실행은 열린 편집기 때문에 남았다.
- [ ] 현재 커밋의 Android/WebGL 빌드를 새로 만든다.
- [x] 실제 장비 시험은 자동 시험과 별도로 미측정 상태를 유지한다.
- [x] `Gate1LocalRunLog` 한 모듈에 `run_start`, `run_end`, `crowd_drop`, `result_action`을 JSON Lines 형식으로 남긴다.
- [x] `run_end`에는 스테이지, 결과, 지속 시간, 정확도, 점수, Perfect/Good/Miss/ghost miss 집계를 남긴다.
- [x] 연습생 기록은 실제 사용자·테스터 분모에 포함되지 않는지 시험한다. 기술 무효 런도 유효 분모에서 제외한다.

완료 조건: 자동 시험과 빌드 결과를 커밋 해시와 함께 기록한다. Unity가 다른 프로세스에서 열려 있거나 Node 버전이 맞지 않으면 이를 실패/차단으로 정확히 남기고 통과로 쓰지 않는다.

## 9. 시험 지도

```text
CODE PATHS                                      USER FLOWS
[+] CreateMiniContest                           [+] 첫 참가
  ├─ 3 stages / 80·90·100 BPM                    ├─ 조율 → 안내 → 카운트 [PlayMode]
  ├─ tap → alternation → hold                    ├─ Stage 1 통과/실패 [PlayMode]
  └─ full chart remains 20 stages [Regression]   └─ 아무 입력 없음 → 실패 [Regression]

[+] ContestRun
  ├─ stage pass/fail boundaries [EditMode]       [+] 생존 진행
  ├─ 3-stage complete → fail_stage 4             ├─ 20 → 12 → 6 → 3 [PlayMode]
  └─ 20-stage complete → fail_stage 21            ├─ 탈락 중 입력이 다음 stage에 새지 않음
                                                   └─ pause/focus → 재조율 또는 안전 중단

[+] MiniContestRoster                           [+] 결과
  ├─ exactly 19 trainees / unique IDs            ├─ 20명 중 순위가 1..20
  ├─ fixed 8/6/3/2 curve                         ├─ 연습생 표기가 항상 보임
  ├─ end/fail/last-stage consistency             ├─ 잘못된 기록은 실제 순위로 보이지 않음
  ├─ deterministic retry                          ├─ 수동 재도전 → 같은 roster
  ├─ shared rank on exact tie                     └─ roster 오류 → 순위 제외, 플레이 유지
  └─ invalid roster fallback

[+] Input adapter                               [+] 타격 감각
  ├─ event timestamp press/release/cancel         ├─ press 즉시 소리·건반 반응
  ├─ no duplicate subscription                    ├─ tap release가 판정 글자를 지우지 않음
  └─ cancel closes hold as Miss                    └─ hold release/cancel 구분

[+] Gate1LocalRunLog                            [+] 재미 측정
  ├─ run start/end and retry link                 ├─ 120초 안 재도전 판별 가능
  ├─ judgement tally and duration                 ├─ 첫 스테이지·5판·활성 시간 계산 가능
  └─ trainee records excluded from denominator    └─ 저장 실패에도 결과·재도전 유지
```

## 10. 실패 처리와 성능

| 실패                  | 시험                  | 사용자에게 보이는 결과                                             |
| --------------------- | --------------------- | ------------------------------------------------------------------ |
| 차트 검증 실패        | Edit Mode             | 시작을 막고 이유를 기록한다.                                       |
| 조율 실패             | Play Mode             | 게임을 시작하지 않고 다시 조율하게 한다.                           |
| 연습생 기록 오류      | Edit Mode + Play Mode | 리듬은 진행하고 순위만 숨긴다.                                     |
| 군중 연출 오류        | Play Mode             | 숫자만으로 남은 인원을 보여준다.                                   |
| 오디오 생성/로드 실패 | Play Mode             | 시각 플레이는 유지하되 해당 런을 기술 오류로 표시한다.             |
| pause/focus 상실      | Play Mode             | 현재 런을 안전하게 중단하고 재조율한다. 공식 성공으로 세지 않는다. |
| 기록 저장 실패        | Play Mode             | 결과와 재도전은 유지하고 저장 실패를 알린다.                       |

성능 원칙:

- 20개 군중 슬롯과 19개 기록은 시작할 때 한 번 준비한다.
- 연주 중 `Instantiate`, `Destroy`, 네트워크 호출을 하지 않는다.
- 봇 입력을 실시간으로 흉내 내지 않는다.
- 한 프레임마다 순위를 다시 정렬하지 않는다. 스테이지 종료와 최종 결과에서만 계산한다.
- 판정 경로에는 새 문자열·목록·배열 할당을 추가하지 않는다.

## 11. 통과 기준

### 구현 통과

- 총 참가자 수가 항상 20명이다.
- 연습생 19명임을 숨기지 않는다.
- 아무 입력 없이 Stage 1을 통과할 수 없다.
- Stage 3에 홀드가 있다.
- 같은 재도전은 같은 roster를 쓴다.
- 결과가 자동으로 넘어가지 않는다.
- 터치 cancel이 홀드 Miss로 처리된다.
- 기존 20스테이지 코어 시험이 깨지지 않는다.
- roster나 군중 실패가 리듬 플레이 전체를 막지 않는다.

### 재미 검증

기존 Gate 1 기준을 낮추지 않는다.

- 첫 스테이지 완료율 80% 이상
- 첫 실패 후 120초 안 자발적 재도전률 40% 이상
- 첫 세션 5판 도달률 25% 이상
- 첫 세션 콘테스트 활성 플레이타임 평균 3분 이상이며 25%가 3분 초과

자동 시험 통과는 재미 검증 통과가 아니다. 실제 사용자 측정 전에는 Gate 1을 통과로 기록하지 않는다.

## 12. 만들지 않는 것

- 100명 군중과 20스테이지 전체 화면
- 실제 사용자 기록 수집과 봇 자동 교체
- 서버, 로그인, Supabase, 공식 순위, 실제 백분위
- 학점, 학년, 연습실, 뇌 타워
- 적응형 음악과 복잡한 AudioMixer
- 게임패드 리바인딩과 전체 메뉴 상태 스택
- 최종 캐릭터 아트, 다중 악기, 광고
- 현재 듣고 따라하기의 추가 기능과 폴리시

설치된 게임 스킬은 개발 도구의 판단 기준일 뿐 게임 런타임 의존성이 아니다. 이번 구현에서는 입력 정확도, 안전한 화면 배치, 최소 타격 피드백, DSP 오디오 예약만 가져온다.

## 13. 반박 관점 리뷰

### “봇과 겨루면 경쟁이 가짜처럼 보인다”

맞는 위험이다. 그래서 사람처럼 꾸미지 않고 모두 `연습생 01`처럼 표시한다. 이번 실험은 사회적 증명을 만드는 것이 아니라, **비교와 탈락이 재도전을 만드는지** 확인하는 것이다. 연습생 표기를 숨겨야만 재미있다면 이 방향은 실패다.

### “20명도 근거 없는 숫자다”

맞다. 20명이 장기 정답이라고 주장하지 않는다. 다만 100명보다 싸고, 16명보다 군중감이 있으며, 순위가 5% 단위라 이번 시험에 좋다. 인원 수는 설정값으로 두고 실제 가독성 문제가 나오면 16명과 비교한다.

### “3스테이지는 너무 짧아서 제품을 대표하지 못한다”

장기 제품은 대표하지 않는다. 하지만 현재 필요한 것은 콘텐츠 양이 아니라 `타격 → 생존 → 탈락 구경 → 결과 → 재도전` 한 바퀴다. 이 바퀴가 재미없으면 20스테이지를 만들어도 실패 비용만 커진다.

### “고정 기록은 반복하면 뻔해진다”

초기에는 오히려 장점이다. 같은 상대를 두고 내 순위가 오르는지 알 수 있다. 매번 roster를 바꾸면 실력 향상인지 운 좋은 상대인지 구분하기 어렵다. 일일 변주는 Gate 1 통과 뒤의 문제다.

### “듣고 따라하기가 더 독특할 수 있다”

가능성은 남아 있다. 현재 커밋을 Git 기록에 보존한다. 하지만 Gate 1의 제품 가설은 듣고 따라하기가 아니라 마림바 손맛과 군중 탈락이 한 판 더를 만드는지다. 두 실험을 한 빌드에 섞지 않는다.

### “음색이 나쁜 상태에서 경쟁만 붙이면 잘못된 결론을 낸다”

가장 강한 반박이다. 외부 테스트 전에 마림바 음색과 즉시 타격 반응을 최소 기준까지 올린다. 다만 여러 곡, 적응형 음악, 믹서 설정 화면까지 만들지는 않는다.

## 14. 구현 작업 목록

이 목록은 엔지니어링 검토에서 나온 실행 작업이다. 각 작업을 끝낼 때 바로 체크한다.

- [x] **T1 (P1, 사람: 약 2시간 / Codex: 약 20분)** — `RhythmCore` — 3스테이지 차트와 길이 기반 완주 sentinel을 추가한다.
  - 나온 이유: 구조 검토 — 20스테이지 전용 상수 `fail_stage=21`을 그대로 두면 미니 차트 완주 기록이 틀어진다.
  - 파일: `unity/Assets/Mallang/RhythmCore/Runtime/ChartCompiler.cs`, `unity/Assets/Mallang/RhythmCore/Runtime/ContestRun.cs`, `unity/Assets/Mallang/RhythmCore/Tests/EditMode/RhythmCoreTests.cs`
  - 확인: 3스테이지 완주 `fail_stage=4`, 20스테이지 완주 `fail_stage=21`
- [x] **T2 (P1, 사람: 약 2시간 / Codex: 약 20분)** — `Gate1 Runtime` — 고정 연습생 기록 19개와 순위 비교를 추가한다.
  - 나온 이유: 구조·실패 검토 — 가짜 실시간 경쟁처럼 보이지 않는 결정적 roster와 순위 실패 격리가 필요하다.
  - 파일: `unity/Assets/Mallang/Gate1/Runtime/MiniContestRoster.cs`, `unity/Assets/Mallang/Gate1/Tests/EditMode/MiniContestRosterTests.cs`
  - 확인: 8/6/3/2 분포, 기록 내부 일관성, 같은 재도전 결과, 동점 공동 순위, 잘못된 roster 폴백
- [x] **T3 (P1, 사람: 약 4시간 / Codex: 약 45분)** — `ContestPrototypeScreen` — 3스테이지 콘테스트 상태 기계와 정확한 입력 이벤트를 연결한다.
  - 나온 이유: 코드 품질 검토 — 현재 자동 반복, 자동 결과 종료, 입력 polling과 release 피드백 덮어쓰기가 대표 흐름을 깨뜨린다.
  - 파일: `unity/Assets/Mallang/Gate1/Runtime/ContestPrototypeScreen.cs`, `unity/Assets/Mallang/Gate1/Tests/PlayMode/ContestPrototypeScreenTests.cs`
  - 확인: press/release/cancel, 결과 수동 재도전, pause/focus 처리, 이벤트 구독 해제
- [~] **T4 (P2, 사람: 약 3시간 / Codex: 약 35분)** — `Gate1 Presentation` — 20칸 군중, 세 단계 피드백, 최소 마림바 믹스를 만들었다. 화면 비율과 실제 청취 확인은 남았다.
  - 나온 이유: 게임 감각·오디오·UI 검토 — 경쟁 숫자가 보이고 타격 시점이 들려야 핵심 재미를 판단할 수 있다.
  - 파일: `unity/Assets/Mallang/Gate1/Runtime/ContestPrototypeScreen.cs`, `unity/Assets/Mallang/Gate1/Scenes/Gate1ContestPrototype.unity`
  - 확인: 16:9·20:9·4:3, 숫자와 슬롯 일치, safe area, 동시음 무클리핑
- [~] **T5 (P1, 사람: 약 3시간 / Codex: 약 40분)** — `Tests and Build` — 런타임·Edit/Play Mode 시험 코드는 컴파일됐고 독립 시험 75개는 통과했다. Unity Test Runner와 Android/WebGL 빌드는 열린 Unity 프로젝트 때문에 남았다.
  - 나온 이유: 시험·실패 전파 검토 — 봇과 로그 오류가 플레이 전체 실패로 번지지 않고 기존 20스테이지가 퇴행하지 않아야 한다.
  - 파일: `unity/Assets/Mallang/RhythmCore/Tests/EditMode/RhythmCoreTests.cs`, `unity/Assets/Mallang/Gate1/Tests/EditMode/`, `unity/Assets/Mallang/Gate1/Tests/PlayMode/`, `docs/03-development/gate-1-mini-contest-test-plan.md`
  - 확인: 로컬 JSON Lines 계측, Edit/Play Mode, Android/WebGL build, 미측정 장비 항목 유지

순차 구현한다. 핵심 화면, 입력, 코어가 서로 같은 파일과 상태를 만지므로 병렬 worktree로 나누면 충돌 비용이 더 크다.

## 15. 구현용 통합 프롬프트

아래 프롬프트는 이 계획을 실제 코드로 옮길 때 그대로 사용한다.

```text
당신은 /Users/easyoon/mallang-brain 저장소의 Unity Gate 1 구현 담당자다.

먼저 다음 문서를 끝까지 읽어라.
- /Users/easyoon/mallang-brain/docs/03-development/gate-1-mini-contest-plan.md
- /Users/easyoon/mallang-brain/docs/03-development/gate-1-implementation-plan.md
- /Users/easyoon/mallang-brain/docs/01-product/practice-contest-balance-design.md
- /Users/easyoon/mallang-brain/AGENTS.md

목표는 현재 듣고 따라하기 자동 반복 화면을 Gate 1 대표 빌드에서 빼고, 기존 RhythmCore를 이용한 3스테이지·20명 미니 브레인 콘테스트를 완성하는 것이다.

고정 결정:
1. 총 20명 = 플레이어 1명 + `연습생`이라고 공개한 합성 기록 19명.
2. 연습생 분포는 Stage 1 실패 8명, Stage 2 실패 6명, Stage 3 실패 3명, 완주 2명이다.
3. 플레이어 생존은 기존 ContestRun의 정확도 통과선으로만 결정한다.
4. 3스테이지는 기본 좌우 탭 → 좌우 교대 → 탭+홀드 순서다.
5. 순위는 완주 여부 → 마지막 통과 스테이지 → 전체 정확도 → rank score 순이다. 완전 동점은 공동 순위다.
6. 같은 빌드에서 재도전하면 같은 roster를 쓴다.
7. 실제 사용자, 실시간 접속, 실제 백분위처럼 보이게 만들지 않는다.
8. 서버, 로그인, 학점, 연습실, 뇌 타워, 광고, 100명 UI는 만들지 않는다.

반드시 사용할 판단 기준:
- game-feel: 일반 타격, 스테이지 통과, 실패 세 피드백 묶음만 만든다. 일반 타격에 화면 흔들기나 시간 정지를 쓰지 않는다.
- audio-design: DSP 시각으로 카운트와 큐를 예약하고, 플레이어 소리·카운트·탈락음의 headroom을 확인한다. 적응형 음악과 복잡한 믹서는 만들지 않는다.
- unity-input-system: 프레임 polling 대신 Gate0MeasurementController처럼 InputSystem.onEvent와 eventPtr.time을 사용한다. press, release, cancel을 처리하고 OnDestroy/OnDisable에서 반드시 구독을 해제한다.
- game-ui-ux: Screen.safeArea를 지키고 16:9, 20:9, 4:3 가로 화면에서 20칸 군중과 결과 버튼이 잘리지 않게 한다. 이번에는 현재 즉시 모드 GUI를 유지한다.

구현 순서:
A. 시작 전 git status를 확인하고 사용자 변경을 보존한다. 파괴적인 checkout/reset을 하지 않는다.
B. 현재 RhythmCore와 시험을 읽고 기준 결과를 기록한다.
C. Gate1ChartFactory.CreateMiniContest()를 추가하되 기존 Create()의 20스테이지 결과를 바꾸지 않는다.
D. ContestRun의 완주 fail_stage를 chart.Stages.Count + 1로 바꾸고 3/20스테이지 회귀 시험을 쓴다.
E. MiniContestRoster 한 모듈에 19개 기록, 검증, 공동 순위 계산, 오류 fallback을 구현한다.
   - Failed 기록은 last_completed_stage=fail_stage-1, Completed 기록은 last_completed_stage=3 및 fail_stage=4인지 검사한다.
   - chart_id, ruleset_version, rank_ruleset, roster_version이 다르면 해당 roster를 무효로 처리한다.
F. ContestPrototypeScreen을 Calibrating → Ready → CountIn → Playing → StageResult → Result 상태 기계로 바꾼다.
G. 입력은 원본 이벤트 timestamp를 정수 마이크로초로 변환한다. touch cancel은 ProcessCancel로 보낸다. UnmatchedRelease가 직전 판정 표시를 지우지 않게 한다.
H. 20개 슬롯을 미리 준비하고 연주 중 Instantiate/Destroy/네트워크 호출을 하지 않는다. 스테이지 종료 때만 순위와 생존 수를 갱신한다.
I. 타격, 통과, 실패 피드백과 최소 마림바 음색을 구현한다. 연습생 소리는 재생하지 않는다.
J. 결과 화면에 `연습생 기록 19명 포함`, `20명 중 N위`, 정확도, 점수, `다시 참가`를 표시한다. 자동 재시작은 금지한다.
K. 문서의 시험 지도를 따라 Edit Mode와 Play Mode 시험을 추가한다.
L. Gate1LocalRunLog 한 모듈로 run_start, run_end, crowd_drop, result_action을 JSON Lines에 남긴다. 저장 실패는 결과와 재도전을 막지 않아야 한다. 연습생 기록은 실제 사용자 분모에 넣지 않는다.
M. 가능한 자동 시험과 Android/WebGL 빌드를 실행한다. Unity 프로젝트가 다른 프로세스에서 열려 있거나 Node 버전이 맞지 않으면 프로세스를 죽이거나 기준을 낮추지 말고 정확한 blocker로 보고한다.

중요한 회귀 조건:
- 무입력은 Stage 1을 통과하지 못한다.
- 3스테이지 완주 fail_stage는 4다.
- 기존 20스테이지 완주 fail_stage는 21이다.
- 기존 순수 RhythmCore 시험은 모두 유지한다.
- roster/군중 오류가 리듬 플레이와 재도전을 막지 않는다.
- 결과는 사용자가 버튼을 누르기 전까지 유지된다.
- 실제 장비 측정은 자동 시험과 구분하고 미측정 항목을 통과로 쓰지 않는다.

구현 후 스스로 반박 검토를 하라.
1. 봇이 사람처럼 보이는 문구나 UI가 남았는가?
2. 20명 때문에 핵심 리듬 큐가 작아졌는가?
3. 시각 연출이 입력이나 DSP 진행을 막는가?
4. 연습생 데이터 오류가 전체 흐름으로 번지는가?
5. 현재 코드보다 더 복잡한데 사용자에게 보이는 이득이 없는 추상화가 생겼는가?
6. 16명으로 줄여야 할 실제 가독성 증거가 있는가, 아니면 추측뿐인가?

마지막 보고는 다음 네 부분으로 쓴다.
- 완료한 일
- 실행한 시험과 정확한 결과
- 아직 확인하지 못한 일, 특히 실제 장비와 오디오 청취
- 사용자가 직접 확인할 가장 짧은 플레이 경로

코드와 시험이 끝나지 않았으면 완료라고 말하지 마라. 커밋이나 push는 사용자가 따로 요청할 때만 한다.
```

## 16. 엔지니어링 검토 요약

- 범위 검토: 100명·20스테이지·서버를 잘라내고, 재미 가설을 확인하는 20명·3스테이지 한 바퀴로 줄였다.
- 구조 검토: `RhythmCore`를 재사용하고 새 핵심 모듈은 `MiniContestRoster`, `Gate1LocalRunLog` 두 개로 제한했다.
- 코드 품질 검토: 실험 화면은 현재 즉시 모드 GUI를 유지한다. UI 프레임워크 교체와 공용 추상화는 미뤘다.
- 시험 검토: 코어 경계, roster 일관성, 입력 cancel, 상태 전환, fallback, 전체 플레이 흐름을 시험 지도에 넣었다.
- 성능 검토: 20개 정적 슬롯, 종료 시점 순위 계산, 플레이 중 생성·삭제·네트워크 없음으로 제한했다.
- 실패 전파 검토: roster, 군중, 로그 저장이 실패해도 리듬 플레이·로컬 결과·재도전은 유지한다.
- TODO 검토: 기존 `TODOS.md`의 Gate 1 항목으로 충분하다. 중복 TODO를 만들지 않는다.
- 병렬화 검토: 핵심 상태와 파일이 겹치므로 순차 구현한다.
- 완전성 선택: 6개 주요 결정 모두 오류 경로와 회귀 시험을 포함한 쪽을 선택했다.
- 외부 검토: Codex CLI는 로컬 상태 DB 쓰기 권한 문제로 실행되지 않았고, 대체 독립 검토는 사용량 제한으로 실행되지 않았다. 계획 검토를 막는 조건은 아니지만 독립 의견은 아직 없다.

## GSTACK REVIEW REPORT

| Review        | Trigger               | Why           |     Runs | Status      | Findings                                         |
| ------------- | --------------------- | ------------- | -------: | ----------- | ------------------------------------------------ |
| CEO Review    | `/plan-ceo-review`    | 범위와 전략   |        0 | —           | 이번 결정은 사용자가 C와 20명 방향을 직접 선택함 |
| Codex Review  | `/codex review`       | 독립 2차 의견 | 2회 시도 | UNAVAILABLE | 로컬 권한 문제, 대체 검토 사용량 제한            |
| Eng Review    | `/plan-eng-review`    | 구조와 시험   |        1 | CLEAR       | 7개 보강점 반영, 치명적 공백 0                   |
| Design Review | `/plan-design-review` | UI/UX 공백    |        0 | —           | 구현 뒤 실제 화면으로 검토 필요                  |
| DX Review     | `/plan-devex-review`  | 개발 경험     |        0 | —           | 이번 계획의 필수 조건 아님                       |

**VERDICT:** ENG CLEARED — 구현을 시작할 수 있다. 독립 검토와 실제 장비·오디오 청취는 아직 완료되지 않았다.

NO UNRESOLVED DECISIONS
