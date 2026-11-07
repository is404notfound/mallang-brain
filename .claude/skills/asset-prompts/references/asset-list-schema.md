# 에셋 리스트 입력 스키마 (2단계)

2단계에 넘기는 에셋 리스트의 형식입니다. 화면·기능 단위로 모아서 줍니다.

## 항목 형식

각 에셋은 다음을 가집니다.

- **id** (`AST-001` 등): 고유 식별자.
- **name** (`friend-rabbit`): 파일명·slot에 쓰는 짧은 영문 이름.
- **slot**: 코드가 이 에셋을 읽는 경로(작업 저장소 기준). 예: `unity/Assets/Mallang/RR02/Art/friend-rabbit.png`.
- **purpose**: 용도 한 줄(어느 화면·어떤 역할).
- **changeFrequency** (`high`|`low`): 자주 바뀌는지. 참고용(묶음·우선순위 판단).
- **sceneType** (`character`|`scene`|`ui-icon`): 어떤 구도 규칙을 붙일지 결정합니다.
- **referenceImage** (선택): image→image 스타일 레퍼런스로 붙일 승인 이미지 경로. 없으면 생략.

## sceneType별 구도 규칙 매핑

프롬프트에 붙일 구도 지침을 sceneType으로 고릅니다(스타일 카드 3절에서 가져옴).

- **character**: 단일 캐릭터 중심. 투명 배경, 캐릭터 규격(비율·외곽선·얼굴)과 포인트색 적용. 배경 겹 없음.
- **scene**: 배경/중경/전경 최소 3겹 + 깊이 단서 2가지. "한 장면·한 감정" 초점. 명도·채도 위계(배경 후퇴).
- **ui-icon**: 단순 기호. 재질 언어(젤리·버블 등) 적용, 여백 확보, 숨긴 정답 노출 금지 규칙 준수.

## 예시

```json
[
  { "id": "AST-001", "name": "friend-rabbit", "slot": "unity/Assets/Mallang/RR02/Art/friend-rabbit.png",
    "purpose": "플레이 좌우 카드·악보·결과의 토끼 기본 자세", "changeFrequency": "high",
    "sceneType": "character", "referenceImage": "docs/design/.../rabbit-ref.png" },
  { "id": "AST-013", "name": "stage-background", "slot": "unity/Assets/Mallang/RR02/Art/stage-background.png",
    "purpose": "타이틀·플레이·결과 공용 무대 배경", "changeFrequency": "low",
    "sceneType": "scene" }
]
```
