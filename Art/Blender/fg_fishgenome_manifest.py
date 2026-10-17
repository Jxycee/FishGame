# fg_fishgenome_manifest.py (FishGenome art, 2026-10-17): merges fishgenome_meshes.json, fishgenome_morphs.json and the mask list into
# Art/Export/FishGenome/fishgenome_manifest.json. Run after fg_fishgenome.py, fg_fishgenome_masks.py and fg_fishgenome_morphs.py: python3 Art/Blender/fg_fishgenome_manifest.py
import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import fg_genome_common as G
OUT = os.path.join(G.root(), 'Art', 'Export', 'FishGenome')
meshes = json.load(open(os.path.join(OUT, 'fishgenome_meshes.json')))
morphs = json.load(open(os.path.join(OUT, 'fishgenome_morphs.json')))
names = ['Stripes', 'Bars', 'Spots', 'Rosettes', 'Marbled', 'Mottled', 'Saddles', 'TwoTone']
models = []
for m in meshes:
    e = {k: m[k] for k in ('name', 'file', 'tris', 'material', 'skinLayer', 'shapeKeys', 'uvSets', 'bodyLengthM', 'notes')}
    e['morphs'] = [dict(name=x['name'], recipe=x['recipe'], file=x['file'], morphLayer=x['morphLayer']) for x in morphs['morphs'] if x['species'] == m['name']]
    e['placement'] = None
    models.append(e)
man = dict(area='FishGenome', branch='fish-genome', models=models,
           patternMasks=dict(atlas='Art/Textures/FishGenome/T_FishPatternArray.png', columns=4, rows=2, tile=512, linear=True, channel='R (greyscale)',
                             layers=[dict(layer=i, name=n, file='Art/Textures/FishGenome/PatternMask_%d_%s.png' % (i, n)) for i, n in enumerate(names)]),
           morphSheet=dict(file='Art/Export/FishGenome/T_FishMorphArray.png', columns=4, rows=morphs['rows'], tile=512, srgb=True, layerFormula=morphs['layerFormula']),
           shapeKeyWeights='Unity blend shape weights 0..100 (Blender 0..1)')
with open(os.path.join(OUT, 'fishgenome_manifest.json'), 'w') as f: json.dump(man, f, indent=1)
print('FGMANIFEST', len(models), 'models')
