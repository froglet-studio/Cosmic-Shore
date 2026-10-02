import sys,json,time; sys.path.insert(0,'.')
from harness import scorecard
from snaptrap import SnapTrap
t=time.time()
card,runs=scorecard(lambda a,p: SnapTrap(a,p), {}, seeds=(7,23), minutes=1.5)
print(json.dumps(card,indent=1)); print(time.time()-t)
