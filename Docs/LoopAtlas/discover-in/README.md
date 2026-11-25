# discover-in: strategies other searches saw, offered to the atlas as loops

Any search or simulation that watches vessels play (the arena balancing models, an AI trainer, flight records) can drop
a JSON file here. `python3 Tools/Build/gene_search.py discover` reads it beside the gene search's own finds, skips a chain
that is not the vessel's verbs or that a combo already names, and `propose --top N` writes the best into the vessel's
YAML on the Proposed layer. A person keeps, renames or drops them there like any other proposal.

```json
{"strategies": [
  {"vessel": "Squirrel", "element": "Time", "chain": ["Time", "Charge"],
   "name": "optional, else the abilities' names", "cue": ["mass_ahead"], "t": [8, 30],
   "score": 0.8, "evidence": "what was seen, and where (a run id, a flight file)"}
]}
```
