# 에셋 프롬프트 생성 스킬 구현 계획

> **For agentic workers:** 이 계획은 superpowers:executing-plans(또는 subagent-driven-development)로 작업별로 구현합니다. 체크박스(`- [ ]`)로 진행을 추적합니다. 스킬 문서형 작업은 TDD 대신 "작성 → 리뷰 검증"을 쓰고, 배치 스크립트만 TDD를 씁니다.

**Goal:** 디자인 문서의 미감 DNA를 고품질 GPT 이미지 프롬프트로 바꾸는 2단계 로컬 스킬을 만들고, 첫 말랑브레인 style-card를 산출한다.

**Architecture:** palolo `.claude/skills/asset-prompts/`에 SKILL.md + 레퍼런스 문서(스타일 카드 템플릿·프롬프트 팩 템플릿) + 헬퍼 스크립트(낱장 파일명→slot 배치)를 둔다. 스킬은 1단계(디자인 문서→검토 가능한 style-card, 사람 승인에서 멈춤)와 2단계(승인된 카드+에셋 리스트→prompts.md·manifest·배치)로 나뉜다.

**Tech Stack:** Markdown(SKILL.md·템플릿), Python 3 + Pillow(배치 스크립트·pytest), 기존 `split_spritesheet.py`는 참고만 하고 수정하지 않음.

**Spec:** `docs/superpowers/specs/2026-11-08-asset-prompt-skill-design.md`

## Global Constraints

- 스킬 위치: `palolo/.claude/skills/asset-prompts/`. 버전 고정 plan-handoff 플러그인 캐시는 수정하지 않는다 (필수).
- 디자인 문서는 `/Users/easyoon/mallang-brain`(feature/game-planning 워크트리)에서 **읽기만**. 산출물(style-card·asset-requests·placed.json)은 palolo에 커밋.
- style-card는 프로젝트 전역 1개: `palolo/art-direction/style-card.md`.
- 색은 정성적으로 지정하고, 미승인 HEX 후보는 "미승인"으로 분리 표기한다 (디자인.md 5절 "수치 토큰 미추출").
- 에셋은 교체 가능 리소스: slot에 실제 에셋을 끼워도 구현/테스트가 깨지지 않는다.
- GPT는 낱장 생성 전제. 균일 격자 시트를 강제하지 않는다. 새 흐름은 독자 manifest를 쓰고 `split_spritesheet.py`와 합치지 않는다.
- 한 스킬, 두 단계. 1단계 뒤 사람 승인에서 멈춘다. 에셋마다 변형 2~3개를 요청한다.

## Review Focus

이 스킬/스크립트가 마주칠 입력 중 테스트로 덮지 않으면 사람을 무는 것들. 각 줄의 테스트를 소유 작업에 박았다.

1. 돌려받은 파일이 manifest의 어느 항목과도 안 맞음 → 조용히 건너뛰지 말고 "미매칭 파일" 보고. → Task 1
2. slot에 이미 실제(placeholder 아님) 에셋이 있음 → `--force` 없이 덮지 않음. → Task 1
3. 에셋 리스트가 가리키는 referenceImage가 없음 → 2단계가 깨진 프롬프트를 내지 말고 경고. → Task 4
4. 디자인 문서 경로가 없거나 못 읽음 → 1단계가 빈 카드를 내지 말고 명확히 멈춤. → Task 3
5. 변형 3개 중 사람이 하나를 고름 → 고른 변형 파일이 slot으로 정확히 매핑. → Task 1·Task 4

---

### Task 1: 낱장 배치 스크립트 (place_by_name.py) [TDD]

**Files:**
- Create: `.claude/skills/asset-prompts/scripts/place_by_name.py`
- Test: `.claude/skills/asset-prompts/scripts/test_place_by_name.py`

**Interfaces (Produces):**
```
python3 place_by_name.py <manifest.json> [--force] [--dry-run]
```
manifest 스키마:
```json
{
  "inbox": "asset-requests/_inbox",
  "assets": [
    { "id": "AST-001", "name": "friend-rabbit",
      "slot": "unity/Assets/Mallang/RR02/Art/friend-rabbit.png",
      "expectedFile": "AST-001-friend-rabbit.png", "variations": 3, "chosen": null }
  ]
}
```
동작: 각 asset에 대해 `chosen`이 있으면 그 파일명을, 없으면 `expectedFile`을 `<inbox>`에서 찾아 `slot`으로 복사. 결과를 manifest 폴더 옆 `placed.json`에 기록. slot이 이미 있고 `--force`가 없으면 건너뛰고 경고. inbox에 있으나 어느 asset과도 안 맞는 파일, 어느 파일과도 안 맞는 asset을 각각 "unmatched" 목록으로 출력. 종료 코드: 전부 배치 성공 0, 미매칭/건너뜀이 있으면 1.

