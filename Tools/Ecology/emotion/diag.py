import sys
from model import *; from iterate import GROUPS
from archetypes import FAMILIES
keys=sum([g for _,g in GROUPS],[])
if len(sys.argv)>1: keys=[k for k in keys if k not in sys.argv[1].split(',')]
X,y,fam,rows=load('results/reference_set.json',keys)
res={}
for kind in ('logistic','prototype'):
  for k in range(4):
    tr,te=fam!=k,fam==k; m=make(kind,**({'lam':0.1} if kind=='logistic' else {})).fit(X[tr],y[tr]); p=m.proba(X[te]).argmax(1)
    for e in range(6):
      s=(y[te]==e); res.setdefault(FAMILIES[EMOTIONS[e]][k].__name__,[]).append((round(np.mean(p[s]==e),2), EMOTIONS[np.bincount(p[s],minlength=6).argmax()]))
for f,v in res.items(): print(f"{f:14s}", v)
