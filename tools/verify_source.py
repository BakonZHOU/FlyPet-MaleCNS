"""Independently compare FlyPet's embedded circuit with local MaleCNS files.

Run from any directory with the FlyBrain virtualenv Python. This script does not
import FlyPet's extractor or simulator, and never edits source data.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
import pandas as pd
import pyarrow.feather as feather
from scipy.sparse import load_npz

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--hash-weights', action='store_true', help='SHA-256 all 1.1 GB of original weight data')
args = parser.parse_args()
asset = json.loads((root / 'FlyPet/Assets/circuit.json').read_text(encoding='utf-8'))
manifest = json.loads((root / 'FlyBrain/flybrain/data/manifest.json').read_text(encoding='utf-8'))
official = {item['path']: item for item in manifest['malecns']['files']}

def sha256(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(4 * 1024 * 1024), b''):
            h.update(chunk)
    return h.hexdigest()

annotation_path = root / 'FlyBrain/data/body-annotations-male-cns-v1.0-minconf-0.5.feather'
assert sha256(annotation_path) == official[annotation_path.name]['sha256'], 'Original annotation hash differs from official manifest'
neurons_path = root / 'FlyBrain/cache/neurons.parquet'
assert sha256(neurons_path) == asset['cacheSha256'], 'Neuron cache differs from extraction input'
neurons = pd.read_parquet(neurons_path)
index_by_id = {int(body_id): i for i, body_id in enumerate(neurons.bodyId)}
locations = {int(row['bodyId']): row['somaLocation'] for row in feather.read_table(
    annotation_path, columns=['bodyId', 'somaLocation']).to_pylist()}
selected = []
for node in asset['nodes']:
    body_id = int(node['bodyId'])
    i = index_by_id[body_id]
    selected.append(i)
    row = neurons.iloc[i]
    assert node['type'] == str(row.type or ''), f'type differs: {body_id}'
    assert node['side'] == str(row.somaSide or ''), f'side differs: {body_id}'
    assert node['soma'] == locations.get(body_id), f'soma differs: {body_id}'

# All signed selected edges must match the separately compiled local matrix.
matrix = load_npz(root / 'FlyBrain/cache/W_post_pre.npz').tocsr()
expected = matrix[selected, :][:, selected].tocoo()
actual_edges = {(e[0], e[1]): e[2] for e in asset['edges']}
assert len(actual_edges) == len(asset['edges']), 'duplicate embedded edge'
expected_edges = {(int(pre), int(post)): int(weight) for post, pre, weight in zip(
    expected.row, expected.col, expected.data) if weight != 0}
assert actual_edges == expected_edges, 'Embedded signed edges differ from compiled MaleCNS cache'

result = {'annotation_sha256': official[annotation_path.name]['sha256'],
          'cache_sha256': asset['cacheSha256'], 'verified_nodes': len(selected),
          'verified_signed_edges': len(actual_edges), 'weight_source': 'FlyBrain compiled cache'}
if args.hash_weights:
    weights_path = root / 'FlyBrain/data/connectome-weights-male-cns-v1.0-minconf-0.5.feather'
    actual = sha256(weights_path)
    expected_hash = official[weights_path.name]['sha256']
    assert actual == expected_hash, 'Original weight table hash differs from official manifest'
    result['official_weight_sha256'] = actual
print(json.dumps(result, indent=2))
