# RR02 8박 기억 합주 프로토타입 구현 계획

> **For agentic workers:** 이 계획은 game-build 절차로 실행합니다 — 테스트는 4단계에서 패키지만 읽은 서브에이전트가 일괄 작성하고, 로직 과제(L1~L7)는 5단계 하네스(로컬 모델 루프)가 features.json 항목으로 하나씩 구현하며, 장면 과제(S1~S6)는 세션이 Unity MCP로 직접 작업합니다. 체크박스는 진행 추적용입니다.

**Goal:** 잠긴 패키지 specs/001-listen-and-repeat-core(RR02)의 판정·점수·저장·랭킹 로직과 5개 화면 흐름을 Unity 프로토타입으로 구현합니다.

**Architecture:** 새 어셈블리 `Mallang.RR02.Runtime`에 순수 C# 로직(차트·판정·진행·점수·저장·랭킹)을 만들고 EditMode 테스트로 포위합니다. 화면은 같은 어셈블리의 Screens가 로직을 소비하며, 시계 보정은 Gate 0A에서 실기기 검증된 `Mallang.Gate0.Runtime` Clock 계열을 재사용합니다. 기존 `Mallang.RhythmCore`는 수정하지 않습니다(FR-002 회귀 보존).

**Tech Stack:** Unity 6000.3.23f1, Unity Test Framework(EditMode), JsonUtility, 기존 RhythmMath(round_half_away_from_zero).

**Spec:** `/Users/easyoon/mallang-brain/specs/001-listen-and-repeat-core/` (읽기 전용). 요구사항↔코드 연결은 [map.md](map.md), 추측은 [assumptions.md](assumptions.md).

## Global Constraints

- 판정·시간은 **int64 마이크로초와 round_half_away_from_zero만** 사용합니다(float 판정 금지) (필수). 반올림은 `Mallang.RhythmCore.RhythmMath.RoundHalfAwayFromZero`를 참조합니다.
- 경계값: Perfect ±60,000µs, Good ±120,000µs(모두 양끝 포함), 소비 창 ±120,000µs, 소유 구간 = 첫 목표 −180,000µs ~ 마지막 목표 +180,000µs(양끝 포함).
- 점수: Perfect 100 · Good 70 · Miss 0. **콤보 배율 없음.** ghost는 0점 + 해당 단계 실패.
- 타이밍: 150BPM, 1박 = 400,000µs, 1박 = 480 tick. 차트 데이터는 패키지 `design-tools/four-friend-chart.json`을 **그대로 복사**해 쓰고 내용을 고치지 않습니다 (필수).
- 친구 식별자: `rabbit`, `capybara`, `fox`, `squirrel` (차트 JSON의 id와 일치).
- 저장 키: `brain:RR02:v1:<friend>`, `reach:RR02:v1`, `badge:RR02:v1:<badge_id>`. RR01 값 자동 변환 없음.
- `Mallang.RhythmCore`·`Mallang.Gate0`·`tools/handoff/`·`harness/`·`features.json`·기존 테스트는 수정 금지 (필수).
- 로직 레인 check: `node tools/handoff/run-tests.mjs edit "<fixture 전체 이름>"` (종료 코드 0 = 통과).

## Review Focus

스펙이 암시하지만 기본 테스트가 놓치기 쉬운 것 다섯 가지입니다. 각각 소유 과제의 테스트 단계에 핀으로 박았습니다.

1. **±120,000µs 정확히**: 경계는 양끝 포함이므로 Good으로 소비돼야 합니다. ±120,001µs는 소비되지 않습니다. → L2
2. **음수 offset 반올림**: −60,000µs 정확히는 Perfect(절댓값 대칭). round_half_away_from_zero가 음수에서도 성립해야 합니다. → L2
3. **거의 동시인 두 입력**: 두 패드를 같은 프레임에 눌러도 sequence_id 순으로 하나씩 처리하고, 먼저 소비된 목표를 두 번째 입력이 되살리지 않습니다(AC-004). → L2
4. **revision 역순·중복 쓰기**: 뒤늦게 도착한 낮은 revision이나 같은 revision의 중복이 확정값을 덮지 않습니다(AC-017). → L5
5. **N=19→20 경계와 꼴찌 백분위**: N=19는 실순위+기준표 병기, N=20부터 공식 순위·백분위이고 rank=N이면 ceil(1000×N/N)/10 = 100.0%입니다. → L6

