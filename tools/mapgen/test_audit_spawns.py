import unittest
from unittest.mock import patch

import numpy as np

import audit_spawns as audit


class SpawnAuditTests(unittest.TestCase):
    def test_player_start_gets_a_clean_replacement(self):
        data = {
            "start": {"authored": [0, 0, 0], "resolved": [0, 0, 0]},
            "nav": {"pos": [0, 0, 0, 1, 2.5, 0], "pool": [0, 1]},
        }
        # Collision accepts both nodes, but visible art covers the first one's head.
        def check_point(_index, _normals, point):
            return (["UNDER"] if point[0] == 0 else []), 0

        with patch.object(audit, "full_pool", True), \
             patch.object(audit, "load_triangles", return_value=(np.zeros((1, 3, 3)), None)), \
             patch.object(audit, "TriIndex"), \
             patch.object(audit, "load_mask", return_value=None), \
             patch.object(audit, "check_point", side_effect=check_point):
            result = audit.audit_map("unused", "test", data, {}, 0, np.random.default_rng(7))
        self.assertEqual(result["start"], ["UNDER"])
        self.assertEqual(result["moves"][0]["name"], "start:start")
        self.assertEqual(result["moves"][0]["to"], [1, 2.5, 0])
        self.assertEqual(result["dm"]["ok"], 1)
        self.assertEqual(result["dm_excluded"][0][0], [0, 0, 0])

    def test_hidden_materials_are_not_spawn_surfaces(self):
        js = {
            "nodes": [{"mesh": 0, "translation": [1, 2, 3]}],
            "materials": [{"name": name} for name in ("Stone", "EnvCollision_MAT", "Decal_Paint")],
            "meshes": [{"primitives": [
                {"material": i, "attributes": {"POSITION": 0}, "indices": 1} for i in range(3)
            ]}],
        }
        positions = np.array([[0, 0, 0], [1, 0, 0], [0, 0, 1]], dtype=float)
        arrays = [positions, np.array([0, 1, 2])]
        with patch.object(audit.gc, "accessor", side_effect=lambda _, i: arrays[i]):
            triangles = list(audit.drawn_triangles(js))
        self.assertEqual(len(triangles), 1)
        np.testing.assert_allclose(triangles[0][0], positions + [1, 2, 3])


if __name__ == "__main__":
    unittest.main()
