import unittest

import numpy as np

import gen_collision as gc


class GenCollisionTests(unittest.TestCase):
    @staticmethod
    def covers(boxes, voxel):
        i, j, k = voxel
        cell = 0.5
        center = ((i + 0.5) * cell, (k + 0.5) * cell, (j + 0.5) * cell)
        return any(
            box[0] <= center[0] <= box[3]
            and box[1] <= center[1] <= box[4]
            and box[2] <= center[2] <= box[5]
            for box in boxes
        )

    def test_vertical_merging_does_not_drop_voxels_next_to_a_done_cell(self):
        grid = gc.Grid(0.5)
        grid.add_wall(0, 0, 0.0, 1.0)  # layers 0 and 1
        grid.add_wall(0, 1, 0.5, 1.0)  # only layer 1, beside an already emitted cell
        boxes = gc.build_boxes(grid)
        self.assertTrue(self.covers(boxes, (0, 0, 0)))
        self.assertTrue(self.covers(boxes, (0, 0, 1)))
        self.assertTrue(self.covers(boxes, (0, 1, 1)))

    def test_small_wall_fragment_is_not_discarded(self):
        grid = gc.Grid(0.5)
        # This 0.28 m high, 0.239 m² fragment is part of a continuous visible wall in Abyss.
        gc.rasterise(
            grid,
            np.array([0.0, 0.0, 0.0]),
            np.array([0.0, 0.28, 0.0]),
            np.array([0.0, 0.0, 1.71]),
        )
        self.assertTrue(grid.walls)


if __name__ == "__main__":
    unittest.main()