추가 핀(과제 내): 무한 k=9가 R2로 순환(L1), 무한 구절에 R1 두 소리 패턴 미사용(L1).

---

## 0단계 준비 (세션, 테스트 번역과 같은 커밋 흐름)

- [ ] `Mallang.RR02.Runtime.asmdef`(참조: Mallang.RhythmCore.Runtime)와 `Mallang.RR02.Tests.EditMode.asmdef` 생성, 패키지에서 `four-friend-chart.json`·`ranking-benchmark.json`을 `unity/Assets/Mallang/RR02/Data/`로 복사(출처 주석 포함), 빈 타입 뼈대 커밋.
- [ ] 하네스 복사·적응: driftfish `harness/` → `palolo/harness/` (경로 `project/`→`unity/Assets/Mallang/RR02/`, 보호 경로에 `unity/Assets/Mallang/RR02/Tests` 추가, 회귀 재검사를 통과 기능 필터를 세미콜론으로 합친 1회 실행으로 교체, CHECK_TIMEOUT 900초). env.sh는 복사하되 커밋 금지.

---

### Task L1: 차트 로드와 타이밍 산수 [로직]

**Files:**
- Create: `unity/Assets/Mallang/RR02/Runtime/Rr02Types.cs`, `Rr02Chart.cs`
- Test: `unity/Assets/Mallang/RR02/Tests/EditMode/Rr02ChartTests.cs`

**Interfaces (Produces):**
```csharp
public enum Rr02Friend { Rabbit, Capybara, Fox, Squirrel }
public enum Rr02Judgement { Perfect, Good, Miss }
public static class Rr02Rules {
    public const long PerfectWindowUs = 60000, GoodWindowUs = 120000, OwnershipPadUs = 180000, BeatUs = 400000;
    public const int TicksPerBeat = 480, PerfectScore = 100, GoodScore = 70;
}
public readonly struct Rr02Note { public readonly int NoteId; public readonly Rr02Friend Friend; public readonly long TargetUs; }
public sealed class Rr02Round { public int Number; public IReadOnlyList<Rr02Note> Notes; public IReadOnlyList<int> HiddenIndices; }
public sealed class Rr02Chart {
    public IReadOnlyList<Rr02Round> Rounds;                  // 9개
    public static Rr02Chart LoadFromJson(string json);       // four-friend-chart.json 파싱(JsonUtility)
    public Rr02Round InfinitePhrase(int k);                  // k=1→R2 … k=8→R9, k=9→R2 순환, HiddenIndices=0~7 전부
}
```

- [ ] 테스트: `Rounds`가 9개이고 R1 시퀀스가 rabbit,rabbit,capybara,capybara,rabbit,capybara,rabbit,capybara(2소리)인지(AC-003) / hiddenIndices가 R1=0개·R2=[7]·R9=8개(뒤쪽 r−1개)인지(AC-011) / targetTicks 0,480,…,3360이 TargetUs 0,400000,…,2800000으로 변환되는지 / 홀드류 개념이 없는지(노트 전부 단일 시각, AC-002) / InfinitePhrase(1)=R2 시퀀스·(8)=R9·(9)=R2이고 전부 숨김이며 R1 패턴이 안 나오는지(AC-013) / 타이밍 산수: 시연+입력 8박 각 3.2초, 라운드 20박=8초, 9라운드 72판정=72초, 카운트인 12박 4.8초 합 76.8초(AC-007).
- [ ] check: `node tools/handoff/run-tests.mjs edit "Mallang.RR02.Tests.EditMode.Rr02ChartTests"`

### Task L2: 탭 판정기 [로직]

**Files:**
- Create: `unity/Assets/Mallang/RR02/Runtime/Rr02Judge.cs`
- Test: `unity/Assets/Mallang/RR02/Tests/EditMode/Rr02JudgeTests.cs`

