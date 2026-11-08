# 에셋 리스트 입력 스키마 (2단계)

2단계에 넘기는 에셋 리스트의 형식입니다. 화면·기능 단위로 모아서 줍니다.

## 항목 형식

각 에셋은 다음을 가집니다.

- **id** (`AST-001` 등): 고유 식별자.
- **name** (`friend-rabbit`): 파일명·slot에 쓰는 짧은 영문 이름.
- **slot**: 이 에셋이 놓일 대상 경로(작업 저장소 기준). **코드 경로든 문서/스토리보드 경로든 됩니다.** 예: 코드 slot `unity/Assets/Mallang/RR02/Art/friend-rabbit.png`, 스토리보드 slot `docs/design/storyboard/screen-title.png`.
- **purpose**: 용도 한 줄(어느 화면·어떤 역할).
- **changeFrequency** (`high`|`low`): 자주 바뀌는지. **변형 생성 여부를 가릅니다** — high는 2~3안, low는 단발(아래 2단계 참고).
- **sceneType** (`character`|`scene`|`scene-with-subject`|`ui-icon`): 어떤 구도 규칙을 붙일지 결정합니다.
- **referenceImage** (선택): image→image 스타일 레퍼런스로 붙일 승인 이미지 경로. 없으면 생략.
- **크기·비율 사양(중요)**: 엔진은 slot 크기를 레이아웃이 정하고 스프라이트는 Image의 **Preserve Aspect**로 비율 유지해 맞추므로, 픽셀 정확은 불필요하고 **비율(aspect)·투명 여부·해상도 하한**이 중요합니다. sceneType별 기본: character/ui-icon = 1:1 정사각·투명 PNG·≥1024px(아이콘은 ≥512), scene = 화면 비율(예 16:9)·불투명·≥기준해상도. **slot 비율이 애매하면 플레이스홀더를 먼저 배치해 slot 비율을 재서 프롬프트에 박습니다.** 배치 시 Image는 Preserve Aspect를 켜고 투명 PNG·pivot center로 import합니다.

## sceneType별 구도 규칙 매핑

프롬프트에 붙일 구도 지침을 sceneType으로 고릅니다(스타일 카드 3절에서 가져옴).

- **character**: 단일 캐릭터 중심. 투명 배경, 캐릭터 규격(비율·외곽선·얼굴)과 포인트색 적용. 배경 겹 없음.
- **scene**: 배경/중경/전경 최소 3겹 + 깊이 단서 2가지. "한 장면·한 감정" 초점. 명도·채도 위계(배경 후퇴). 특정 캐릭터가 주인공이 아닌 장면.
- **scene-with-subject**: scene의 3겹·깊이·위계를 모두 적용하되, **한 캐릭터(또는 1~2마리)가 전경의 주인공**인 장면(결과·타이틀 화면 등). 캐릭터 규격(비율·외곽선·얼굴·포인트색)과 scene 구도를 함께 적용합니다.
- **ui-icon**: 단순 기호. 재질 언어(젤리·버블 등) 적용, 여백 확보, 숨긴 정답 노출 금지 규칙 준수.

## 프레임 세트 (폴리시 단계)

프로토타입 초기엔 상태당 **단일 스틸**(idle 1장, hit 1장)이면 충분하지만, 상용 수준 폴리시 단계에선 **프레임 세트**(한 상태의 애니메이션 프레임 여러 장)가 필요합니다. 품질 격차 루프(game-build 6-폴리시 / playbook C4)가 돌 때마다 리스트에 프레임 세트를 추가합니다.

- 프레임 세트 항목은 `frames`로 표현합니다: 같은 캐릭터·상태의 연속 프레임 N장. 예:
  ```json
  { "id": "AST-001-idle", "name": "friend-rabbit-idle", "sceneType": "character",
    "frames": 4, "purpose": "토끼 대기 숨쉬기 루프", "referenceImage": "art-direction/references/style-exemplar-rabbit.png",
    "slot": "unity/Assets/Mallang/RR02/Art/anim/rabbit-idle_{n}.png" }
  ```
  slot의 `{n}`은 1..frames로 치환됩니다(파일명·배치도 프레임별). 프롬프트는 "1번과 같은 캐릭터로, 숨쉬기 n/N 프레임(살짝 눌림/퍼짐)"처럼 **프레임 간 미세 변화**를 지시하고, 모든 프레임에 같은 스타일 견본을 첨부합니다(일관성).
- 자주 필요한 세트: 대기(숨쉬기), 타격/연주, 성공(기뻐함), 실패/미스, 전환. 필요 프레임 수는 격차 분석에서 정합니다.

## 예시

```json
[
  { "id": "AST-001", "name": "friend-rabbit", "slot": "unity/Assets/Mallang/RR02/Art/friend-rabbit.png",
    "purpose": "플레이 좌우 카드·악보·결과의 토끼 기본 자세", "changeFrequency": "high",
    "sceneType": "character", "referenceImage": "docs/design/.../rabbit-ref.png" },
  { "id": "AST-013", "name": "stage-background", "slot": "unity/Assets/Mallang/RR02/Art/stage-background.png",
    "purpose": "타이틀·플레이·결과 공용 무대 배경", "changeFrequency": "low",
    "sceneType": "scene" },
  { "id": "SB-001", "name": "storyboard-title", "slot": "docs/design/storyboard/screen-title.png",
    "purpose": "스토리보드에 넣을 타이틀 화면 한 장(전체 구성)", "changeFrequency": "high",
    "sceneType": "scene-with-subject", "referenceImage": "art-direction/references/screen-title-final.png" }
]
```

> 스토리보드/화면 이미지 메모: 전체 화면 한 장은 `sceneType`을 `scene`(캐릭터 비주인공) 또는 `scene-with-subject`(캐릭터 주인공)로 두고, `slot`을 스토리보드/문서 경로로 둡니다. 승인 캔버스가 있으면 `referenceImage`로 붙여 미감을 고정합니다. `changeFrequency: high`로 두면 2~3안이 나와 고를 수 있습니다. 배치는 동일하게 `place_by_name.py`가 slot(문서 경로)으로 복사합니다.
