import unittest

import numpy as np

import trim_collision as tc


class EdgeOccupancy:
    """One occupied cell whose upper X bound meets the original wall exactly."""

    def __init__(self):
        self.lo = np.zeros(3)

    def cells_in(self, _b0, _b1):
        return np.array([[7, 0, 0]], dtype=np.int64)


class TrimCollisionTests(unittest.TestCase):
    def test_occupied_wall_is_not_deleted_when_shrinking_degenerates(self):
        original = [1.0, 0.0, 0.0, 1.34, 2.0, 0.34]
        boxes, stats = tc.trim([original], EdgeOccupancy())
        self.assertEqual(len(boxes), 1)
        self.assertEqual(stats["wall_degenerate"], 1)
        self.assertEqual(stats["wall_removed"], 0)
        self.assertEqual(stats["vol_after"], stats["vol_before"])


if __name__ == "__main__":
    unittest.main()