- [ ] **Step 1: 실패 테스트 작성** (`test_place_by_name.py`, pytest + PIL로 더미 PNG 생성)
  - `test_places_expected_file_to_slot`: inbox에 expectedFile을 두면 slot에 복사되고 placed.json에 기록(Review Focus 없음).
  - `test_chosen_variant_overrides_expected`: `chosen`이 set이면 그 파일을 slot으로(RF5).
  - `test_unmatched_returned_file_is_reported`: 어느 asset과도 안 맞는 inbox 파일이 unmatched로 출력되고 종료 1(RF1).
  - `test_missing_returned_file_is_reported`: asset의 파일이 inbox에 없으면 unmatched asset으로 출력되고 종료 1(RF1).
  - `test_existing_slot_not_overwritten_without_force`: slot에 파일이 이미 있으면 건너뛰고 경고, `--force`면 덮어씀(RF2).
  - `test_dry_run_writes_nothing`: `--dry-run`은 slot·placed.json을 만들지 않고 계획만 출력.
- [ ] **Step 2: 테스트 실행해 실패 확인** — `cd .claude/skills/asset-prompts/scripts && python3 -m pytest test_place_by_name.py -v` → ModuleNotFound/함수 없음으로 FAIL.
- [ ] **Step 3: place_by_name.py 구현** — argparse, manifest 로드, inbox/slot 경로는 manifest 위치 기준 상대 해석. placeholder 판단은 "slot 파일 존재 여부"만 본다(내용 분석 없음, --force로 덮기). Pillow는 PNG 유효성 확인에만 선택적으로 사용.
- [ ] **Step 4: 테스트 통과 확인** — 같은 pytest 명령 → PASS.
- [ ] **Step 5: 커밋** — `git add .claude/skills/asset-prompts/scripts && git commit -m "feat(asset-prompts): 낱장 파일명→slot 배치 스크립트"`

### Task 2: 스킬 뼈대 (SKILL.md) [리뷰 검증]

**Files:**
- Create: `.claude/skills/asset-prompts/SKILL.md`

**Interfaces (Produces):** 스킬 이름 `asset-prompts`. 2단계 절차(1단계 style-card 증류→승인 게이트→2단계 프롬프트 팩·배치)와 입력(디자인 문서 경로, 에셋 리스트), 산출물 경로를 명시.

- [ ] **Step 1: SKILL.md 작성** — frontmatter(name: asset-prompts, description: 전역 규칙 준수 한국어). 본문: 개요 → 사용법 → 기본 원칙(패키지/디자인 문서 읽기 전용, 에셋 교체 가능, 색 정성적+미승인 분리) → 실행 절차(`### 1단계: 스타일 카드 증류`, `### 2단계: 프롬프트 팩 컴파일과 배치`, 사이에 사람 승인 게이트) → 주의사항 → 용어 설명. CLAUDE.md 글쓰기 규칙 준수.
- [ ] **Step 2: 리뷰 검증** — SKILL.md가 설계 문서의 2단계·제약·산출물 경로와 어긋나지 않는지 자기 점검(게시 전 점검 목록). 승인 게이트가 "사람 승인 전 2단계 금지"로 명시됐는지 확인.
- [ ] **Step 3: 커밋** — `git add .claude/skills/asset-prompts/SKILL.md && git commit -m "feat(asset-prompts): 스킬 뼈대와 2단계 절차"`

### Task 3: 1단계 레퍼런스 — 스타일 카드 템플릿·증류 절차 [리뷰 검증]

**Files:**
- Create: `.claude/skills/asset-prompts/references/style-card-template.md`
- Create: `.claude/skills/asset-prompts/references/distill-style-card.md`

**Interfaces (Consumes):** 디자인 문서 경로 목록(기본값: `/Users/easyoon/mallang-brain/디자인.md`, `.../docs/design/title-redesign-2026-11-07/`, 해당 spec 패키지). **Produces:** `art-direction/style-card.md`의 7개 섹션 양식(공통 스타일 문단 / 팔레트 / 구도·위계 / 캐릭터 시트 / 재질 언어 / 금지사항 / 레퍼런스 이미지 매핑).

- [ ] **Step 1: style-card-template.md 작성** — 7개 섹션의 빈 양식과 각 섹션 작성 지침. 색 섹션은 "정성적 규칙 + 미승인 HEX 후보(분리)" 두 칸으로 나눔.
- [ ] **Step 2: distill-style-card.md 작성** — 증류 절차: 문서 경로를 받아 읽고, 각 규칙에 출처 파일명을 달고, 못 찾은 항목은 "문서에서 못 찾음"으로 정직 표기, 추정은 "추정:"으로 구분. **디자인 문서 경로가 없거나 못 읽으면 빈 카드 대신 멈추고 경로 오류를 보고한다**(RF4). 증류는 세션(상위 모델) 판단 작업이라 약한 모델에 맡기지 않는다고 명시.
- [ ] **Step 3: 리뷰 검증** — 템플릿 7섹션이 설계 문서와 일치, RF4(경로 오류 시 멈춤) 지침이 distill 문서에 있는지 확인.
- [ ] **Step 4: 커밋** — `git add .claude/skills/asset-prompts/references && git commit -m "feat(asset-prompts): 1단계 스타일 카드 템플릿·증류 절차"`