**Interfaces (Consumes L1 / Produces):**
```csharp
public enum Rr02InputKind { Judged, Ghost, OutsideOwnership }
public readonly struct Rr02InputResult { public readonly Rr02InputKind Kind; public readonly int NoteId; public readonly Rr02Judgement Judgement; }
public sealed class Rr02Judge {
    public Rr02Judge(IReadOnlyList<Rr02Note> notes);
    public Rr02InputResult ProcessPress(Rr02Friend friend, long inputUs);  // 호출 순서 = sequence_id 순
    public void AdvanceTime(long nowUs);                                   // 목표+120ms 지난 노트를 Miss로 닫음
    public IReadOnlyList<Rr02Judgement> Outcomes { get; }                  // note_id 순 최종 판정
    public int GhostCount { get; }
    public bool Completed(long nowUs);                                     // 마지막 목표+180ms 경과
}
```

- [ ] 테스트(경계 전수, AC-005): offset −121ms→미소비, −120ms→Good, −61ms→Good, −60ms→Perfect, 0→Perfect, +60ms→Perfect, +61ms→Good, +120ms→Good, +121ms→미소비, 목표+120ms 초과 미입력→Miss. 같은 목표 중복 소비 0건.
- [ ] 테스트(소리 주인, AC-004): 여우 목표 0ms에 토끼 패드 0ms 입력→그 목표 Miss 1·ghost 0, 이어 +20ms 두 번째 입력→닫힌 목표를 되살리지 않고 ghost 1. 처리 순서는 호출(sequence_id) 순.
- [ ] 테스트(선택 규칙): 같은 친구의 열린 목표 여럿이면 |offset| 최소 → 동률 시 이른 목표 → 다시 동률 시 낮은 note_id.
- [ ] 테스트(소유 구간): 첫 목표 −180,001µs 입력→OutsideOwnership, −180,000µs→구간 내. 마지막 목표 +180,000µs까지 포함.
- [ ] check: `node tools/handoff/run-tests.mjs edit "Mallang.RR02.Tests.EditMode.Rr02JudgeTests"`

### Task L3: 도전 상태기계 [로직]

**Files:**
- Create: `unity/Assets/Mallang/RR02/Runtime/Rr02Run.cs`
- Test: `unity/Assets/Mallang/RR02/Tests/EditMode/Rr02RunTests.cs`

**Interfaces (Consumes L1·L2 / Produces):**
```csharp
public enum Rr02Phase { Listen, Repeat, Ended }
public enum Rr02PracticeReason { None, Pause, ClockError, SoundFallback, UserAssist }
public sealed class Rr02RunResult {
    public int NormalCleared; public int Loops; public bool OfficialEligible;
    public IReadOnlyDictionary<Rr02Friend, int> AnimalScores; public int TotalScore; public bool AllPerfect;
}
public sealed class Rr02Run {
    public Rr02Run(Rr02Chart chart);
    public Rr02Phase Phase { get; }  public int CurrentRound { get; }      // 1~9, 무한은 10+k가 아닌 Loops로 구분
    public int NormalCleared { get; } public int Loops { get; } public bool OfficialEligible { get; }
    public void BeginRepeat();                                             // 듣기→따라치기 전환(구절 시작 기준시각 0)
    public Rr02InputResult ProcessPress(Rr02Friend f, long usFromRepeatStart);
    public void AdvanceTime(long usFromRepeatStart);                       // 구절 종료 확정 포함(마지막+180ms에 1회)
    public void InvalidateOfficial(Rr02PracticeReason reason);             // 완료 단계 유지, 같은 듣기부터 재개(MCH-007)
    public Rr02RunResult Result { get; }                                   // Ended 후
}
```

