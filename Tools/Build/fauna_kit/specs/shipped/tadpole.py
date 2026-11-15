"""Tadpole: a SHIPPED creature, transcribed into the kit's spec language (recreate.py).

Measured from Assets/_Prefabs/FloraAndFauna/TadPoleFauna.prefab by dossier.py; `recreate.py --check` holds this file to the prefab, so a
change to either shows as a difference. It is not a celestial design: `celestial.py --shipped` lists the
laws this body breaks (the redesign list, /fauna-creation section 7.1). Wires are the shipped meshes by
reference (`mesh`, placed by `local`, a 3x4 matrix in the creature's space at root scale 1).
"""
SPEC = {'species': 'Tadpole',
 'shipped': True,
 'prefab': 'Assets/_Prefabs/FloraAndFauna/TadPoleFauna.prefab',
 'klass': 'Boid',
 'role': 'herbivore',
 'possession': {'top_speed': 15.0, 'mouth_reach': 20.0, 'attack': 'lunge (FaunaAttackDriver)'},
 'numbers': {'asset': 'Assets/_Prefabs/FloraAndFauna/TadPoleFauna.prefab',
             'min_speed': 10.0,
             'max_speed': 15.0,
             'trail_block_interaction_radius': 20.0,
             'feeding_cluster_radius': 10.0,
             'shield_bite': 0.25},
 'variants': {'Charge': {'body_scale': 1.081, 'min_speed': 10.0, 'max_speed': 15.0, 'erode_spindle': False},
              'Mass': {'body_scale': 3.0, 'min_speed': 10.0, 'max_speed': 15.0, 'erode_spindle': False},
              'Space': {'body_scale': 4.943, 'min_speed': 10.0, 'max_speed': 15.0, 'erode_spindle': True},
              'Time': {'body_scale': 0.8175, 'min_speed': 15.0, 'max_speed': 20.0, 'erode_spindle': False}},
 'root_scale': 0.4,
 'skin': [{'name': 'BezierCurve.001',
           'mesh': 'Assets/_Models/Fauna/bonita.fbx#BezierCurve.001',
           'limb': 'TadpoleSpindle',
           'local': [[3.0, 0.0, 0.0, 0.0], [0.0, 3.0, 0.0, 0.0], [0.0, 0.0, 3.0, 0.0]]}],
 'prisms': [{'name': 'BoidBlock',
             'kind': 'health',
             'shielded': False,
             'at': [0.0, 0.0, -5.81],
             'q': [0.0, 0.0, 0.0, 1.0],
             'size': [1.5, 1.5, 5.0],
             'limb': 'TadpoleSpindle'}],
 'heart': {'at': [0.0, 0.0, 0.0], 'scale': 1.75},
 'limbs': [],
 'spindles': ['TadpoleSpindle']}
