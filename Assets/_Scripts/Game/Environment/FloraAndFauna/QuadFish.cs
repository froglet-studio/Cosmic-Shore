using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    public class QuadFish : Fauna
    {
        public override void Initialize(Cell cell) { }

        protected override void Spawn() { }

        protected override void Die(string killername = "")
        {
            Destroy(gameObject);
        }
    }
}
