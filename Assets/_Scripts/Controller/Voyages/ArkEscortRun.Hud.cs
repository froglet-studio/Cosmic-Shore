namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the escort shows on the voyage HUD (Frogma Rev 113, Hud): the objective box (<see cref="VoyageObjective"/>:
    /// the goal and its breach line, the worlds as route stops, the hull, where the Ark is and the assault on it), the
    /// Ark's alarms (<see cref="ArkAlarmToasts"/>) and the marks on the fauna attacking it (<see cref="ArkAttackerMarks"/>). Facts only; the words are the voyage's authored lines.
    /// </summary>
    public sealed partial class ArkEscortRun : IVoyageObjectiveSource
    {
        ArkAlarmToasts _alarms;
        ArkAttackerMarks _attackerMarks;

        void ShowOnHud()
        {
            VoyageObjective.Source = this;
            (_alarms ??= new ArkAlarmToasts(this, () => _sea)).Hook();
            if (!_attackerMarks) _attackerMarks = ArkAttackerMarks.Create(transform, this, () => Ark);
        }

        void LeaveHud()
        {
            _alarms?.Unhook();
            if (_attackerMarks) Destroy(_attackerMarks.gameObject);
            _attackerMarks = null;
            if (ReferenceEquals(VoyageObjective.Source, this)) VoyageObjective.Source = null;
        }

        public bool TryGetObjective(out VoyageObjective objective)
        {
            objective = default;
            if (!_active || !_escort || _escort.Legs.Count == 0 || _report == null) return false;
            var s = Status;
            int legs = _escort.Legs.Count;
            objective.Goal = VoyageObjective.Kind.Escort;
            objective.Difficulty = _difficulty;
            objective.Hull = s.HullTotal > 0 ? s.Hull : 1f;
            objective.Breach = s.BreachLine;
            objective.Cells = legs;
            objective.Cell = _leg >= 0 ? _leg + 1 : 0;
            objective.Stops = new string[legs];
            objective.HostileCells = new bool[legs];
            for (int i = 0; i < legs; i++)
            {
                objective.Stops[i] = WorldNameFor(i);
                objective.HostileCells[i] = i < _report.LegReports.Count && _report.LegReports[i].Hostile
                                            && !_report.LegReports[i].Pacified;
            }
            objective.To = objective.Stops[legs - 1];
            objective.World = s.WorldName;
            objective.Hostile = s.WorldHostile && !s.WorldPacified;
            objective.TakeArk(s);
            return true;
        }
    }
}
