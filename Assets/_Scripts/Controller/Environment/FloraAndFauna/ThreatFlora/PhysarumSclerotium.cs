using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A SCLEROTIUM (round 11c, Docs/THREAT_FLORA.md §3) - one heart of the cell's physarum network. The network
    /// itself (agents, trail field, tubes, the peristaltic danger wave) is one <see cref="PhysarumCore"/> owned by
    /// the cell's <see cref="ThreatGrove"/>; each sclerotium is a lifeform in it with ONE crystal, a pacemaker that
    /// fires a pulse down its nearest tube every Period, and a six-prism BEAT SHELL around the crystal that glows
    /// ahead of each beat and stings (danger tier) for BeatOn after it - the crystal is taken by diving in between
    /// beats. It climbs the slow trail gradient at HeartSpeed, so the crystal ends up inside the pulsing cables.
    ///
    /// <para><b>Ecology.</b> A seeded sclerotium brings its planted share of the network (tube bodies plus its own
    /// shell). The shell is paid from the network's reserve and re-laid only from it. While the reserve holds a whole
    /// shell, a living sclerotium buds a daughter through the platform's one offspring path
    /// (<see cref="Flora.TrySpawnOneOffspring"/>, so the cell's cap and the Frenzy freeze apply); she brings no mass
    /// and lays her shell from the reserve, so the network turns what it digested into a new heart. Tubes belong to
    /// the network, not to a heart: a heart that is jousted stops beating and climbing (its agents freeze, as in the
    /// research), its shell stays as a skeleton, and the cables it fed are resorbed only while another heart lives.</para>
    /// </summary>
    public class PhysarumSclerotium : Flora
    {
        [Header("Threat grove")]
        [Tooltip("The grove this network belongs to (shared with the cell's snap traps). Its tube and shell leaves " +
                 "size the network's prisms; its sector says where a seeded sclerotium is planted.")]
        [SerializeField] ThreatGroveConfigSO grove;

        ThreatGrove _grove;
        int _heart = -1;
        Spindle _limb;
        bool _budded;   // set on a daughter by her parent (ConfigureOffspring) before Initialize
        readonly HealthPrism[] _shell = new HealthPrism[ThreatGroveDefaults.ShellPrisms];
        bool _beat;

        static readonly Vector3[] ShellDirs =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
        };

        float ShellPrismVolume => grove ? grove.ShellLeaf.x * grove.ShellLeaf.y * grove.ShellLeaf.z : 0f;

        protected override int PrismBudget => ThreatGroveDefaults.ShellPrisms;
        protected override bool PrismSizeFixedByGrowthRule => true;

        public override void Plant()
        {
            _grove = ThreatGrove.For(cell, grove);
            if (!_grove)
            {
                CSDebug.LogWarning($"{name}: PhysarumSclerotium needs a cell and a ThreatGroveConfigSO; it was planted without one and will not grow.");
                return;
            }
            Vector3 at = TryGetPlantPositionOverride(out var pinned) ? pinned : _grove.PickSclerotiumSite();
            _heart = _grove.RegisterSclerotium(this, at, healthPrism, _budded);
            transform.position = _grove.HeartPosition(_heart);
            LayShell();
        }

        /// <summary>Re-lay any missing shell prism from the network's reserve (never from nothing).</summary>
        public override void Grow()
        {
            if (_grove && _heart >= 0 && !IsDying) LayShell();
        }

        /// <summary>The network's reserve holds a whole shell: bud a daughter (ThreatGrove.BudSclerotium).</summary>
        public void TryBud()
        {
            if (_grove && _heart >= 0 && !IsDying) TrySpawnOneOffspring();
        }

        /// <summary>A daughter is planted where a seeded sclerotium would be (inside the grove); she climbs the trail
        /// from there like every heart.</summary>
        protected override bool TryResolveOffspringPlacement(out Vector3 position, out Quaternion rotation, out Vector3? up)
        {
            position = _grove ? _grove.PickSclerotiumSite() : transform.position;
            rotation = transform.rotation;
            up = null;
            return true;
        }

        protected override void ConfigureOffspring(Flora child)
        {
            if (child is PhysarumSclerotium daughter) daughter._budded = true;
        }

        void LayShell()
        {
            if (!healthPrism) return;
            float v = ShellPrismVolume;
            for (int k = 0; k < _shell.Length; k++)
            {
                if (_shell[k]) continue;
                if (!_grove.SpendOnShell(v)) return;
                var prism = LayShellPrism(k);
                if (!prism) { _grove.ShellRefund(v); return; }
                _shell[k] = prism;
                if (_beat) ThreatGrove.SetDanger(prism, true);
            }
        }

        HealthPrism LayShellPrism(int k)
        {
            if (!_limb)
            {
                _limb = AddSpindle();
                _limb.transform.SetPositionAndRotation(transform.position, transform.rotation);
            }
            Vector3 pos = transform.position + ShellDirs[k] * grove.ShellRadius;
            HealthPrism prism = EnvironmentPrismPool.Get(healthPrism, pos, Quaternion.LookRotation(ShellDirs[k]));
            if (!prism) return null;
            AddHealthBlock(prism);
            prism.AdmitTargetScale(grove.ShellLeaf);
            prism.TargetScale = grove.ShellLeaf;
            prism.transform.SetParent(_limb.transform, true);
            prism.LifeForm = this;
            prism.Initialize("flora");
            NotifyGrew();
            return prism;
        }

        /// <summary>The beat's telegraph: the shell glows into the domain's danger colours BeatGlow ahead of it.</summary>
        public void BeatGlow(float duration)
        {
            foreach (var p in _shell) if (p) _grove.StampGlow(p, true, duration);
        }

        /// <summary>The beat: the shell is a danger prism for BeatOn, then plain again.</summary>
        public void SetBeat(bool on)
        {
            _beat = on;
            foreach (var p in _shell)
            {
                if (!p) continue;
                ThreatGrove.SetDanger(p, on);
                if (!on) _grove.StampGlow(p, false, 0.3f);
            }
        }

        /// <summary>The heart climbed (one event per materialize pass, a few units): the crystal and its shell move
        /// together, and each shell prism flies the hop on the GPU clock.</summary>
        public void MoveTo(Vector3 position, float duration)
        {
            if (IsDying) return;
            Vector3 delta = position - transform.position;
            if (delta.sqrMagnitude < 1e-6f) return;
            transform.position = position;
            foreach (var p in _shell)
            {
                if (!p) continue;
                Vector3 end = p.transform.position;
                p.transform.position = end - delta;
                ThreatGrove.PoseAndFly(p, end, p.transform.rotation, duration);
            }
        }

        public override void RemoveHealthBlock(HealthPrism healthPrism, string killerName = "")
        {
            for (int k = 0; k < _shell.Length; k++)
            {
                if (_shell[k] != healthPrism || !healthPrism) continue;
                _shell[k] = null;
                if (_grove) _grove.ShellLost(ShellPrismVolume);
            }
            base.RemoveHealthBlock(healthPrism, killerName);
        }

        protected override void Die(string killerName = "")
        {
            if (_grove && _heart >= 0)
            {
                _grove.SclerotiumDied(_heart);
                // the standing shell leaves the network's ledger as cell mass (skeleton or debris, by death style)
                for (int k = 0; k < _shell.Length; k++)
                {
                    if (!_shell[k]) continue;
                    _shell[k] = null;
                    _grove.ShellSkeleton(ShellPrismVolume);
                }
            }
            _heart = -1;
            base.Die(killerName);
        }

        protected override void OnDestroy()
        {
            if (_grove && _heart >= 0) _grove.SclerotiumDied(_heart);
            _heart = -1;
            base.OnDestroy();
        }
    }
}
