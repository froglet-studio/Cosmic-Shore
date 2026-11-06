# python3 hz.py <nseeds> [give-json]: each hazard alone vs none, same seeds, no cards (or the given cards)
import os, sys; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pool, statistics, json
n=int(sys.argv[1]); give=json.loads(sys.argv[2]) if len(sys.argv)>2 else {}
HZ=['none','gunners','drift','keys','reefs']
jobs=[]
for h in HZ:
  H={k:(k==h) for k in HZ[1:]}
  for i in range(n): jobs.append({'id':[h,i],'opts':{'seed':1000+i,'intensity':1+i%4,'hazards':H,'give':give}})
rows=pool.run('hz',jobs,{})
base={r['id'][1]:r['time'] for r in rows if r['id'][0]=='none'}
for h in HZ:
  rr=[r for r in rows if r['id'][0]==h]
  print(f"{h:8s} {statistics.mean(r['time'] for r in rr):6.1f}s  x{statistics.mean(r['time']/base[r['id'][1]] for r in rr):.2f}  lost {statistics.mean(r['lost'] for r in rr):.0f}  by I: "+' '.join('%.2f'%statistics.mean(r['time']/base[r['id'][1]] for r in rr if r['opts']['intensity']==i) for i in range(1,5)))