- [ ] 테스트(구절 성공/진행): 전 목표 Good 이상·Miss 0·ghost 0 → NormalCleared+1, 다음 라운드 Listen으로(AC-003). R9 성공 → 무한 구절, 구절 성공마다 Loops+1(AC-013).
- [ ] 테스트(점수, AC-006): Perfect2·Good1·Miss1 = 270점이 그 친구들의 AnimalScores에 합산, 콤보 배율 없음, ghost 입력은 0점 + 그 단계 실패.
- [ ] 테스트(실패, AC-008): 길이 3 입력 중 2번째 Miss → 음악(구간)은 계속, 마지막 목표+180ms에 종료 1회, NormalCleared는 직전까지, 올바른 입력 점수는 포함, Phase=Ended.
- [ ] 테스트(자격, AC-009·MCH-007): Repeat 중 InvalidateOfficial(Pause) → OfficialEligible=false, NormalCleared 유지, 같은 라운드 Listen부터 재개, 미완료 구절 입력은 폐기.
- [ ] 테스트(FR-025 전제): R2 입력 구간에서 토끼만 여러 번 올바르게 치면 토끼 AnimalScores만 증가.
- [ ] check: `node tools/handoff/run-tests.mjs edit "Mallang.RR02.Tests.EditMode.Rr02RunTests"`

### Task L4: 뇌 점수·배지 [로직]

**Files:**
- Create: `unity/Assets/Mallang/RR02/Runtime/BrainScore.cs`
- Test: `unity/Assets/Mallang/RR02/Tests/EditMode/BrainScoreTests.cs`

**Interfaces (Consumes L3 / Produces):**
```csharp
public static class BrainScore {
    public static int Stage(int score);                       // 0~199→0(콩알) 200~399→1(자람) 400~699→2(점화) 700+→3(치솟음)
    public static IReadOnlyDictionary<Rr02Friend,int> MergeBest(IReadOnlyDictionary<Rr02Friend,int> before, IReadOnlyDictionary<Rr02Friend,int> run, bool eligible); // eligible일 때만 max, 아니면 before 그대로
    public static int MyBrainTotal(IReadOnlyDictionary<Rr02Friend,int> bestAfter);  // 4종 합
    public static IReadOnlyList<string> Badges(Rr02RunResult r, int prevBestLoops); // "all-clear"(NormalCleared=9), "all-perfect"(AllPerfect), "loop-best"(Loops>prevBestLoops)
}
```

- [ ] 테스트(AC-015 수치 그대로): before 1200/1100/900/800 + run 1400/980/1100/850(유효) → bestAfter 1400/1100/1100/850, MyBrainTotal 4000→4450, 이번 도전 합 4330과 구분(카피바라 최고 1100 유지).
- [ ] 테스트(단계 경계): 0·199→0, 200→1, 399→1, 400→2, 699→2, 700→3 (FR-025).
- [ ] 테스트(자격): eligible=false면 bestAfter=before (FR-015).
- [ ] check: `node tools/handoff/run-tests.mjs edit "Mallang.RR02.Tests.EditMode.BrainScoreTests"`

### Task L5: 저장·병합 [로직]

**Files:**
- Create: `unity/Assets/Mallang/RR02/Runtime/Rr02Store.cs`
- Test: `unity/Assets/Mallang/RR02/Tests/EditMode/Rr02StoreTests.cs`

**Interfaces (Produces):**
```csharp
public interface IKeyValueStore { bool TryRead(string key, out string value); bool TryWrite(string key, string value); }
public readonly struct BrainRecord { public readonly int Confirmed; public readonly int Revision; public readonly bool Unverified; }
public sealed class Rr02Store {
    public Rr02Store(IKeyValueStore store);
    public BrainRecord ReadBrain(Rr02Friend f);                        // 키 brain:RR02:v1:<friend>, 읽기 실패 시 Unverified=true
    public bool WriteBrainMax(Rr02Friend f, int candidate);            // max 병합 + revision+1 직렬, 쓰기 확인 후 true
    public (int normalCleared, int loops, string runId) ReadReach();   // reach:RR02:v1
    public bool WriteReachMax(int normalCleared, int loops, int runScore, string runId); // 순위 키 비교로 갱신
    public bool WriteBadge(string badgeId, string runId, long earnedAtUs); // badge:RR02:v1:<id>, 최초 1회만
}
```

