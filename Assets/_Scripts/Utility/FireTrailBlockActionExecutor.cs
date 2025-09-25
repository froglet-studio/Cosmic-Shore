using System.Collections;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;
using UnityEngine.Serialization;

public sealed class FireTrailBlockActionExecutor : ShipActionExecutorBase
{
    [FormerlySerializedAs("trailBlockPrefab")]
    [Header("Scene Refs")]
    [SerializeField] private Prism prismPrefab; 
    [SerializeField] private Transform muzzle;           
    [SerializeField] private PoolManager pool;     

    IVesselStatus _status;
    Coroutine _loop;

    public override void Initialize(IVesselStatus shipStatus)
    {
        _status = shipStatus;
    }

    public void Begin(FireTrailBlockActionSO so)
    {
        if (_loop != null) return;
        _loop = StartCoroutine(FireLoop(so));
    }

    public void End()
    {
        if (_loop == null) return;
        StopCoroutine(_loop);
        _loop = null;
    }

    IEnumerator FireLoop(FireTrailBlockActionSO so)
    {
        var interval = 1f / Mathf.Max(0.01f, so.FiringRate);

        while (true)
        {
            FireBlock(so);
            yield return new WaitForSeconds(interval);
        }
    }

    void FireBlock(FireTrailBlockActionSO so)
    {
        if (prismPrefab == null || muzzle == null) return;

        Prism blockInstance = null;
        if (pool != null)
        {
            var go = pool.SpawnFromPool(prismPrefab.tag, muzzle.position, muzzle.rotation);
            blockInstance = go?.GetComponent<Prism>();
        }
        else
        {
            blockInstance = Instantiate(prismPrefab, muzzle.position, muzzle.rotation);
        }

        if (blockInstance == null) return;

        blockInstance.TargetScale *= so.ProjectileScale;
        blockInstance.prismProperties.IsShielded = so.Shielded;
        blockInstance.prismProperties.IsDangerous = so.FriendlyFire; 
        blockInstance.Domain = _status.Domain; 
        blockInstance.PlayerName = _status.PlayerName;
        
        StartCoroutine(MoveBlockForward(blockInstance, so));

        Destroy(blockInstance.gameObject, so.ProjectileTime);
    }
    
    IEnumerator MoveBlockForward(Prism block, FireTrailBlockActionSO so)
    {
        float t = 0f;
        Transform tf = block.transform;

        while (t < so.ProjectileTime && block != null)
        {
            tf.position += tf.forward * (so.ProjectileSpeed * Time.deltaTime);
            t += Time.deltaTime;
            yield return null;
        }

        if (block)
            Destroy(block.gameObject);
    }

}