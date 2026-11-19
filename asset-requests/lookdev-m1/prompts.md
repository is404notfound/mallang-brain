# M1 룩뎁·소품 프롬프트 팩 (스타일 카드 v5 기준 · 2026-11-19 개정)

스타일 카드 v5(클레이 디오라마판, 승인 2026-11-19)를 기준으로 하는 생성 프롬프트 모음입니다. 생성은 사람이 하고, 선택본은 지정 파일명으로 저장합니다. 탈락 변형은 `-alt1`, `-alt2`로 보관합니다 (증거).

## 공통 생성 설정 (Krea)

| 설정 | 값 |
| --- | --- |
| Model | Krea 2 Medium |
| Aspect Ratio | 1:1 |
| Resolution | 1K |
| Creativity | Raw |
| Moodboard | Malang Brain M1 v2 (`daeeb5d1-55ec-4b51-bc47-5d166693d550`) |
| Moodboard Strength | 0.22~0.28 |

공통 규칙:
- **공통 네거티브**: photorealistic, realistic fabric/fur, sharp metallic details, extra objects, text, letters, logos, watermark, hands, characters
- 세 소품은 각각 따로 생성해도 **같은 조명 방향·그림자 농도·무광 클레이 질감**이어야 합니다 (아래 QA 표로 검수).
- 생성물은 목표 룩 참고와 3D 모델링 레퍼런스용입니다. 전체 화면 완성컷 생성은 금지 (필수).

---

## Part A. 룩뎁 3장 — 완료 (2026-11-17~19 승인)

LKD-001(토끼 콘셉트)·LKD-002(턴어라운드)·LKD-003(축제 무드보드)는 승인되어 스타일 카드 v5의 캔버스로 승격되었습니다. 산출물: `~/Desktop/schedule/lookdev/m1-cast.png`, `m1-character-turnaround.png`, `m1-play.png`. 이 절의 구버전 프롬프트는 git 이력(커밋 224eb3b)에 있습니다.

---

## Part B. 소품 3종 (PROP-001~003)

생성 순서 추천: ① 학사모(세계관 소품 질감 확인) → ② 실로폰(다색 오브젝트 통일성 확인) → ③ 입력 카드(게임 UI 재질·형태 확정).

### PROP-001 · 학사모 (graduation-cap)

- 저장: `asset-requests/_inbox/PROP-001-graduation-cap.png` (3D 소품 모델링 레퍼런스)
- 디자인 결정(2026-11-20, v5.2): **먹색(따뜻한 차콜) + 버터 옐로 술로 회귀** — 라벤더 안 취소. 1차 생성본의 형태·색은 승인, 크라운의 얼룩 질감만 3D에서 깨끗한 무광 클레이로 교정합니다 (재생성 불필요).
- 체크 포인트: 네모판이 너무 납작하거나 뾰족하면 안 됩니다. 동물 머리에 얹었을 때 살짝 폭신한 느낌 (필수). 재생성이 필요해지면 아래 프롬프트의 색 지시를 사용: "deep warm charcoal (soft near-black ink color) for the entire cap, clean smooth matte polymer clay surface with NO mottled, leathery or camouflage texture, delicate buttery-yellow tassel".

```text
A single beautifully crafted miniature graduation cap prop for an original cozy casual game called "Malang Brain Music Academy".

DESIGN:
A charming, compact graduation cap designed for tiny round animal students. The square mortarboard has softly rounded corners, gently puffed edges and a slightly cushioned appearance rather than sharp geometric edges. The cap base is a small rounded fabric-like crown that gives the object a soft, wearable silhouette.

Use a sophisticated pastel color combination: muted lavender for the main cap, warm creamy ivory for the lower band, and a delicate buttery-yellow tassel. The tassel hangs naturally from one corner, with a tiny rounded decorative end.

ART DIRECTION:
Soft handcrafted miniature 3D game asset, smooth matte polymer clay and painted resin materials, rounded sculptural forms, subtle tactile surface, gentle ambient occlusion, soft studio lighting, delicate contact shadow.

The design should feel like a premium collectible miniature belonging to the same world as adorable chubby clay animal musicians. Cute, elegant, warm, youthful and carefully designed.

COMPOSITION:
One complete graduation cap only, isolated and centered, viewed from a slightly elevated three-quarter angle. Show the top board, soft crown and hanging tassel clearly. Keep the entire object inside the frame with comfortable margins.

Plain warm ivory studio background. No characters, no mannequin, no head, no text, no letters, no logos, no watermark, no additional props, no photorealistic fabric, no sharp metallic details.
```

### PROP-002 · 실로폰 (xylophone)

- 저장: `asset-requests/_inbox/PROP-002-xylophone.png` (토끼 악기 — 3D 소품 레퍼런스)
- 체크 포인트: 음판 7개가 무지개색 한 판으로 합쳐지지 않고 **각각 독립 막대**여야 합니다 (필수) — Unity에서 음판별 반응 애니메이션을 넣기 위한 조건입니다.

