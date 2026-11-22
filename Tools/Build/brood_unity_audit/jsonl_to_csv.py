#!/usr/bin/env python3
"""Rebuild a Brood audit results.csv from its matches.jsonl (the harness writes both after every match; a write that
failed, e.g. on a full disk, leaves the jsonl as the record). Same columns the summary reads.

    py -3 Tools/Build/brood_unity_audit/jsonl_to_csv.py <matches.jsonl> <results.csv>
"""
import csv
import json
import sys

COLS = ["match", "seed", "teamA", "teamB", "teamC", "park_host", "stock_card", "difficulty", "outcome", "race_s",
        "winner", "broods", "seat_name", "hull", "requested_on_seat", "domain", "is_host", "vc", "vd", "vc_vd", "lost",
        "held", "super_shields", "team_broods", "trail_volume", "start_levels", "speed", "nucleus_s", "blocks",
        "volume_created", "stolen", "destroyed", "crystals", "ability_s", "seat_difficulty", "pilots", "pilots_at_end",
        "ability_starts", "abilities", "serpent_walls_max", "serpent_bricks_max", "fault", "notes"]


def main(src, dst):
    with open(dst, "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(COLS)
        for line in open(src, encoding="utf-8"):
            if not line.strip():
                continue
            m = json.loads(line)
            for s in m.get("seats", []):
                w.writerow([m["index"], m["seed"], m["teamA"], m["teamB"], m.get("teamC", ""), m["parkHost"],
                            m["stockCard"], m["difficulty"], m["outcome"], m["raceS"], m["winner"], m["broods"],
                            s["name"], s["hull"], s["requestedOnSeat"], s["domain"], s["isHost"], s["vc"], s["vd"],
                            s["vc"] + s["vd"], s["lost"], s["held"], s["superShields"], s["teamBroods"],
                            s["trailVolume"], s["startLevels"], s["speed"], s["nucleusS"], s["blocks"],
                            s["volumeCreated"], s["stolen"], s["destroyed"], s["crystals"], s["abilityS"],
                            s["difficulty"], s["pilots"], s["pilotsAtEnd"], s.get("abilityStarts", 0),
                            s.get("abilities", ""), m["serpentWallsMax"], m["serpentBricksMax"], m["fault"], m["notes"]])


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
