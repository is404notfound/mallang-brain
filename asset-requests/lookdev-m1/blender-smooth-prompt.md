# Blender GLB 다듬기 프롬프트 (Tripo 후처리용)

AI 에이전트 세션(Claude Code 등)에 아래 블록을 통째로 붙여 쓰는 프롬프트입니다. GLB 경로만 바꿔 쓰세요. 기준은 스타일 카드 v5의 스무딩 규칙입니다.

---

```
Tripo에서 뽑은 캐릭터 GLB를 Blender로 다듬어 Unity용 FBX로 출력하라.

입력: <GLB 경로>   (예: ~/Downloads/rabbit.glb)
출력: 같은 폴더에 <이름>_smooth.fbx + 미리보기 PNG 4장. 원본은 절대 덮어쓰지 말 것.

0. 환경 준비
- Blender가 없으면 설치부터: `brew install --cask blender` (실패 시 blender.org에서 macOS DMG).
- CLI 경로 확인: `/Applications/Blender.app/Contents/MacOS/Blender --version`
- 모든 작업은 헤드리스로: `Blender -b -P script.py -- <인자>` 형태의 파이썬 스크립트(bpy)로 실행하라. GUI를 띄우지 마라.

1. 다듬기 기준 (클레이 룩 — 어기지 말 것)
- 이 캐릭터는 수제 점토 피규어 룩이다. 미세한 표면 요철은 질감 자산이므로 유지한다.
- 제거 대상은 "실루엣을 깨는 혹·뾰족한 스파이크·면이 각져 보이는 셰이딩"뿐이다.
- 과도한 스무딩으로 매끈한 비닐 피규어처럼 만들면 실패다.
- UV와 텍스처는 반드시 보존한다. Voxel Remesh는 UV를 파괴하므로 금지 (쓰려면 나에게 먼저 물어라).

2. 작업 절차 (bpy 스크립트)
a. 빈 씬에서 GLB 임포트 (bpy.ops.import_scene.gltf).
b. 각 메시마다:
   - Merge by Distance 0.0001 (중복 정점 병합)
   - 노멀 재계산 (outside)
   - Shade Smooth 적용 + Smooth by Angle 약 60° (Blender 4.x: Smooth by Angle 모디파이어 또는 shade_smooth_by_angle)
c. 여기까지 하고 미리보기를 먼저 렌더하라 (대부분 여기서 끝난다 — 울퉁함의 다수는 지오메트리가 아니라 플랫 셰이딩 문제다).
d. 렌더에서 여전히 실루엣에 혹이 보이는 메시에만: Corrective Smooth 모디파이어 (factor 0.5, iterations 5) 적용 후 재렌더. factor를 올리기 전에 반드시 전후 비교 렌더를 남겨라.
e. 통계 보고: 메시별 verts/tris, UV 레이어 유무, 머티리얼 수, 적용한 처리 목록.

3. 미리보기 렌더 (사람 판정용)
- 중립 3점 조명(따뜻한 키 + 차가운 필), 옅은 회색 배경.
- 정면 / 측면 / 후면 / 3/4 앵글 4장을 PNG로 저장.

4. Unity용 FBX 출력
- bpy.ops.export_scene.fbx: 텍스처 포함(path mode COPY + embed), Apply Transform, 스케일 1.
- 출력 후 파일 크기와 경로를 보고하라.

5. 완료 조건
- 4장 렌더를 보여주고 내 판정을 기다려라. 내가 "통과"라고 하기 전에는 끝난 것이 아니다.
- 스크립트·렌더·FBX 경로를 전부 보고에 포함하라.
```

---

## 사용 뒤 할 일

- 렌더 4장을 보고 "점토 느낌이 살아 있는가 / 실루엣 혹이 사라졌는가"로 판정하세요 (스타일 카드 v5 기준).
- 통과하면 FBX를 Unity로 — plan.html m1-9 체크, 소요 시간을 m1-12(공수 기록)에 적습니다.
- 느낀 점 한 줄을 plan.html 5절 wf-8(Tripo 검수)·wf-9(Blender 보정)에 기록하세요.
