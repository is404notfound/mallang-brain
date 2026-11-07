# 프롬프트 팩 템플릿 (2단계)

승인된 스타일 카드와 에셋 리스트로 두 산출물을 만듭니다.
- `asset-requests/prompts.md` — 사람이 GPT에 붙일 에셋별 프롬프트.
- `asset-requests/manifest.json` — 돌려받은 파일을 slot에 배치할 매핑(배치 스크립트 입력).

## prompts.md 양식

맨 위에 공통 스타일(스타일 카드 1절)을 한 번 싣고, "이 문단을 아래 모든 프롬프트 앞에 붙여 생성하세요"라고 안내합니다. **확정 기준 이미지(스타일 카드 0절)가 있으면 "모든 캐릭터·장면 에셋은 이 이미지를 스타일 레퍼런스로 함께 첨부하세요"를 맨 위에 명시합니다** (필수). 이어서 에셋마다 한 블록:

```
### AST-001 · friend-rabbit  (sceneType: character)
용도: 플레이 좌우 카드·악보·결과의 토끼 기본 자세
레퍼런스 이미지: docs/design/.../rabbit-ref.png  (이 이미지를 스타일 레퍼런스로 함께 첨부)

프롬프트:
[공통 스타일 문단] + [토끼 개별 묘사: 외형·포인트색·시그니처(스타일 카드 4절)]
+ [character 구도 규칙: 투명 배경, 캐릭터 규격, 배경 겹 없음]

변형 지시: 같은 캐릭터·같은 규격으로 포즈/표정만 다른 3안을 만들어 주세요.
돌려줄 파일 이름: 고른 1안을 AST-001-friend-rabbit.png 로, 혹은 3안을
  AST-001-friend-rabbit-v1.png ~ -v3.png 로 저장해 하나를 manifest의 chosen에 적으세요.
```

규칙:
- **공통 스타일 + 개별 묘사 + sceneType 구도 규칙**을 반드시 결합합니다(스타일 카드 1·3·4절).
- **감정 목표를 에셋마다 명시합니다** (필수, 스타일 카드 1.5절). 각 프롬프트에 "이 에셋이 주는 느낌: <예: 수줍지만 설레는 다람쥐>" 한 줄을 넣고, 그 감정을 **빛·표정·순간·질감·공기감** 레버로 풀어 씁니다 — 정확한 묘사만으로 끝내지 않습니다. 예: "트럼펫을 불려다 수줍게 눈을 돌리는 찰나, 따뜻한 전구 빛이 볼에 번지고 홍조, 말랑한 질감".
- **변형은 changeFrequency로 가릅니다**: `high`(캐릭터 등 자주 바뀌는 것)는 2~3안을 요청하고, `low`(배경·아이콘)는 단발 1안만 요청합니다. 비용·수고를 줄이고 중요한 에셋에만 선택지를 둡니다.
- **referenceImage가 지정됐는데 파일이 없으면** 그 프롬프트에 레퍼런스 줄을 넣지 말고, prompts.md 끝 "경고" 목록에 "AST-xxx: 레퍼런스 이미지 없음"으로 올립니다 (필수).
- 금지사항(스타일 카드 6절)에서 해당 에셋에 걸리는 항목을 프롬프트에 음성 지시로 덧붙입니다("… 하지 마세요").

## manifest.json 양식

`place_by_name.py`가 읽는 스키마와 **정확히 같아야** 합니다.

```json
{
  "inbox": "asset-requests/_inbox",
  "assets": [
    { "id": "AST-001", "name": "friend-rabbit",
      "slot": "unity/Assets/Mallang/RR02/Art/friend-rabbit.png",
      "expectedFile": "AST-001-friend-rabbit.png",
      "variations": 3, "chosen": null }
  ]
}
```

- **expectedFile**: `<id>-<name>.png` 고정 규칙.
- **variations**: 요청한 변형 수. changeFrequency high면 2~3, low면 1.
- **chosen**: 처음엔 `null`. 사람이 3안 중 하나를 고르면 그 파일명(예: `AST-001-friend-rabbit-v2.png`)을 여기 적습니다. 배치 스크립트는 chosen이 있으면 그걸, 없으면 expectedFile을 slot에 넣습니다.
- **slot**: 에셋 리스트의 slot 그대로.

## 사람 안내 (prompts.md 끝에 포함)

1. 공통 스타일 문단을 모든 프롬프트 앞에 붙여 GPT에서 하나씩 생성합니다. 레퍼런스가 있으면 함께 첨부합니다.
2. 각 에셋의 3안 중 하나를 고릅니다. 고른 파일을 `expectedFile` 이름으로 저장하거나, 세 안을 `-v1~-v3`로 저장하고 manifest의 `chosen`에 고른 파일명을 적습니다.
3. 돌려받은 PNG를 `asset-requests/_inbox/`에 넣고 배치:
   ```
   python3 .claude/skills/asset-prompts/scripts/place_by_name.py asset-requests/manifest.json
   ```
   slot에 placeholder가 있으면 `--force`로 교체합니다. 결과는 `placed.json`에 기록됩니다.