```text
A single adorable miniature xylophone instrument prop for an original cozy casual game called "Malang Brain Music Academy".

DESIGN:
A small, beautifully designed wooden toy xylophone with seven individual rounded rectangular tone bars arranged in a clear ascending musical scale.

Each tone bar has a softly beveled edge, a slightly domed surface and a distinct pastel color. Use a harmonious palette of creamy yellow, peach pink, soft coral, powder blue, mint green, pale lavender and warm ivory.

The bars are mounted on a compact warm maple-colored wooden base with gently rounded edges. The base should look handcrafted and sturdy, with simple curved supports that keep the colorful bars visibly separated.

Include exactly two small matching mallets placed neatly beside the xylophone. Each mallet has a slim rounded handle and a soft spherical striking head in warm cream.

ART DIRECTION:
Soft handcrafted miniature 3D game asset, smooth matte polymer clay and painted wood materials, rounded sculptural forms, subtle tactile finish, gentle ambient occlusion, soft studio lighting and delicate contact shadows.

The xylophone must feel like a collectible miniature musical instrument from the same world as cute chubby clay animal musicians. Refined pastel styling, playful but not overly childish, charming and visually readable.

COMPOSITION:
One complete xylophone with two mallets, isolated and centered, viewed from a slightly elevated three-quarter angle. All seven tone bars must be individually visible and clearly separated. The instrument should occupy most of the frame without cropping.

Plain warm ivory studio background. No characters, no hands, no musical notes floating in the air, no sound waves, no text, no labels, no logos, no watermark, no extra instruments, no photorealistic metal.
```

### PROP-003 · 멜로디 입력 카드 (input-card-material)

- 저장: `asset-requests/_inbox/PROP-003-input-card.png` (플레이 화면 카드 재질·형태 기준)
- **가장 중요한 에셋**: 장식 카드가 아니라 플레이어가 멜로디를 기억해 직접 탭하는 악보의 한 칸(8칸 중 1칸)입니다.
- 체크 포인트: 중앙은 비워 둡니다 (필수) — 캐릭터 음색 표시·커서 활성 효과·눌림 애니메이션이 들어갈 자리입니다. **파란 O·분홍 X·커서·발광·눌림 상태는 이 에셋에 넣지 않고 Unity 런타임 상태로 구현합니다** (스타일 카드 v5.1). 그래야 8장이 같은 규칙으로 작동합니다.

```text
A single reusable melody input card tile for the core rhythm gameplay of an original cozy casual game called "Malang Brain Music Academy".

GAMEPLAY CONTEXT:
This card is one of eight identical reusable tiles arranged in a 2-column by 4-row grid during gameplay. During the listening phase, a musical cursor travels across the cards in melody order, showing which animal friend's sound occurs at each beat. During the player's repeat phase, the player taps the corresponding card in rhythm. Correct and incorrect inputs are indicated by separate blue O and pink X feedback overlays.

The card itself represents one position in the musical score. It must be instantly recognizable as a tactile, tappable game tile.

DESIGN:
Create ONE standalone square melody input card with generously rounded corners, a softly raised cushioned surface and a subtle beveled edge.

The overall silhouette should be a rounded square, with balanced proportions that remain clear and readable when eight copies are arranged in a compact 2x4 grid on a mobile screen.

Use a warm creamy ivory base color, with very subtle pastel lavender undertones. The surface should feel like smooth matte polymer clay combined with thick premium textured paper. The edge should have a slightly visible thickness, creating the impression of a small physical tile rather than a flat vector button.

The center should remain visually clean and mostly empty, leaving enough negative space for future musical symbols, cursor highlights and interaction feedback.

Add only extremely subtle surface detailing, such as a faint inset border or a delicate embossed rim. Avoid decorative illustrations that compete with gameplay readability.

ART DIRECTION:
Soft handcrafted miniature 3D game UI asset, smooth matte polymer clay, tactile premium paper-like surface, rounded sculptural geometry, gentle ambient occlusion, soft studio lighting, subtle contact shadow.

Match the exact visual language of the pastel clay animal musicians and miniature props from "Malang Brain Music Academy". Warm ivory, pale lavender, muted mint and delicate peach color harmony. Cute, calm, polished and highly readable.

COMPOSITION:
One single card tile only, viewed from a slightly elevated three-quarter angle. Show the entire square card and its thickness. Center it with comfortable margins.

Plain warm ivory studio background.

IMPORTANT:
This is a reusable gameplay tile, NOT a complete interface screen. Do not generate eight cards, a grid, a music keyboard, a piano, a musical staff, or a full mobile UI.

Do not include O or X symbols, numbers, letters, musical notation, character portraits, animal icons, cursor arrows, hands, touch indicators, text, logos, watermarks or extra objects.
```

---

## 소품 일관성 QA (생성 결과 검수표)

| 항목 | 학사모 | 실로폰 | 입력 카드 |
| --- | --- | --- | --- |
| 둥근 실루엣 | ☐ | ☐ | ☐ |
| 무광 클레이 질감 | ☐ | ☐ | ☐ |
| 파스텔 팔레트 (v5) | ☐ | ☐ | ☐ |
| 3/4 시점·조명 방향 동일 | ☐ | ☐ | ☐ |
| 단일 오브젝트 (화면·그리드 아님) | ☐ | ☐ | ☐ |
| 게임 내 기능 명확성 | ☐ | ☐ | ☐ |

추가 확인: 그림자 농도·재질 부드러움이 세 장에서 비슷한가 / 사실적이거나 평면 일러스트로 흐르지 않았는가 / 캐스트(m1-cast.png)와 같은 세계로 보이는가.

## manifest (파일명 → 자리)

| ID | 저장 파일명 | 자리(용도) |
| --- | --- | --- |
| LKD 선택본 3장 | `lookdev/m1-*.png` | 캔버스 (승인 완료) |
| PROP-001 | `_inbox/PROP-001-graduation-cap.png` | 학사모 3D 소품 모델링 레퍼런스 |
| PROP-002 | `_inbox/PROP-002-xylophone.png` | 토끼 악기 3D 소품 레퍼런스 |
| PROP-003 | `_inbox/PROP-003-input-card.png` | 플레이 화면 카드 재질·형태 기준 (상태 연출은 Unity) |
