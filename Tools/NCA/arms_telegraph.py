"""Telegraph numbers for results/arms/eval.json: for every catch (3 seeds x 60 s), was the catcher BURSTING in the
second before it (the burst is drawn red in the viewer: the visible tell), how long before the catch that burst
began, and how long the victim had been inside the catcher's perception radius.

    python Tools/NCA/arms_telegraph.py
"""
import sys, json; sys.path.insert(0,'.')
import numpy as np, arms_eval as E, arms_sim as A
sn=E.load_snaps('runs/arms_a3'); cfg=A.Cfg(); out={}
for g in (0,1460):
    burst_share, onset, stalk, n = [], [], [], 0
    for s in (11,12,13):
        rec=E.record(cfg, sn[g]['thq'], sn[g]['thp'], s, 60.0)
        for (t,p,q,*_x) in rec['events']:
            ti=int(round(t/cfg.dt)); n+=1
            b=rec['pb'][:ti+1,p]
            burst_share.append(b[max(0,ti-10):ti+1].any())
            if b[max(0,ti-10):ti+1].any():
                k=ti
                while k>0 and not b[k]: k-=1
                while k>0 and b[k-1]: k-=1
                onset.append((ti-k)*cfg.dt)
            d=np.linalg.norm(rec['pp'][:ti,p]-rec['qp'][:ti,q],axis=1)
            k=ti-1
            while k>0 and d[k]<cfg.pred_R: k-=1
            stalk.append((ti-1-k)*cfg.dt)
    out[f'{g}:{g}']=dict(catches=n, burst_before_catch_share=round(float(np.mean(burst_share)),3),
        burst_onset_to_catch_s_median=round(float(np.median(onset)),2), burst_onset_to_catch_s_mean=round(float(np.mean(onset)),2),
        victim_in_catcher_view_s_median=round(float(np.median(stalk)),2))
    print(g,out[f'{g}:{g}'])
p='results/arms/eval.json'; ev=json.load(open(p)); ev['telegraph']=out; json.dump(ev,open(p,'w'),indent=1)
