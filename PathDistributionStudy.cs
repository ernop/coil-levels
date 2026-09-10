using System;
using System.Collections.Generic;
using System.Linq;

namespace coil
{
    public static class PathDistributionStudy
    {
        public static object Run(int side = 3, int maximumSteps = 16384, bool deep = false)
        {
            if (side < 1 || side > 3) throw new ArgumentOutOfRangeException(nameof(side));
            var states = new List<BackwardGenerator>();
            void Visit(BackwardGenerator state)
            {
                states.Add(state);
                for (var d = 0; d < 4; d++) if (state.CanPrepend((Dir)d))
                {
                    var child = BackwardGenerator.Replay(state.Recipe()); child.Prepend((Dir)d); Visit(child);
                }
            }
            for (var cell = 0; cell < side * side; cell++) Visit(new BackwardGenerator(side, side, cell % side, cell / side));
            var index = states.Select((s, i) => (key: s.RecipeJson(), i)).ToDictionary(p => p.key, p => p.i);
            var transitions = states.Select(_ => new Dictionary<int, double>()).ToArray();
            for (var i = 0; i < states.Count; i++)
            {
                var sampler = new ReversiblePathSampler(states[i]);
                void Record(bool success, double proposal, Action undo)
                {
                    if (!success) return;
                    var j = index[sampler.Snapshot().RecipeJson()];
                    transitions[i][j] = transitions[i].GetValueOrDefault(j) + proposal;
                    undo();
                    if (sampler.Snapshot().RecipeJson() != states[i].RecipeJson()) throw new InvalidOperationException("Study inverse failed.");
                }
                foreach (var start in new[] { true, false })
                foreach (var grow in new[] { true, false })
                for (var d = 0; d < 4; d++)
                for (var length = 1; length <= 8; length++)
                    Record(sampler.Endpoint(start, grow, d, length, 1, _ => 0), 1.0 / (12 * 4 * 8),
                        () => { if (!sampler.Endpoint(start, !grow, d, length, 1, _ => 0)) throw new InvalidOperationException("No inverse."); });
                foreach (var grow in new[] { true, false })
                for (var a = 0; a < side * side; a++)
                for (var d = 0; d < 4; d++)
                for (var length = 2; length <= 12; length++)
                for (var height = 1; height <= 3; height++)
                foreach (var right in new[] { true, false })
                    Record(sampler.Rectangle(grow, a, d, length, height, right, 1, _ => 0), 1.0 / (12 * side * side * 4 * 11 * 3 * 2),
                        () => { if (!sampler.Rectangle(!grow, a, d, length, height, right, 1, _ => 0)) throw new InvalidOperationException("No inverse."); });
            }
            var activities = new List<object>();
            foreach (var activity in new[] { 1, 2, 4 })
            {
                var target = states.Select(s => Math.Pow(activity, s.Count)).ToArray();
                var z = target.Sum(); for (var i = 0; i < target.Length; i++) target[i] /= z;
                var matrix = transitions.Select((row, i) => row.ToDictionary(p => p.Key,
                    p => (deep ? 7.0 / 8 : 1) * p.Value * Math.Min(1, Math.Pow(activity, states[p.Key].Count - states[i].Count)))).ToArray();
                if (deep && side >= 2)
                {
                    for (var i = 0; i < states.Count; i++)
                    {
                        var sampler = new ReversiblePathSampler(states[i]);
                        for (var size = 2; size <= Math.Min(4, side); size++)
                        for (var y = 0; y <= side - size; y++) for (var x = 0; x <= side - size; x++)
                        for (var entry = 0; entry < 4 * size; entry++)
                        {
                            var options = sampler.EnumerateBlock(x, y, size, entry);
                            if (options == null) continue;
                            var weights = options.Routes.Select(route => Math.Pow(activity, route.Length)).ToArray();
                            var zBlock = weights.Sum();
                            var proposal = 1.0 / (8 * (Math.Min(4, side) - 1) * (side - size + 1) * (side - size + 1) * 4 * size);
                            for (var route = 0; route < options.Routes.Count; route++)
                            {
                                sampler.ApplyBlock(options, route);
                                var j = index[sampler.Snapshot().RecipeJson()];
                                matrix[i][j] = matrix[i].GetValueOrDefault(j) + proposal * weights[route] / zBlock;
                                sampler.ApplyBlock(new BlockRoutes { Before = options.Before, After = options.After,
                                    Original = options.Routes[route], Routes = new[] { options.Original } }, 0);
                            }
                        }
                    }
                }
                double balanceError = 0;
                for (var i = 0; i < states.Count; i++) foreach (var entry in matrix[i])
                    balanceError = Math.Max(balanceError, Math.Abs(target[i] * entry.Value - target[entry.Key] * matrix[entry.Key].GetValueOrDefault(i)));
                if (balanceError > 1e-12) throw new InvalidOperationException("Detailed balance failed.");
                var startIndices = new[] { 0, states.FindIndex(s => s.Count == side * side) };
                var histories = new List<object>();
                foreach (var start in startIndices.Distinct())
                {
                    var probabilities = new double[states.Count]; probabilities[start] = 1;
                    var trace = new List<object>();
                    for (var step = 0; step <= maximumSteps; step++)
                    {
                        if (step == 0 || (step & (step - 1)) == 0 || step == maximumSteps)
                            trace.Add(new { step, totalVariation = probabilities.Select((p, i) => Math.Abs(p - target[i])).Sum() / 2,
                                meanOpenCells = probabilities.Select((p, i) => p * states[i].Count).Sum() });
                        if (step == maximumSteps) break;
                        var next = new double[states.Count];
                        for (var i = 0; i < states.Count; i++)
                        {
                            var outbound = 0.0;
                            foreach (var entry in matrix[i]) { var mass = probabilities[i] * entry.Value; next[entry.Key] += mass; outbound += mass; }
                            next[i] += probabilities[i] - outbound;
                        }
                        probabilities = next;
                    }
                    histories.Add(new { startState = start, initialOpenCells = states[start].Count, trace });
                }
                var occupancy = new double[side * side + 1];
                for (var i = 0; i < states.Count; i++) occupancy[states[i].Count] += target[i];
                activities.Add(new { activity, detailedBalanceMaximumError = balanceError,
                    meanOpenCells = target.Select((p, i) => p * states[i].Count).Sum(), occupancy, histories });
            }
            return new { side, solutionStates = states.Count, activities, method = deep ? "legacy-plus-block-heat-bath-v1" : "legacy-v1",
                target = "activity^openCells per ordered solution; NOT uniform boards",
                scope = "Full production proposal descriptors enumerated; detailed balance checked; exact finite-state probabilities propagated." };
        }
    }
}
