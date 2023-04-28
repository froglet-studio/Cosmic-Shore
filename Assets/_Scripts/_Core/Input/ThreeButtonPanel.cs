using StarWriter.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class ThreeButtonPanel : MonoBehaviour
{
    Dictionary<int, List<ShipActionAbstractBase>> ButtonActions;
    [SerializeField] List<ShipActionAbstractBase> Button1Actions;
    [SerializeField] List<ShipActionAbstractBase> Button2Actions;
    [SerializeField] List<ShipActionAbstractBase> Button3Actions;

    Ship ship;
    float abilityStartTime;

    void Start()
    {
        ButtonActions = new Dictionary<int , List<ShipActionAbstractBase>> 
        {
                { 1, Button1Actions},
                { 2, Button2Actions },
                { 3, Button3Actions },
        };

        foreach (var key in ButtonActions.Keys)
            foreach (var shipAction in ButtonActions[key])
                shipAction.Ship = ship;

    }

    public void PerformButtonActions(int buttonNumber)
    {
        abilityStartTime = Time.time;
        var buttonActions = ButtonActions[buttonNumber];
        foreach (var action in buttonActions)
            action.StartAction();
    }

    public void StopButtonActions(int buttonNumber)
    {
        // TODO: p1 ability activation tracking doesn't work - needs to have separate time keeping for each control type
        if (StatsManager.Instance != null)
            StatsManager.Instance.AbilityActivated(ship.Team, ship.Player.PlayerName, InputEvents.ButtonAction, Time.time - abilityStartTime);

        var buttonActions = ButtonActions[buttonNumber];
        foreach (var action in buttonActions)
            action.StopAction();
    }

}
