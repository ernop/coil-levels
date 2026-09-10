"""Rank-normalized split/folded R-hat and bulk ESS for scalar chain traces.

Uses Vehtari et al. (2021): pooled midranks with the Blom transform,
split-chain variance estimates, and Geyer's initial-positive, monotone pairs.
These diagnose the supplied observables, not the entire board distribution.
"""
import math
from statistics import NormalDist, mean, median, variance


def ranks(values: list[float]) -> list[float]:
    ordered = sorted(range(len(values)), key=values.__getitem__)
    result = [0.0] * len(values)
    lo = 0
    while lo < len(values):
        hi = lo + 1
        while hi < len(values) and values[ordered[hi]] == values[ordered[lo]]:
            hi += 1
        midrank = (lo + 1 + hi) / 2
        for index in ordered[lo:hi]:
            result[index] = midrank
        lo = hi
    return result


def normalize(chains: list[list[float]]) -> list[list[float]]:
    flat = [x for chain in chains for x in chain]
    normal = NormalDist()
    transformed = [normal.inv_cdf((r - 3 / 8) / (len(flat) + 1 / 4)) for r in ranks(flat)]
    n = len(chains[0])
    return [transformed[i:i+n] for i in range(0, len(transformed), n)]


def variance_parts(chains: list[list[float]]) -> tuple[float, float]:
    n = len(chains[0])
    within = mean(variance(chain) for chain in chains)
    between = n * variance([mean(chain) for chain in chains])
    return within, (n - 1) / n * within + between / n


def rhat(chains: list[list[float]]) -> float:
    within, total = variance_parts(chains)
    if within == 0:
        return math.inf if total > 0 else math.nan
    return math.sqrt(total / within)


def bulk_ess(chains: list[list[float]]) -> float:
    m, n = len(chains), len(chains[0])
    within, total = variance_parts(chains)
    if within == 0 or total <= 0:
        return 0.0
    centers = [mean(chain) for chain in chains]
    rho = [1.0]
    for lag in range(1, n):
        covariance = mean(sum((chain[i] - center) * (chain[i+lag] - center)
                              for i in range(n-lag)) / n for chain, center in zip(chains, centers))
        rho.append(1 - (within - covariance) / total)
    pairs = []
    for i in range(0, n-1, 2):
        pair = rho[i] + rho[i+1]
        if pair <= 0:
            break
        pairs.append(min(pair, pairs[-1]) if pairs else pair)
    # Cap at the actual retained draw count; negative autocorrelation can
    # otherwise produce a larger ESS. The cap is conservative and explicit.
    return min(float(m * n), m * n / max(1.0, -1 + 2 * sum(pairs)))


def diagnose(chains: list[list[float]]) -> dict:
    if len(chains) < 2 or len({len(chain) for chain in chains}) != 1 or len(chains[0]) < 8:
        raise ValueError("Need at least two equal-length chains with at least eight retained observations")
    if any(not math.isfinite(x) for chain in chains for x in chain):
        raise ValueError("Trace values must be finite")
    half = len(chains[0]) // 2
    split = [part for chain in chains for part in [chain[:half], chain[-half:]]]
    normalized = normalize(split)
    center = median([x for chain in split for x in chain])
    folded = normalize([[abs(x-center) for x in chain] for chain in split])
    rank_rhat, fold_rhat = rhat(normalized), rhat(folded)
    candidates = [x for x in [rank_rhat, fold_rhat] if not math.isnan(x)]
    maximum = max(candidates) if candidates else math.nan
    return {"chains": len(chains), "retainedPerChain": len(chains[0]),
            "rHat": maximum if math.isfinite(maximum) else None,
            "rankSplitRHat": rank_rhat if math.isfinite(rank_rhat) else None,
            "foldedSplitRHat": fold_rhat if math.isfinite(fold_rhat) else None,
            "bulkEss": bulk_ess(normalized),
            "chainMeans": [mean(chain) for chain in chains],
            "status": "constant-or-degenerate" if not math.isfinite(maximum) else
                      "between-chain-or-within-chain-disagreement" if maximum > 1.01 else "no-rhat-disagreement-detected",
            "scope": "Scalar observables only; low R-hat does not prove board mixing. ESS assumes a stationary target and is capped at retained draws."}
