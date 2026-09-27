"""Read existing FlyBrain cache only; create a small, attributable desktop-pet circuit.
No modification of source data, and no dependency on Python at runtime.
"""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
import pandas as pd
from scipy.sparse import load_npz

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--max-neurons', type=int, default=1800)
args = parser.parse_args()
n = pd.read_parquet(root / 'FlyBrain/cache/neurons.parquet')
import pyarrow.feather as feather
locations = {int(row['bodyId']): row['somaLocation'] for row in feather.read_table(
    root / 'FlyBrain/data/body-annotations-male-cns-v1.0-minconf-0.5.feather',
    columns=['bodyId', 'somaLocation']).to_pylist()}
w = load_npz(root / 'FlyBrain/cache/W_post_pre.npz').tocsr()
t = n.type.fillna('')
sweet = set(pd.read_csv(root / 'FlyBrain/flybrain/data/taste_grns.csv').query("taste == 'sweet'").bodyId)
groups = {
    'visual': t.isin(['LC4', 'LPLC2']),
    'escape': t.eq('DNp01'),
    'flight': t.str.startswith('DNg02') | t.isin(['DNa08', 'DNp31']),
    'steer': t.eq('DNa02'),
    'optomotor': t.eq('DNp04'),
    'reverse': t.eq('MDN'),
    'wing': t.str.startswith(('DLMn', 'DVMn')),
    'taste': n.bodyId.isin(sweet),
    'feed': t.eq('MN9'),
}
seed = np.flatnonzero(np.logical_or.reduce(list(groups.values())))
# Rank one-hop partners by raw absolute synapse counts, preserving all functional seeds.
score = np.asarray(abs(w[:, seed]).sum(axis=1)).ravel() + np.asarray(abs(w[seed, :]).sum(axis=0)).ravel()
score[seed] = 0
partners = np.flatnonzero(score > 0)
partners = partners[np.argsort(-score[partners], kind='stable')][:max(0,args.max_neurons-len(seed))]
selected = np.sort(np.concatenate([seed, partners]))
sub = w[selected, :][:, selected].tocoo()
nodes = []
for i in selected:
    row = n.iloc[i]
    nodes.append({'bodyId': str(int(row.bodyId)), 'type': str(row.type or ''), 'side': str(row.somaSide or ''),
                  'nt': str(row.nt), 'soma': locations.get(int(row.bodyId)),
                  'groups': [k for k, mask in groups.items() if mask.iloc[i]]})
edges = [[int(pre), int(post), int(count)] for post, pre, count in zip(sub.row, sub.col, sub.data) if count != 0]
result = {'source': 'MaleCNS v1.0 via local FlyBrain compiled cache',
          'sourceUrl': 'https://male-cns.janelia.org/download/',
          'selection': 'All functional seeds + strongest one-hop partners by absolute synapse count; induced subgraph.',
          'notes': 'Cached signs include FlyBrain NT assumptions. No receptor correction. Runtime weights are capped and normalized; sensory drives and decoder are engineered, not validated biology.',
          'cacheSha256': hashlib.sha256((root/'FlyBrain/cache/neurons.parquet').read_bytes()).hexdigest(),
          'nodes': nodes, 'edges': edges}
out = root / 'FlyPet/Assets/circuit.json'
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(result, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
print(json.dumps({'neurons':len(nodes), 'edges':len(edges), 'groups':{k:int(v.sum()) for k,v in groups.items()}, 'bytes':out.stat().st_size}))
