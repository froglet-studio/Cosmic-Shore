namespace CosmicShore.Game.StateSystem
{
    public class ShipStateMachine : StateManager<ShipStateMachine.ShipState>
    {
        public enum ShipState
        {
            Boost,
            Discharge,
            Drift,
            Turret,
            SingleControl,
            Projectile,
            Stationary,
            GainAmmo,
            AutoPilot,
            Align,
            Attached,
            GunActive
        }

        void Awake()
        {
            // Could Create any other ship state class inherited from BaseState to put each contained logics there
            // And store those in States
            CurrentState = States[ShipState.Drift];
        }
        
        // Do other ship logic in update
    }
}