### Task 4: 2단계 레퍼런스 — 프롬프트 팩 템플릿·에셋 리스트 스키마 [리뷰 검증]

**Files:**
- Create: `.claude/skills/asset-prompts/references/prompt-pack-template.md`
- Create: `.claude/skills/asset-prompts/references/asset-list-schema.md`

**Interfaces (Consumes):** 승인된 `style-card.md` + 에셋 리스트. 에셋 항목: `id`, `name`, `slot`, `purpose`, `changeFrequency`, `sceneType`(character|scene|ui-icon), 선택 `referenceImage`. **Produces:** `asset-requests/prompts.md`(에셋별 프롬프트) + `asset-requests/manifest.json`(Task 1 스키마와 동일, `expectedFile`·`variations`·`chosen` 포함).

- [ ] **Step 1: asset-list-schema.md 작성** — 입력 항목 정의와 예시. sceneType 3종(character/scene/ui-icon)별로 어떤 구도 규칙을 붙일지 매핑.
- [ ] **Step 2: prompt-pack-template.md 작성** — 각 에셋 프롬프트 = 공통 스타일(카드 1절) + 개별 묘사 + sceneType 구도 규칙(카드 3절) + **referenceImage 지목(있으면)** + **변형 2~3개 지시**. `referenceImage`가 지정됐는데 파일이 없으면 프롬프트에 넣지 말고 경고 목록에 올린다(RF3). manifest의 `expectedFile`은 `<id>-<name>.png`, 변형은 `<id>-<name>-v1.png` 식으로 규칙화(RF5). GPT 실행 안내(한 번에 하나씩, 돌려받은 파일 이름 규칙, 3안 중 하나 고르기)를 포함.
- [ ] **Step 3: 리뷰 검증** — 템플릿의 manifest 필드가 Task 1 place_by_name.py 스키마와 정확히 일치하는지 대조(expectedFile·chosen·slot·inbox). RF3·RF5 지침 존재 확인.
- [ ] **Step 4: 커밋** — `git add .claude/skills/asset-prompts/references && git commit -m "feat(asset-prompts): 2단계 프롬프트 팩 템플릿·에셋 리스트 스키마"`

### Task 5: 체크포인트 — 모델링 재분석·대안 리뷰·사용자 확인·첫 카드 생성 [프로세스]

구현(Task 1~4)이 끝난 뒤의 검토·산출 단계. 코드 작업이 아니라 판단·승인 단계다.

- [ ] **Step 1: 스킬 모델링 전반 재분석** — 완성된 SKILL.md·템플릿·스크립트를 묶어, 이 스킬이 디자인 DNA를 프롬프트로 옮기는 방식(섹션 구조, sceneType 분류, 변형 처리, 배치 계약)을 다시 읽는다.
- [ ] **Step 2: 대안 2~3개 + 반박 리뷰** — stage-review 방식으로, 모델링의 핵심 결정(예: 스타일 카드 섹션 구성, sceneType 체계, 변형 네이밍·선택 방식)마다 대안 2~3개를 내고 각 대안이 실패하는 상황을 적어 표로 제시한다.
- [ ] **Step 3: 사용자 확인** — 리뷰 결과를 보여주고 수정/승인을 받는다. 승인 전에는 다음 단계로 가지 않는다 (필수).
- [ ] **Step 4: 첫 말랑브레인 style-card 생성** — 승인된 모델링으로 1단계를 실제 실행한다. 디자인.md(30절)·title-redesign 문서를 읽어 `art-direction/style-card.md`를 증류하고, 사람 승인 대기 상태로 둔다.
- [ ] **Step 5: 커밋** — `git add art-direction/style-card.md && git commit -m "feat(asset-prompts): 첫 말랑브레인 스타일 카드(승인 대기)"`

## 자기 점검 결과

- **스펙 커버리지**: 설계의 1단계=Task 2·3, 2단계=Task 2·4, 배치=Task 1, 첫 카드 생성=Task 5. 제약(전역 카드·읽기/쓰기 레포 분리·색 정성적·격자 비통합)은 Global Constraints와 각 Task에 반영.
- **타입 일관성**: manifest 스키마(expectedFile·chosen·slot·inbox)를 Task 1에서 정의하고 Task 4에서 동일 필드로 생성하도록 Step 3 대조를 넣음.
- **Review Focus**: 5개 모두 소유 Task의 테스트/지침에 박음(1·2·5→Task 1, 3→Task 4, 4→Task 3).
- **비례**: 설계 문서 대비 과하지 않게 — 문서형 Task는 양식·지침만, 코드 Task만 테스트 상세.
