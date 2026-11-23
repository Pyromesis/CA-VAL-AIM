import unittest

import audit_collision as audit


class StepWorld:
    """One start cell and one raised neighbour, with no floor anywhere else."""

    def __init__(self, height):
        self.height = height

    def ground(self, x, z, feet_y, step=audit.STEP):
        if z != 0:
            return -1e9
        if x == 0:
            top = 0
        elif x == audit.CELL:
            top = self.height
        else:
            return -1e9
        return top if top <= feet_y + step else -1e9

    def blocked(self, x, z, feet_y):
        return False


class CollisionAuditTests(unittest.TestCase):
    def test_walking_flood_uses_only_the_real_step_height(self):
        for height in (0.70, 0.71, 1.05):
            with self.subTest(height=height):
                reached = audit.flood(StepWorld(height), [0, 0, 0])
                self.assertEqual((1, 0) in reached, height <= audit.STEP)

    def test_crouch_jump_reach_is_explicitly_opt_in(self):
        reached = audit.flood(StepWorld(1.05), [0, 0, 0], rise=audit.RISE)
        self.assertIn((1, 0), reached)
        self.assertEqual(reached[(1, 0)], 1.05)


if __name__ == "__main__":
    unittest.main()
