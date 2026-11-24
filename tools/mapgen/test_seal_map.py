import unittest
from unittest.mock import Mock, patch

import numpy as np

import seal_map


class SealMapTests(unittest.TestCase):
    def test_wall_on_cell_boundary_is_sealed(self):
        # A visible wall at x=0 crosses from cell 0 to cell 1. A ray starting exactly on its plane
        # used to miss it, leaving both walking and bullets free to cross at the cell centre.
        triangles = np.array(
            [
                [[0, 0, -1], [0, 2, 1], [0, 0, 1]],
                [[0, 0, -1], [0, 2, -1], [0, 2, 1]],
            ],
            dtype=np.float32,
        )
        normals = np.zeros_like(triangles)
        world = Mock()
        world.blocked.return_value = False
        world.ground.return_value = 0.0
        with (
            patch.object(seal_map.ac, "World", return_value=world),
            patch.object(
                seal_map.ac, "flood", return_value={(i, 0): 0.0 for i in range(50)}
            ),
            patch.object(
                seal_map.au, "load_triangles", return_value=(triangles, normals)
            ),
        ):
            walls, pairs, cells = seal_map.leak_walls("unused", [], [[0, 0, 0]])
        self.assertGreater(pairs, 0)
        self.assertEqual(cells, 50)
        self.assertEqual(len(walls), 1)
        self.assertLessEqual(float(walls[0][0]), 0.0)
        self.assertGreaterEqual(float(walls[0][3]), 0.0)


if __name__ == "__main__":
    unittest.main()
