using UnityEngine;
using System;
using CosmicShore.Game;

namespace CosmicShore.Core
{
    public class BlockTeamManager : MonoBehaviour
    {
        private TrailBlock trailBlock;
        private MaterialPropertyAnimator materialAnimator;
        private Teams currentTeam = Teams.Unassigned;

        public Teams Team
        {
            get => currentTeam;
            private set
            {
                if (currentTeam != value)
                {
                    var oldTeam = currentTeam;
                    currentTeam = value;
                    OnTeamChanged?.Invoke(oldTeam, value);
                }
            }
        }

        public event Action<Teams, Teams> OnTeamChanged;

        private void Awake()
        {
            trailBlock = GetComponent<TrailBlock>();
            materialAnimator = GetComponent<MaterialPropertyAnimator>();
            OnTeamChanged += HandleTeamChange;
        }

        public void SetInitialTeam(Teams team)
        {
            if (currentTeam == Teams.Unassigned)
            {
                Team = team;
                materialAnimator.UpdateMaterial(
                    ThemeManager.Instance.GetTeamTransparentBlockMaterial(team),
                    ThemeManager.Instance.GetTeamBlockMaterial(team)
                );
            }
        }

        public void ChangeTeam(Teams newTeam)
        {
            if (Team != newTeam)
            {
                Team = newTeam;
            }
        }

        public void Steal(string playerName, Teams newTeam, bool superSteal)
        {
            if (Team != newTeam)
            {
                if (!superSteal && (trailBlock.TrailBlockProperties.IsShielded || trailBlock.TrailBlockProperties.IsSuperShielded))
                {
                    trailBlock.DeactivateShields();
                    return;
                }

                playerName = playerName != null ? playerName : "No name";

                // TODO - Raise events about steal.
                if (StatsManager.Instance != null)
                {
                    StatsManager.Instance.BlockStolen(newTeam, playerName, trailBlock.TrailBlockProperties);
                }

                if (NodeControlManager.Instance != null)
                {
                    NodeControlManager.Instance.StealBlock(newTeam, trailBlock.TrailBlockProperties);
                }

                ChangeTeam(newTeam);
                // trailBlock.Player = player;
            }
        }

        private void HandleTeamChange(Teams oldTeam, Teams newTeam)
        {
            if (trailBlock.TrailBlockProperties.IsDangerous)
            {
                materialAnimator.UpdateMaterial(
                    ThemeManager.Instance.GetTeamTransparentDangerousBlockMaterial(newTeam),
                    ThemeManager.Instance.GetTeamDangerousBlockMaterial(newTeam)
                );
            }
            else if (trailBlock.TrailBlockProperties.IsShielded)
            {
                materialAnimator.UpdateMaterial(
                    ThemeManager.Instance.GetTeamTransparentShieldedBlockMaterial(newTeam),
                    ThemeManager.Instance.GetTeamShieldedBlockMaterial(newTeam)
                );
            }
            else if (trailBlock.TrailBlockProperties.IsSuperShielded)
            {
                materialAnimator.UpdateMaterial(
                    ThemeManager.Instance.GetTeamTransparentSuperShieldedBlockMaterial(newTeam),
                    ThemeManager.Instance.GetTeamSuperShieldedBlockMaterial(newTeam)
                );
            }
            else
            {
                materialAnimator.UpdateMaterial(
                    ThemeManager.Instance.GetTeamTransparentBlockMaterial(newTeam),
                    ThemeManager.Instance.GetTeamBlockMaterial(newTeam)
                );
            }
        }
    }
}
