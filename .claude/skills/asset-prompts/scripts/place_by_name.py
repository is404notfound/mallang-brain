#!/usr/bin/env python3
"""돌려받은 낱장 이미지를 manifest의 파일명→slot 규칙대로 코드 내 자리에 배치합니다.

GPT 등에서 사람이 생성해 inbox 폴더에 떨군 PNG를, 각 에셋의 slot(코드가 읽는 경로)으로
복사합니다. 격자 시트를 자르는 split_spritesheet.py와 달리 이쪽은 낱장 파일명 매칭입니다.

사용법:
    python3 place_by_name.py <manifest.json> [--force] [--dry-run]

manifest 형식:
    {
      "inbox": "asset-requests/_inbox",
      "assets": [
        { "id": "AST-001", "name": "friend-rabbit",
          "slot": "unity/Assets/Mallang/RR02/Art/friend-rabbit.png",
          "expectedFile": "AST-001-friend-rabbit.png",
          "variations": 3, "chosen": null }
      ]
    }

inbox·slot 경로는 manifest 파일 위치를 기준으로 상대 해석합니다.
chosen이 있으면 그 파일을, 없으면 expectedFile을 inbox에서 찾습니다.
slot에 파일이 이미 있으면 --force 없이는 건너뜁니다(placeholder 교체는 --force).
결과를 manifest 폴더 옆 placed.json에 기록합니다.

종료 코드: 전부 배치 성공 0 / 미매칭·건너뜀이 있으면 1.
"""
import argparse
import json
import os
import shutil
import sys


class PlaceResult:
    def __init__(self):
        self.placed = []            # [{"id","slot"}]
        self.unmatched_assets = []  # asset id: inbox에 해당 파일 없음
        self.unmatched_files = []   # inbox 파일인데 어느 asset과도 안 맞음
        self.skipped = []           # asset id: slot 이미 존재(force 아님)

    @property
    def exit_code(self):
        bad = self.unmatched_assets or self.unmatched_files or self.skipped
        return 1 if bad else 0


def place(manifest_path, force=False, dry_run=False):
    manifest_path = os.path.abspath(manifest_path)
    base = os.path.dirname(manifest_path)
    with open(manifest_path, encoding="utf-8") as f:
        man = json.load(f)

    inbox = os.path.join(base, man.get("inbox", "_inbox"))
    assets = man.get("assets", [])
    result = PlaceResult()

    wanted = {}  # inbox 파일명 -> asset (매칭 소비 추적용)
    for a in assets:
        fname = a.get("chosen") or a.get("expectedFile")
        if fname:
            wanted[fname] = a

    for a in assets:
        fname = a.get("chosen") or a.get("expectedFile")
        src = os.path.join(inbox, fname) if fname else None
        if not src or not os.path.exists(src):
            result.unmatched_assets.append(a["id"])
            continue
        slot_abs = a["slot"] if os.path.isabs(a["slot"]) else os.path.join(base, a["slot"])
        if os.path.exists(slot_abs) and not force:
            result.skipped.append(a["id"])
            continue
        if not dry_run:
            os.makedirs(os.path.dirname(slot_abs), exist_ok=True)
            shutil.copyfile(src, slot_abs)
        result.placed.append({"id": a["id"], "slot": a["slot"]})

    # inbox에 있으나 어느 asset도 원하지 않는 파일
    if os.path.isdir(inbox):
        for name in sorted(os.listdir(inbox)):
            full = os.path.join(inbox, name)
            if os.path.isfile(full) and name not in wanted:
                result.unmatched_files.append(name)

    if not dry_run:
        placed_path = os.path.join(base, "placed.json")
        with open(placed_path, "w", encoding="utf-8") as f:
            json.dump({
                "placed": result.placed,
                "unmatchedAssets": result.unmatched_assets,
                "unmatchedFiles": result.unmatched_files,
                "skipped": result.skipped,
            }, f, ensure_ascii=False, indent=2)

    return result


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("manifest")
    ap.add_argument("--force", action="store_true", help="slot이 이미 있어도 덮어씀")
    ap.add_argument("--dry-run", action="store_true", help="복사·기록 없이 계획만 출력")
    args = ap.parse_args()

    r = place(args.manifest, force=args.force, dry_run=args.dry_run)
    for p in r.placed:
        print(f"배치: {p['id']} -> {p['slot']}")
    for aid in r.skipped:
        print(f"건너뜀(slot 이미 있음, --force로 덮기): {aid}", file=sys.stderr)
    for aid in r.unmatched_assets:
        print(f"미매칭 에셋(inbox에 파일 없음): {aid}", file=sys.stderr)
    for name in r.unmatched_files:
        print(f"미매칭 파일(어느 에셋과도 안 맞음): {name}", file=sys.stderr)
    sys.exit(r.exit_code)


if __name__ == "__main__":
    main()
