"""place_by_name.py 테스트 (stdlib unittest — 의존성 0).

실행: python3 -m unittest test_place_by_name -v
"""
import json
import os
import tempfile
import unittest

from PIL import Image

import place_by_name


def _png(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.new("RGBA", (4, 4), (255, 0, 0, 255)).save(path)


def _manifest(root, assets, inbox="_inbox"):
    man = {"inbox": inbox, "assets": assets}
    path = os.path.join(root, "manifest.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(man, f)
    return path


class PlaceByNameTest(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.root = self._tmp.name

    def tearDown(self):
        self._tmp.cleanup()

    def test_places_expected_file_to_slot(self):
        _png(os.path.join(self.root, "_inbox", "AST-001-friend-rabbit.png"))
        man = _manifest(self.root, [
            {"id": "AST-001", "name": "friend-rabbit",
             "slot": "art/friend-rabbit.png",
             "expectedFile": "AST-001-friend-rabbit.png"}
        ])
        result = place_by_name.place(man)
        self.assertTrue(os.path.exists(os.path.join(self.root, "art/friend-rabbit.png")))
        self.assertEqual([p["id"] for p in result.placed], ["AST-001"])
        self.assertEqual(result.unmatched_files, [])
        self.assertEqual(result.unmatched_assets, [])
        self.assertTrue(os.path.exists(os.path.join(self.root, "placed.json")))

    def test_chosen_variant_overrides_expected(self):
        _png(os.path.join(self.root, "_inbox", "AST-001-friend-rabbit-v2.png"))
        man = _manifest(self.root, [
            {"id": "AST-001", "name": "friend-rabbit",
             "slot": "art/friend-rabbit.png",
             "expectedFile": "AST-001-friend-rabbit.png",
             "chosen": "AST-001-friend-rabbit-v2.png"}
        ])
        result = place_by_name.place(man)
        self.assertEqual([p["id"] for p in result.placed], ["AST-001"])
        self.assertTrue(os.path.exists(os.path.join(self.root, "art/friend-rabbit.png")))

    def test_unmatched_returned_file_is_reported(self):
        _png(os.path.join(self.root, "_inbox", "AST-001-friend-rabbit.png"))
        _png(os.path.join(self.root, "_inbox", "stray-leftover.png"))
        man = _manifest(self.root, [
            {"id": "AST-001", "name": "friend-rabbit",
             "slot": "art/friend-rabbit.png",
             "expectedFile": "AST-001-friend-rabbit.png"}
        ])
        result = place_by_name.place(man)
        self.assertIn("stray-leftover.png", result.unmatched_files)
        self.assertEqual(result.exit_code, 1)

    def test_missing_returned_file_is_reported(self):
        man = _manifest(self.root, [
            {"id": "AST-001", "name": "friend-rabbit",
             "slot": "art/friend-rabbit.png",
             "expectedFile": "AST-001-friend-rabbit.png"}
        ])
        result = place_by_name.place(man)
        self.assertIn("AST-001", result.unmatched_assets)
        self.assertEqual(result.exit_code, 1)

    def test_existing_slot_not_overwritten_without_force(self):
        _png(os.path.join(self.root, "_inbox", "AST-001-friend-rabbit.png"))
        _png(os.path.join(self.root, "art", "friend-rabbit.png"))  # 이미 있음
        man = _manifest(self.root, [
            {"id": "AST-001", "name": "friend-rabbit",
             "slot": "art/friend-rabbit.png",
             "expectedFile": "AST-001-friend-rabbit.png"}
        ])
        result = place_by_name.place(man)  # force 없음
        self.assertIn("AST-001", result.skipped)
        self.assertEqual(result.placed, [])
        result2 = place_by_name.place(man, force=True)
        self.assertEqual([p["id"] for p in result2.placed], ["AST-001"])

    def test_dry_run_writes_nothing(self):
        _png(os.path.join(self.root, "_inbox", "AST-001-friend-rabbit.png"))
        man = _manifest(self.root, [
            {"id": "AST-001", "name": "friend-rabbit",
             "slot": "art/friend-rabbit.png",
             "expectedFile": "AST-001-friend-rabbit.png"}
        ])
        place_by_name.place(man, dry_run=True)
        self.assertFalse(os.path.exists(os.path.join(self.root, "art/friend-rabbit.png")))
        self.assertFalse(os.path.exists(os.path.join(self.root, "placed.json")))


if __name__ == "__main__":
    unittest.main()
