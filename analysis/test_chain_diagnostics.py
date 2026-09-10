import random
import unittest
from analysis.chain_diagnostics import diagnose, ranks


class DiagnosticsTests(unittest.TestCase):
    def test_midranks(self):
        self.assertEqual(ranks([5, 1, 5, 3]), [3.5, 1, 3.5, 2])

    def test_independent_normal_draws(self):
        chains = [[random.Random(seed * 10000 + i).gauss(0, 1) for i in range(600)] for seed in range(1, 5)]
        result = diagnose(chains)
        self.assertLess(result['rHat'], 1.01)
        self.assertGreater(result['bulkEss'], 1000)

    def test_location_and_scale_disagreement(self):
        rng = random.Random(37)
        base = [[rng.gauss(0, 1) for _ in range(600)] for _ in range(4)]
        shifted = [[x + index * 3 for x in chain] for index, chain in enumerate(base)]
        scaled = [[x * (1 if index < 2 else 10) for x in chain] for index, chain in enumerate(base)]
        self.assertGreater(diagnose(shifted)['rHat'], 1.1)
        self.assertGreater(diagnose(scaled)['foldedSplitRHat'], 1.1)

    def test_constants_are_not_evidence_of_mixing(self):
        self.assertIsNone(diagnose([[1.0] * 20, [1.0] * 20])['rHat'])
        self.assertIsNone(diagnose([[1.0] * 20, [2.0] * 20])['rHat'])


if __name__ == '__main__':
    unittest.main()