- [ ] 테스트(AC-017): 읽기 실패(Unverified) 상태에서 WriteBrainMax가 원본을 덮지 않고 false / 역순 revision(낮은 값 재도착)·같은 revision 중복이 확정값을 덮지 않음 / max 병합(900 저장소 vs 800 후보 → 900 유지) / RR01 키의 숫자 변환 없음(brain:RR01:* 무시).
- [ ] 테스트(키 형식): 네 친구 키가 정확히 `brain:RR02:v1:rabbit` 형태(FR-017), 저장됨 표시는 TryWrite true 후에만.
- [ ] check: `node tools/handoff/run-tests.mjs edit "Mallang.RR02.Tests.EditMode.Rr02StoreTests"`

### Task L6: 랭킹 규칙 [로직]

**Files:**
- Create: `unity/Assets/Mallang/RR02/Runtime/RankingRules.cs`
- Test: `unity/Assets/Mallang/RR02/Tests/EditMode/RankingRulesTests.cs`

**Interfaces (Produces):**
```csharp
public readonly struct ReachKey { public readonly int NormalCleared; public readonly int Loops; public readonly int RunScore; }
public enum RankingMode { BenchmarkOnly, RankWithBenchmark, Official }
public static class RankingRules {
    public static int Compare(ReachKey a, ReachKey b);                                   // 내림차순 키(완료,무한,점수)
    public static IReadOnlyList<(string player, int rank)> Rank(IReadOnlyList<(string, ReachKey)> entries); // 공동 순위 1,1,3
    public static double Percentile(int rank, int n);                                    // ceil(1000×rank/n)/10.0
    public static RankingMode Mode(int population);                                      // 0→BenchmarkOnly, 1~19→RankWith, 20+→Official
    public static string BenchmarkGrade(BenchmarkTable t, ReachKey mine);
    public static BenchmarkTable ParseBenchmark(string json);                            // ranking-benchmark.json
}
```

- [ ] 테스트(AC-021): (9,3,5000) > (9,2,9999) > (8,0,9999) 순서 / 완전 동점 두 명 공동 1위, 다음은 3위 / 부분 성공은 키에 없음.
- [ ] 테스트(AC-022·Review Focus 5): Mode(0)=BenchmarkOnly, Mode(1)·Mode(19)=RankWithBenchmark, Mode(20)=Official / Percentile(1,20)=5.0, Percentile(20,20)=100.0, Percentile(7,23)=ceil(7000/23)/10=30.5 / 기준표는 공식 집계에 미합산(Rank 입력에 벤치마크 행 없음).
- [ ] check: `node tools/handoff/run-tests.mjs edit "Mallang.RR02.Tests.EditMode.RankingRulesTests"`

### Task L7: 공식 기록 제출 [로직]

**Files:**
- Create: `unity/Assets/Mallang/RR02/Runtime/RankingSubmission.cs`
- Test: `unity/Assets/Mallang/RR02/Tests/EditMode/RankingSubmissionTests.cs`

**Interfaces (Consumes L3·L6 / Produces):**
```csharp
public enum SubmissionState { Pending, Verifying, Confirmed, Held, Rejected }
public interface IRankingServer { SubmissionState Submit(string runId, string rulesVersion, ReachKey key, bool eligible); }
public sealed class RankingSubmission {
    public RankingSubmission(IRankingServer server, string rulesVersion);
    public SubmissionState Submit(string runId, ReachKey key, bool eligible);  // 같은 runId 재호출 = 같은 응답, 중복 집계 없음
    public void OnTimeout(string runId);                                       // → Held (이동은 막지 않음: 상태만)
    public SubmissionState StateOf(string runId);
}
public sealed class FakeRankingServer : IRankingServer { }                     // 멱등 + 부적격 Rejected + 다른 rulesVersion Rejected
```

- [ ] 테스트(AC-024): 같은 runId 두 번 제출 → 같은 응답, 집계 1회 / eligible=false → Rejected(연습 분리) / 다른 rulesVersion → Rejected / 검증 전 상태에서 공식 순위 없음(Verifying) / OnTimeout → Held, 이전 Confirmed 기록은 유지.
- [ ] check: `node tools/handoff/run-tests.mjs edit "Mallang.RR02.Tests.EditMode.RankingSubmissionTests"`

---

## 장면 레인 (세션 + Unity MCP, 과제마다 증거 생성 → 사람 확인)

