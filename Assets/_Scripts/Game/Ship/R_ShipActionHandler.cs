using CosmicShore.Game;
using CosmicShore.Game.IO;
using CosmicShore.Models.Enums;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Component responsible for mapping input and resource events to
    /// ship actions.  This logic previously lived inside the Ship classes.
    /// </summary>
    public class R_ShipActionHandler : MonoBehaviour
    {
        [SerializeField] List<InputEventShipActionMapping> _inputEventShipActions;
        [SerializeField] List<ResourceEventShipActionMapping> _resourceEventClassActions;

        readonly Dictionary<InputEvents, List<ShipAction>> _shipControlActions = new();
        readonly Dictionary<ResourceEvents, List<ShipAction>> _classResourceActions = new();

        IShip _ship;

        public void Initialize(IShip ship)
        {
            _ship = ship;
            ShipHelper.InitializeShipControlActions(ship, _inputEventShipActions, _shipControlActions);
            ShipHelper.InitializeClassResourceActions(ship, _resourceEventClassActions, _classResourceActions);
        }

        public void Perform(InputEvents ev)
        {
            ShipHelper.PerformShipControllerActions(ev, out _, _shipControlActions);
        }

        public void Stop(InputEvents ev)
        {
            ShipHelper.StopShipControllerActions(ev, _shipControlActions);
        }
    }
}
