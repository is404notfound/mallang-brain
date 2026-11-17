# M1 룩뎁 프롬프트 팩 (스타일 카드 v4 기준 · 2026-11-17)

스타일 카드 v4(승인됨, 외곽선 있는 툰)를 기준으로 만든 룩뎁용 프롬프트 3종입니다. 생성은 사람이 하고, 선택본은 지정 파일명으로 `~/Desktop/schedule/lookdev/`에 저장하면 프로젝트 보드 갤러리에 자동 표시됩니다. 탈락 변형은 `-alt1`, `-alt2`로 같은 폴더에 보관합니다 (증거).

공통 규칙:
- **레퍼런스 첨부** (필수): 캐릭터 프롬프트에는 `art-direction/references/charactors.png`와 `asset-requests/_inbox/AST-001-friend-rabbit.png`(토끼 낱장)를 image→image로 첨부합니다. 무드보드에는 `art-direction/references/screen-play-final.png`를 팔레트 참고로 첨부합니다.
- **공통 네거티브**: photorealistic, realistic fur texture, plastic glossy skin, extra limbs, human fingers, angry eyebrows, multiple eyelashes, large white sclera, hard cel-shading edges, text, watermark, UI elements
- 이 이미지들은 목표 룩 참고용입니다. 게임 화면에 잘라 넣지 않습니다 (필수).

---

## LKD-001 · 캐릭터 1호(토끼) 3D 목표 룩 — 워크플로 1~2단계

- 저장: 선택본 → `lookdev/m1-character.png`
- 감성 목표: 따뜻함 + 매사에 열심인 설렘 ("연주 직전, 말렛을 꼭 쥔 한 순간")
- 진행: 아래 프롬프트를 **Krea 2 계열과 Nano Banana 계열 두 모델에 같이** 넣어 4장씩 일괄 생성 → 실루엣 기준 1장 선택 → 인페인팅으로 귀·나비넥타이·말렛만 보정. 모델별 차이를 체험 로그 wf-1에 기록합니다.

**본 프롬프트 (텍스트 생성용 — Krea 2 등):**

> A cute chubby cream-white lop-eared rabbit character rendered as a soft matte 3D toon figurine, in the style of a cozy life-sim game. Rounded mallow-soft body, head-to-body ratio 1:1, short stubby arms without fingers, droopy long rounded ears with soft pink inner ear, black dot eyes with a single white highlight each, tiny w-shaped mouth, three small red blush strokes on each cheek, dark charcoal graduation cap with a yellow tassel, coral bow tie, holding two small xylophone mallets close to its chest, leaning slightly forward with earnest sparkling anticipation — the moment just before playing. Thin soft dark-brown outline around the silhouette, matte 2–3 step toon shading (no hard cel edges, no plastic gloss). Lighting: cool navy-violet dusk ambience with a warm golden key light as if lit by festival string lights, gentle warm rim light. Plain soft gradient background (pale lavender-grey), full body, centered, slight 3/4 front angle.

**편집 프롬프트 (참조 편집용 — Nano Banana 계열, 토끼 낱장 첨부):**

> Keep this exact character design, proportions and face. Re-render it as a soft matte 3D toon figurine (cozy life-sim game style): volumetric rounded body, matte 2–3 step toon shading, thin soft dark-brown outline, warm golden key light with cool navy-violet dusk ambience and a gentle rim light. Plain pale lavender-grey background, full body, slight 3/4 front angle. Do not change the ears, blush strokes, graduation cap, bow tie, or xylophone mallets.

**변형 지시**: V1 그대로 / V2 연주 직전 대신 "수줍게 웃는 정면" / V3 림 라이트를 더 강하게(저녁 무드 강조). 변수는 한 번에 하나만 바꿉니다.

---

## LKD-002 · 토끼 3면 턴어라운드 — 워크플로 3단계

- 저장: `lookdev/m1-character-turnaround.png` (Tripo 입력용 원본은 배경 제거 후 `asset-requests/_inbox/`에)
- 진행: LKD-001 선택본을 첨부하고 참조 편집 모델로 생성 → 배경 제거 → 흰 테두리·그림자 잘림 확인(wf-4 기록).

**프롬프트 (LKD-001 선택본 첨부):**

> Character turnaround sheet of this exact rabbit figurine: three views side by side — front view, side view, back view. Same character, same proportions, same soft matte 3D toon shading and thin dark-brown outline in every view. Neutral standing pose, arms relaxed, ears hanging naturally, graduation cap and bow tie visible in all views, tail visible in back view. Flat even lighting, plain solid pale grey background, no ground shadow, no text or labels. Consistent scale and feet position across all three views.

**검수 포인트** (필수): 측면·후면에서 귀 길이와 몸 비율이 정면과 같은가, 뒷모습에 모자 술·꼬리가 있는가. 오추정은 인페인팅으로 수정합니다.

---

## LKD-003 · 플레이 화면 무드보드 — 분위기·조명·재질 참고 전용

- 저장: `lookdev/m1-play.png`
- 감성 목표: 아늑한 저녁 축제의 평화로움 ("따뜻한 빛 웅덩이 속에서 음악이 시작되기 직전")
- 주의 (필수): UI·글자·캐릭터 클로즈업 없이 **공간의 분위기만**. 이 그림을 게임 배경으로 잘라 쓰지 않습니다. 1:1로 생성 후 아웃페인팅으로 16:9 확장해 보고 wf-5에 기록합니다.

**프롬프트 (screen-play-final.png를 팔레트 참고로 첨부):**

> Mood reference of a small cozy evening campus festival plaza, rendered as a soft 3D toon diorama (cozy life-sim game look). Deep navy-violet dusk sky gradient, warm golden string lights hanging above and glowing softly, cream-colored clock tower building softly blurred in the background, striped pastel festival stall awnings on one side, small wooden stage with warm spotlights on the other, grey-violet tiled ground reflecting warm pools of light, a few floating light particles and drifting cherry-blossom petals. Soft matte materials, gentle depth of field, no characters in focus, no UI, no text. Peaceful, warm, about-to-begin atmosphere. Square 1:1 composition with the empty plaza center kept clear.

**변형 지시**: V1 그대로 / V2 카메라를 약간 낮춰 전구 스트링이 화면 위를 가로지르게 / V3 황혼을 10% 더 밝게(접근성 확인용).

---

## manifest (파일명 → 자리)

| 생성물 | 저장 파일명 | 자리(용도) |
|---|---|---|
| LKD-001 선택본 | `m1-character.png` | plan.html 갤러리 · 3D 모델링 목표 룩 |
| LKD-001 탈락 변형 | `m1-character-alt1.png`, `-alt2.png` | 선택 과정 증거 |
| LKD-002 3면 시트 | `m1-character-turnaround.png` | Tripo 입력 · 갤러리 |
| LKD-003 무드보드 | `m1-play.png` | plan.html 갤러리 · 조명/재질 참고 |