장면 과제는 테스트로 완성을 주장하지 않습니다. 각 과제 끝에 스크린샷(필요하면 플레이 영상)을 만들어 사람 확인을 요청하고, 기각 사유는 기준 문서로 내려보낸 뒤 다시 시도합니다. 소리·손맛·실기기 동작은 이 환경에서 통과를 주장하지 않습니다 (필수).

### Task S1: 씬·16:9·배치 뼈대 [장면 | FR-001·020]
- [ ] `Rr02Prototype.unity` 씬, 1280×720 기준 16:9 고정(레터박스), 좌우 카드 각 폭 26%·높이 50%(위 0/아래 50%), 중앙 48% 악보 영역. 외형이 입력 영역을 바꾸지 않는 히트 영역 분리.
- [ ] 증거: 1280/960/640 너비 스크린샷 3장(오차 1px 이하 확인은 수치 로그 병기) → 사람 확인.

### Task S2: 소리 안내·타이틀 [장면 | FR-027, SCR-001]
- [ ] 진입 직후 이어폰 안내 화면(1.5~2초 경과 또는 탭으로 타이틀), 타이틀에 시작 동선·네 친구·HUD 자리.
- [ ] 증거: 안내→타이틀 전환 영상 → 사람 확인.

### Task S3: 플레이 화면 [장면 | FR-003·011·012·018 표현, SCR-002]
- [ ] 8칸 악보(숨김 ?칸은 chart.hiddenIndices 단일 기준), 현재박 표시, 시연(큰 동작)→입력 전환, placeholder 4음(사인톤 등 구별되는 임시음, 입력 즉시 재생), Gate0 Clock으로 보정·카운트인. ?칸에 정답 얼굴·이름·고유색·접근성 이름 금지.
- [ ] 증거: R1→R2 플레이 영상(성공·실패 각 1회) → 사람 확인.

### Task S4: 결과 화면 [장면 | FR-010·016·025, SCR-005]
- [ ] 동물별 전후 점수→최고 갱신 뇌 확대→빛 중앙 모임→합산(약 3초, 즉시 스킵), 버튼은 "다음" 하나, 저장/통신 대기 없음. 배지 스탬프는 연출 끝.
- [ ] 증거: 결과 연출 영상(스킵 포함) → 사람 확인.

### Task S5: 랭킹 화면·공유 자리 [장면 | FR-023·026, SCR-006]
- [ ] 상단(내 뇌 최고·최고 도달·공식 순위 또는 기준표 등급+배지)/하단(주변 순위 또는 기준표), 다시 연주/타이틀, 로딩·보류·거부·연습 구분 표시. 공유는 placeholder 동선(실패해도 이동 안 막음).
- [ ] 증거: N=0(기준표)·스텁 N=20(공식) 두 상태 스크린샷 → 사람 확인.

### Task S6: 감성 연출·움직임 줄이기 [장면 | FR-028]
- [ ] HUD "n번째 무대"+전구 줄, 대기 일렁임·공기 연출 최소 1종, 움직임 줄이기 설정 시 고정 표시 대체. 어떤 연출도 판정 카드·악보·현재박을 가리지 않음.
- [ ] 증거: 연출 켬/줄임 비교 영상 → 사람 확인.

---

## 에셋 요청 (7단계, 사람 생성 대기)

asset-plan.md 기준으로 `asset-requests/manifest.json`+`request.md` 작성: 친구 기본 그림 4종(slot: `unity/Assets/Mallang/RR02/Art/friend-<id>.png`), 누름 반응, 뇌 그림(단계 4종), 배경 1종. 정체성 음 4종은 소리 후보 요청서(별도 — 이미지 아님). 구현은 placeholder로 끝까지 조립되고 실제 에셋 교체가 테스트를 깨지 않아야 합니다 (필수).

## 미확인 목록 (완료 보고로 승계)

- 실기기 소리·터치·판정 체감(장면 레인 전체) / FR-026 WebGL 영상·GIF 저장 기술 확인(ASM-003) / 실서버 랭킹(ASM-002) / 시각 확인 19건 pending·design.assets 공백(ASM-006) / FR-019 음원 선정(사람 청취 PT).
