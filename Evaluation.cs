using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace coil
{
    partial class Program
    {
        // One board on stdin, one JSON result on stdout; no file writes or generator calls.
        static int Evaluate(string[] args)
        {
            try
            {
                var (pos, opts) = ParseArgs(args);
                var allowed = new[] { "budget", "timeout-ms", "max-cells", "depth-limit", "directions", "starts", "pruning" };
                if (pos.Count != 0 || opts.Keys.Except(allowed).Any()) throw new ArgumentException("Unknown evaluation argument");
                int Number(string key, string value) => int.Parse(opts.GetValueOrDefault(key, value), CultureInfo.InvariantCulture);
                int maxCells = Number("max-cells", "10000");
                if (maxCells < 1 || maxCells > 100000) throw new ArgumentException("max-cells must be 1..100000");
                // Read at most the configured board bound, even for a malformed or oversized stdin stream.
                var input = new StringBuilder();
                while (input.Length <= maxCells + 128)
                {
                    int c = Console.In.Read();
                    if (c < 0) break;
                    input.Append((char)c);
                }
                if (input.Length > maxCells + 128) throw new ArgumentException("Board input exceeds max-cells bound");
                string board = input.ToString().Trim();
                var (w, h, wall) = CoilFormat.ParseBoard(board);
                if ((long)w * h > maxCells) throw new ArgumentException("Board exceeds max-cells bound");
                string pruning = opts.GetValueOrDefault("pruning", "on");
                if (pruning != "on" && pruning != "off") throw new ArgumentException("pruning must be on or off");
                var solver = new Solver(board) {
                    NodeBudget = long.Parse(opts.GetValueOrDefault("budget", "100000"), CultureInfo.InvariantCulture),
                    TimeLimitMilliseconds = Number("timeout-ms", "5000"), DepthLimit = Number("depth-limit", "2048"),
                    DirectionOrder = opts.GetValueOrDefault("directions", "URDL"), StartOrder = opts.GetValueOrDefault("starts", "natural"),
                    UseFeasibility = pruning == "on"
                };
                var sw = Stopwatch.StartNew();
                bool solved = solver.Solve();
                sw.Stop();
                if (solved) CoilFormat.Validate(board, solver.FirstSolution);
                int open = wall.Count(x => !x);
                Console.WriteLine(JsonSerializer.Serialize(new {
                    schemaVersion = 1, status = solved ? "solved" : solver.TimedOut ? "time_limit" : solver.DepthExceeded ? "depth_limit" : solver.BudgetExceeded ? "node_budget" : "unsolvable",
                    solved, validated = solved, width = w, height = h, openCells = open,
                    boardSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(board))).ToLowerInvariant(),
                    solution = solver.FirstSolution, elapsedSeconds = sw.Elapsed.TotalSeconds,
                    nodes = solver.Nodes, branchNodes = solver.BranchNodes, forcedMoves = solver.ForcedMoves,
                    startsTried = solver.StartsTried, candidateStarts = solver.CandidateStarts,
                    bestVisited = solver.BestVisited, coverage = open == 0 ? 0 : (double)solver.BestVisited / open,
                    budgetExceeded = solver.BudgetExceeded, timedOut = solver.TimedOut, depthExceeded = solver.DepthExceeded
                }));
                return 0; // Unsolved and bounded-out searches are valid evaluation results.
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OverflowException || ex is KeyNotFoundException || ex is InvalidOperationException)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { schemaVersion = 1, status = "error", error = ex.Message }));
                return 2;
            }
        }
    }
}
