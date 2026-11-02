"""Compute evaluators: one module per loss function. The contract every module keeps is in Docs/Compute/README.md
(section "An evaluator"); /compute-job is the skill that writes a new one."""

import os as _os
import sys as _sys

# The evaluators import each other by plain name (course_port, race_models), so the package's own folder is on the path
# wherever they are loaded from: the worker's pool, `compute.py eval`, the self-test.
_HERE = _os.path.dirname(_os.path.abspath(__file__))
if _HERE not in _sys.path:
    _sys.path.insert(0, _HERE)
