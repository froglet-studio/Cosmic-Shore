"""Rev 117: write the Launch Console boards.  python3 gen.py <out project dir> Console Notes [Mission Pipeline ...]

Console is the published board (all six pages in one, the canvas had two free files); the page names also write each
page as a board of its own, for when Frogma has room for them."""
import sys, os, importlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
out = sys.argv[1]
for name in sys.argv[2:]:
    m = importlib.import_module('p_' + name.lower())
    open(os.path.join(out, 'R117' + name + '.dc.html'), 'w').write(m.build())
    print('wrote', name)
