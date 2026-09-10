"""Group saved boards by recorded creation settings, independently of seed and outcome."""
import hashlib
import json

METHODS = {
    'tweak-generator': 'Walk and tweaks',
    'original-game': 'Original game',
    'archived-tweak-generator': 'Archived walk and tweaks',
    'hardness-selected-generator': 'Solver-selected tweaks',
    'backward-growth-v1': 'Backward growth',
    'reversible-path-v1': 'Earlier solution edits',
    'block-solution-edits-v1': 'Deeper solution edits',
    'exact-uniform-board-v1': 'Uniform catalog draws',
    'uniform-board-chain-v1': 'Board-flip chain',
    'uniform-board-rejection-v1': 'Uniform rejection',
}
DYNAMICS_SETTINGS = ('kernel', 'steps', 'activity', 'initialization', 'spacing', 'endpointMax',
    'rectangleMax', 'heightMax', 'lazyProbability', 'operationOrder', 'blockProbability', 'blockSides')
BATCH_SETTINGS = ('randomAlgorithm', 'catalogueSize', 'burnIn', 'stride', 'method')


def configuration(saved: dict) -> dict:
    method = saved.get('generator', 'tweak-generator')
    parameters = {'generator': method}
    for key in ('sampler', 'generationPolicy'):
        if key in saved:
            parameters[key] = saved[key]
    if 'archiveSettings' in saved:
        parameters['archiveSettings'] = saved['archiveSettings']
    if 'options' in saved:
        parameters['options'] = saved['options']
    dynamics = saved.get('dynamics') or {}
    if dynamics:
        parameters['dynamics'] = {k: dynamics[k] for k in DYNAMICS_SETTINGS if k in dynamics}
    batch = saved.get('samplingBatch') or {}
    if batch:
        parameters['samplingBatch'] = {k: batch[k] for k in BATCH_SETTINGS if k in batch}
    digest = hashlib.sha256(json.dumps(parameters, sort_keys=True).encode()).hexdigest()[:16]
    label = METHODS.get(method, method)
    if saved.get('archiveSettings'):
        label = 'Archived · ' + saved['archiveSettings'][0] + (f" (+{len(saved['archiveSettings']) - 1} other recorded recipes)" if len(saved['archiveSettings']) > 1 else '')
    elif dynamics:
        label += f" · {dynamics['initialization']} · activity {dynamics['activity']} · {dynamics['steps']:,} steps"
    elif saved.get('options'):
        label = ' · '.join(f'{k} {v}' for k, v in saved['options'].items())
    elif batch.get('burnIn') is not None:
        label += f" · burn-in {batch['burnIn']:,} · stride {batch['stride']:,}"
    return {'id': digest, 'method': method, 'methodLabel': METHODS.get(method, method), 'label': label, 'parameters': parameters}
