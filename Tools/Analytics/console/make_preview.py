#!/usr/bin/env python3
"""Build a review copy of the Froglet Analytics console that needs no server: the real page, CSS and JS, with a
snapshot of the server's answers baked in. Made for a claude.ai artifact link (the person reviews the console before
the VM exists), so the output is page content without <html>/<head>/<body>, as the artifact publisher wraps it.

    python3 Tools/Analytics/console/make_preview.py --out <file.html>               invented sample players and matches
    python3 Tools/Analytics/console/make_preview.py --data <data_dir> --out <file>  a real server's data dir instead

The sample is INVENTED and the page says so on every view: every id starts with "sample-" and every display name ends
in "(sample)". Its shape follows what Ian measured on 2026-10-29 (about 25 release players, editor play the bulk of
matches, a port build that rarely finishes, every event sent twice until a build with the #1639 fix), and it uses
every event the game sends so each page has something to show. The overnight read is the rules' version (no Claude
key here). The Ask page carries answers for its three example questions only: each runs the real guarded query on the
sample aggregates, with the sentence written from the rows by this script, not by Claude; the page says so.

Do not run --data on a real server's data for a link anyone else can open: a review copy holds every profile it shows.
"""
import argparse
import json
import os
import random
import sys
import tempfile
from datetime import datetime, timedelta, timezone

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "server"))

import analytics_claude as C  # noqa: E402
import analytics_server as A  # noqa: E402
import analytics_store as S  # noqa: E402
import analytics_views as V  # noqa: E402

BUILDS = ("release", "editor", "port", "development")
PAGE_DAYS = (7, 30, 90, 365)
HEALTH_DAYS = (14, 30, 90)
LABELS = {"solo_vs_ai": "Solo vs AI", "solo_alone": "Solo", "coop": "Co-op", "adversarial": "Versus",
          "mixed": "Mixed teams", "multi_human_unknown": "Multi-human (teams not recorded)"}
FONTS = "https://fonts.googleapis.com/css2?family=Aldrich&family=Chakra+Petch:wght@500;600;700&display=swap"
CALLSIGNS = ["Tidewalker", "Lumen", "Brine", "Kestrel", "Halcyon", "Mothwing", "Brightwake", "Nova Kite", "Driftglass",
             "Saltmarsh", "Coral Fox", "Reefrunner", "Inkcap", "Starling", "Pelagic", "Glint", "Undertow Ann", "Skerry",
             "Marlin Jo", "Ebb", "Lanternfish", "Spindrift", "Quill", "Fathom", "Weft"]
COUNTRIES = ["US", "US", "US", "GB", "GB", "IN", "DE", "SE", "BR", "CA", "PH", "AU", "FR", "NL", "PL", "MX"]
FTUE_STEPS = [(0, "wake", "Wake the vessel"), (0, "steer", "Steer through the rings"), (1, "boost", "Boost on a trail"),
              (1, "crystal", "Take a crystal"), (2, "first_match", "Fly a first match"), (2, "hangar", "Open the hangar")]
MODES = ["SkimRace", "Joust", "CrystalCapture", "Bloomrush", "HexRace", "Regatta", "BroodRush", "Scurry"]
SERVER_LIVE_DAYS = 1         # events older than this came in with the PostHog import (stored as history)
NEW_BUILD_DAYS = 8           # builds this recent send element, the Toybox's vessel, freestyle_ended and vessel switches
IDENTIFY_DAYS = 25           # builds since 12 Oct re-identify after each Toybox flight (selected_vessel)
DOMAIN_DAYS = 3              # builds this recent send the match setup (domain, humans_on_*_domain(s), ai_on_*_domain(s))
VESSELS = ["Squirrel", "Sparrow", "Manta", "Dolphin", "Rhino", "Urchin"]


def sample_events(seed=7):
    """Invented players and matches. Returns {player_id: (country, events)} so each player's batch is ingested with
    their country, as the server does from the sender's address."""
    rng = random.Random(seed)
    now = datetime.now(timezone.utc)
    players = {}
    table = S.mode_elements()
    selected = {}

    def iso(t):
        return t.strftime("%Y-%m-%dT%H:%M:%S.") + f"{t.microsecond // 1000:03d}Z"

    def send(player, event, t, platform, version, **props):
        base = {"distinct_id": player, "install_id": "dev-" + player, "player_id": player, "app_version": version,
                "platform": platform, "schema_version": 1}
        base.update(props)
        players[player][1].append({"event": event, "properties": base, "timestamp": iso(t)})

    def session(player, start, platform, version, double, finish_rate, humans=1, team=None, first=False, wallet=None):
        t = start
        launch = {"Android": 14, "IPhonePlayer": 9, "WindowsPlayer": 6, "WindowsEditor": 4}.get(platform, 7)
        if first:
            send(player, "game_first_launched", t, platform, version)
        send(player, "menu_ready", t + timedelta(seconds=launch), platform, version, is_first=first,
             seconds_since_launch=round(launch * rng.uniform(0.7, 2.2)))
        t += timedelta(seconds=launch + 20)
        if first:
            reached = rng.choice([2, 3, 4, 5, 6, 6, 6])
            for i, (phase, step, name) in enumerate(FTUE_STEPS[:reached]):
                send(player, "ftue_step_completed", t + timedelta(seconds=40 * (i + 1)), platform, version,
                     quest_id="FirstFlight", phase_index=phase, step_id=step, step_name=name,
                     seconds_since_launch=launch + 40 * (i + 1) + rng.randint(0, 30))
            if reached == len(FTUE_STEPS):
                send(player, "ftue_completed", t + timedelta(seconds=40 * reached + 5), platform, version,
                     quest_id="FirstFlight", seconds_since_launch=launch + 40 * reached + 5)
            t += timedelta(seconds=40 * reached + 30)
        if rng.random() < 0.55:                       # the Toybox (Freestyle) before the matches
            new = start > now - timedelta(days=NEW_BUILD_DAYS)
            vessel = selected.get(player) or rng.choice(VESSELS)
            send(player, "freestyle_entered", t, platform, version, is_first=True, seconds_since_launch=launch + 25,
                 **({"vessel_class": vessel} if new else {}))
            stretch, flown = rng.randint(40, 420), {vessel}
            for k in range(rng.choice([0, 0, 1, 1, 2, 3])):
                to = rng.choice([v for v in VESSELS if v != vessel])
                at = t + timedelta(seconds=int(stretch * (k + 1) / 4))
                if new:
                    send(player, "freestyle_vessel_changed", at, platform, version, vessel_class=to,
                         from_vessel_class=vessel, seconds_in_freestyle=round(stretch * (k + 1) / 4, 1))
                elif start > now - timedelta(days=IDENTIFY_DAYS):     # the identify history keeps the pick
                    identify_at(player, at, to)
                vessel = to
                flown.add(to)
            selected[player] = vessel
            if new:
                send(player, "freestyle_ended", t + timedelta(seconds=stretch), platform, version, seconds=stretch,
                     vessel_class=vessel, vessels_flown=len(flown), seconds_since_launch=launch + 25 + stretch)
            t += timedelta(seconds=stretch + 15)
        if rng.random() < 0.15:
            send(player, "setting_changed", t, platform, version, setting=rng.choice(["invert_y", "haptics_enabled",
                 "music_level"]), value=rng.choice(["true", "false", "0.6"]), seconds_since_launch=launch + 30)
        for _ in range(rng.randint(1, 4)):
            match = f"m-{player}-{t.timestamp():.0f}"
            mode = rng.choice(MODES)
            props = dict(match_id=match, player_count_human=humans, player_count_ai=0 if rng.random() < 0.3 else
                         rng.randint(1, 3), game_mode=mode, intensity=rng.randint(1, 4), vessel_class=rng.choice(VESSELS),
                         **(team or {}))
            if start > now - timedelta(days=NEW_BUILD_DAYS) and mode in table:   # new builds send the element
                props["element"] = table[mode][0]
                if table[mode][1]:
                    props["element_secondary"] = table[mode][1]
            if start > now - timedelta(days=DOMAIN_DAYS) and humans == 1:      # ... and the solo setup
                ai = props["player_count_ai"]
                teammates = 1 if ai >= 2 and rng.random() < 0.35 else 0
                props.update(domain="Jade", humans_on_my_domain=1, humans_on_other_domains=0,
                             ai_on_my_domain=teammates, ai_on_other_domains=ai - teammates)
            send(player, "game_started", t, platform, version, **props)
            if double:
                send(player, "game_started", t + timedelta(milliseconds=2), platform, version, **props)
            length = rng.randint(80, 200)
            if rng.random() >= finish_rate:
                if rng.random() < 0.3:
                    send(player, "repeated_game_fail", t + timedelta(seconds=length), platform, version,
                         game_mode=mode, fail_count=rng.randint(3, 6))
                break
            paused = rng.choice([0, 0, 0, 5, 12, 45])
            send(player, "game_completed", t + timedelta(seconds=length), platform, version, match_id=match,
                 flight_time_seconds=length - paused - rng.randint(1, 5), duration_seconds=length,
                 player_won=rng.random() < 0.45, game_mode=mode)
            if wallet is not None:
                amount = rng.choice([50, 100, 150, 200])
                wallet[0] += amount
                send(player, "crystals_earned", t + timedelta(seconds=length + 1), platform, version,
                     amount=amount, source="game_placement", balance=wallet[0])
                if double and platform == "WindowsEditor" and rng.random() < 0.08:   # the October double payout
                    wallet[0] += amount
                    send(player, "crystals_earned", t + timedelta(seconds=length + 1, milliseconds=rng.randint(2, 27)),
                         platform, version, amount=amount, source="game_placement", balance=wallet[0])
                if wallet[0] >= 400 and rng.random() < 0.25:
                    vessel = rng.choice(VESSELS[2:])
                    wallet[0] -= 300
                    send(player, "vessel_unlocked", t + timedelta(seconds=length + 30), platform, version,
                         vessel=vessel, cost=300, balance=wallet[0])
                elif rng.random() < 0.12:
                    send(player, "crystal_spend_blocked", t + timedelta(seconds=length + 30), platform, version,
                         amount=300, item=rng.choice(["Manta", "Rhino", "Urchin"]), balance=wallet[0])
            if rng.random() < 0.08:
                send(player, "minigame_favorited", t + timedelta(seconds=length + 40), platform, version,
                     game_mode=mode, favorited=rng.random() < 0.85)
            if rng.random() < 0.04:
                send(player, "share_triggered", t + timedelta(seconds=length + 45), platform, version, game_mode=mode)
            if rng.random() < 0.3:
                send(player, "play_again_pressed", t + timedelta(seconds=length + 5), platform, version)
            t += timedelta(seconds=length + rng.choice([60, 120, 300, 2000]))
        if rng.random() < 0.02:
            send(player, "cloud_save_failed", t, platform, version, key=rng.choice(["HANGAR_DATA", "MODE_STATS"]))
        send(player, "session_ended", t + timedelta(seconds=30), platform, version,
             reason=rng.choice(["quit", "quit", "background", "background", "crash"]), app_state="menu")

    def party(t, seats, ai=None, rounds=2):
        """A party's matches from builds that send the setup: seats [(player, domain)] share one match id; `ai` is
        {domain: AI pilots on it}. Each player's game_started counts the sides from their own seat."""
        ai = ai or {}
        humans, bots = len(seats), sum(ai.values())
        for _ in range(rounds):
            match, mode, length = f"m-party-{seats[0][0]}-{t.timestamp():.0f}", rng.choice(MODES), rng.randint(90, 200)
            winner = rng.choice(seats)[1]
            for player, domain in seats:
                mine = sum(1 for _, d in seats if d == domain)
                send(player, "game_started", t, "WindowsPlayer", "0.2.1", match_id=match, player_count_human=humans,
                     player_count_ai=bots, game_mode=mode, intensity=rng.randint(1, 4), vessel_class=rng.choice(VESSELS),
                     domain=domain, humans_on_my_domain=mine, humans_on_other_domains=humans - mine,
                     ai_on_my_domain=ai.get(domain, 0), ai_on_other_domains=bots - ai.get(domain, 0))
                if rng.random() < 0.85:
                    paused = rng.choice([0, 0, 6])
                    send(player, "game_completed", t + timedelta(seconds=length), "WindowsPlayer", "0.2.1", match_id=match,
                         flight_time_seconds=length - paused - 2, duration_seconds=length, player_won=domain == winner,
                         game_mode=mode)
            t += timedelta(seconds=length + rng.choice([60, 120, 900]))

    def identify_at(player, t, vessel):
        players[player][1].append({"event": "$identify", "timestamp": iso(t), "properties": {
            "distinct_id": player, "$set": {"selected_vessel": vessel, "preferred_vessel": vessel}}})

    def identify(player, name, wallet):
        players[player][1].append({"event": "$identify", "timestamp": iso(now - timedelta(hours=1)), "properties": {
            "distinct_id": player, "$set": {"display_name": name, "avatar_id": rng.randint(1, 12),
                                            "crystal_balance": wallet[0], "invite_wave": "2026-10-12"}}})

    fixed = now - timedelta(days=6)          # pretend builds with the #1639 fix reached players 6 days ago
    wallets = {}
    for i in range(25):
        player = f"sample-player-{i:02d}"
        players[player] = (rng.choice(COUNTRIES), [])
        wallet = wallets.setdefault(player, [0])
        platform = rng.choice(["WindowsPlayer", "Android", "IPhonePlayer"])
        first = now - timedelta(days=rng.randint(1, 88))
        for n, day in enumerate([first] + [first + timedelta(days=d) for d in (1, 2, 5, 9) if rng.random() < 0.35]):
            if day > now:
                continue
            start = min(day.replace(hour=rng.randint(8, 22), minute=rng.randint(0, 59)), now - timedelta(hours=2))
            session(player, start, platform, "0.2.1", start < fixed, 0.63, first=n == 0, wallet=wallet)
    for i in range(4):                       # organised multi-human matches, with invites and friends
        t = now - timedelta(days=rng.randint(1, 20), hours=rng.randint(1, 8))
        coop = i % 2 == 0
        host, guest = f"sample-player-{i:02d}", f"sample-player-{i + 10:02d}"
        send(host, "party_invite_sent", t - timedelta(minutes=2), "WindowsPlayer", "0.2.1", target_id=guest)
        send(guest, "party_invite_received", t - timedelta(minutes=2), "WindowsPlayer", "0.2.1", host_id=host)
        send(guest, "party_joined", t - timedelta(minutes=1), "WindowsPlayer", "0.2.1")
        if i < 3:
            send(host, "friend_request_sent", t + timedelta(minutes=20), "WindowsPlayer", "0.2.1", target_id=guest)
            send(guest, "friend_request_received", t + timedelta(minutes=20), "WindowsPlayer", "0.2.1", from_id=host)
            if i < 2:
                send(guest, "friend_added", t + timedelta(minutes=21), "WindowsPlayer", "0.2.1", friend_id=host)
        for player in (host, guest):                 # older builds: no domain fields, so "teams not recorded"
            session(player, t, "WindowsPlayer", "0.2.1", False, 0.9, humans=2, wallet=wallets[player],
                    team={"humans_on_my_domain": 2 if coop else 1, "humans_on_other_domains": 0 if coop else 1}
                    if t > now - timedelta(days=DOMAIN_DAYS) else None)
    # parties on builds that send the setup: 1v1, a three-way free-for-all, 2v2, two against the AI, two against one
    p = lambda i: f"sample-player-{i:02d}"
    for hours, seats, ai in ((30, [(p(3), "Jade"), (p(13), "Ruby")], {"Gold": 2}),
                             (8, [(p(9), "Jade"), (p(19), "Ruby"), (p(21), "Gold")], None),
                             (52, [(p(4), "Jade"), (p(5), "Jade"), (p(14), "Ruby"), (p(15), "Ruby")], None),
                             (20, [(p(6), "Jade"), (p(16), "Jade")], {"Ruby": 3}),
                             (5, [(p(6), "Jade"), (p(16), "Jade")], {"Ruby": 2, "Jade": 1}),
                             (44, [(p(7), "Jade"), (p(8), "Jade"), (p(17), "Ruby")], {"Ruby": 1})):
        party(now - timedelta(hours=hours), seats, ai, rounds=rng.choice([1, 2]))
    for i in range(6):                       # more multiplayer from before the setup was sent: most of it, as live data is
        t = now - timedelta(days=rng.randint(4, 60), hours=rng.randint(1, 8))
        for player in (p(i + 18), p(i + 2)):
            session(player, t, "WindowsPlayer", "0.2.1", t < fixed, 0.85, humans=2, wallet=wallets[player])
    for i in range(20, 25):                  # release players on builds that send the solo setup (AI teammates too)
        session(p(i), now - timedelta(hours=rng.randint(3, 60)), "Android", "0.2.1", False, 0.7, wallet=wallets[p(i)])
    for i in range(25):
        identify(f"sample-player-{i:02d}", CALLSIGNS[i] + " (sample)", wallets[f"sample-player-{i:02d}"])
    for i in range(6):                       # the team in the editor
        player = f"sample-dev-{i}"
        players[player] = ("SE" if i < 3 else "IN", [])
        wallet = [0]
        for d in range(0, 88, 2):
            start = now - timedelta(days=d, hours=rng.randint(1, 10))
            session(player, start, "WindowsEditor", "0.2.1", start < fixed, 0.7, first=d == 86, wallet=wallet)
        identify(player, f"Dev {i + 1} (sample)", wallet)
    for i in range(5):                       # the port build
        player = f"sample-port-{i}"
        players[player] = ("US", [])
        session(player, now - timedelta(days=rng.randint(1, 30)), "WindowsPlayer", "0.2.0-port", False, 0.03, first=True)
    players["sample-player-01"][1].append({"event": "game_startd", "properties": {"distinct_id": "sample-player-01"},
                                           "timestamp": iso(now)})
    return players


ASKS = [
    ("Which play type brings players back for a rematch?",
     "SELECT play_type, COUNT(*) AS completed, SUM(rematched) AS rematches,\n"
     "       ROUND(100.0 * SUM(rematched) / COUNT(*), 1) AS rematch_pct\n"
     "FROM plays\nWHERE build_kind = 'release' AND completed = 1\nGROUP BY play_type\nORDER BY rematch_pct DESC",
     lambda r: f"{LABELS.get(r[0][0], r[0][0])} brings players back most: {r[0][3]}% of completed matches were "
               f"followed by another within 10 minutes ({r[0][2]} of {r[0][1]}). "
               f"{LABELS.get(r[-1][0], r[-1][0])} is lowest at {r[-1][3]}%."),
    ("Which game modes do players finish least often?",
     "SELECT game_mode, COUNT(*) AS plays,\n       ROUND(100.0 * SUM(completed) / COUNT(*), 1) AS finished_pct\n"
     "FROM plays\nWHERE build_kind = 'release'\nGROUP BY game_mode\nHAVING COUNT(*) >= 5\nORDER BY finished_pct ASC\nLIMIT 10",
     lambda r: f"{r[0][0]} is finished least often: {r[0][2]}% of {r[0][1]} plays. "
               f"{r[-1][0]} is finished most often, at {r[-1][2]}%."),
    ("How many players came back the day after their first match, by play type?",
     "SELECT play_type, SUM(players) AS players, SUM(returned_d1) AS back_next_day,\n"
     "       ROUND(100.0 * SUM(returned_d1) / SUM(players), 1) AS back_pct\n"
     "FROM returns\nWHERE build_kind = 'release'\nGROUP BY play_type\nORDER BY back_pct DESC",
     lambda r: " ".join(f"{LABELS.get(x[0], x[0])}: {x[2]} of {x[1]} players came back the next day ({x[3]}%)."
                        for x in r)),
]


def snapshot(store, agg):
    get = {"/v1/me": {"name": "preview", "admin": True, "profiles": True}, "/v1/digest": store.latest_digest() or {}}
    rev65(store, agg, get)
    for build in BUILDS:
        for days in PAGE_DAYS:
            q = f"?build={build}&days={days}"
            page = V.overview(agg, build, days)
            page["digest"] = store.latest_digest()
            get["/v1/overview" + q] = page
            get["/v1/retention" + q] = V.retention(agg, build, days)
            get["/v1/matches" + q] = V.matches(agg, build, days)
            get["/v1/audience" + q] = V.audience(agg, build, days)
            get["/v1/ftue" + q] = V.ftue(agg, build, days)
            get["/v1/economy" + q] = V.economy(agg, build, days)
            get["/v1/social" + q] = V.social(agg, build, days)
            get["/v1/people" + q] = store.people_list(build, days)       # the page filters a search itself
    for days in HEALTH_DAYS:
        get[f"/v1/health?days={days}"] = V.health(agg, store.health_facts(), days)
    for person in store.people_list(None, 365)["people"]:
        get[f"/v1/person?id={person['distinct_id']}"] = store.person(person["distinct_id"])
    ask = {}
    for question, sql, sentence in ASKS:
        result = V.run_readonly_sql(agg, sql)
        ask[question] = {"answer": sentence(result["rows"]) if result["rows"] else "No rows in the sample.",
                         "caveat": "Preview: this sentence was written from the rows by the preview script. On the live "
                                   "console Claude writes the query and the answer.",
                         "sql": sql, **result}
    built = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    return {"get": get, "ask": ask,
            "note": f"Preview on invented sample data, frozen at {built}. No server is behind this page: every player, "
                    f"name and number is made up, Erasure is off, and Ask knows its three example questions only."}


SERIES_BUILDS, SERIES_DAYS = ("release", "editor"), (30, 90)
SAMPLE_DASHBOARD = {"id": "launch_week", "name": "Launch week", "owner": "preview", "shared": "readers", "email": None,
                    "tiles": [{"id": f"t{i}", "source": f"builtin:{name}", "title": title, "chart": chart, "w": w, "h": h}
                              for i, (name, title, chart, w, h) in enumerate((
                                  ("new_players", "New players", "number", 1, 1), ("d1_return", "D1 return", "number", 1, 1),
                                  ("ftue_finished", "FTUE finished", "number", 1, 1), ("rows_per_play", "Rows per play", "number", 1, 1),
                                  ("matches_per_day", "Matches per day", "bars", 2, 2), ("play_mix", "Play mix", "pie", 2, 2)))]
                    + [{"id": "tv", "source": "preset:p_elements", "title": "Matches by element", "chart": "bars", "w": 4, "h": 2}]}
SAMPLE_PRESETS = [
    ("p_elements", "preview", "Matches by element, weekly", True, "overview", "overview.series", True,
     {"form": "bars", "measure": "matches", "by": "element", "over": "week"}, {"build": "release", "days": 90, "play": "all"}),
    ("p_toybox", "garrett", "Toybox switches by vessel", True, "overview", "overview.freestyle.switches", True,
     {"form": "bars", "measure": "freestyle_switches", "by": "vessel_class", "over": "none"}, {"build": "release", "days": 30, "play": "all"}),
    ("p_solo", "preview", "Solo vs AI finishes by mode", False, "matches", "matches.series", True,
     {"form": "line", "measure": "finished_pct", "by": "game_mode", "over": "week"}, {"build": "release", "days": 90, "play": "solo_vs_ai"}),
    ("p_secret", "caleb", "Caleb's private view", False, "overview", "overview.series", True,
     {"form": "bars", "measure": "matches", "by": "none", "over": "day"}, {"build": "editor", "days": 30}),
]


def sample_presets(store):
    """Saved views as people would make them: the preview's own, a public one of someone else's, and a private one of
    someone else's that the preview never lists (the same rule as the server)."""
    for preset_id, owner, name, public, page, chart, series, settings, filters in SAMPLE_PRESETS:
        with store.lock:
            store.db.execute("INSERT OR REPLACE INTO presets VALUES(?,?,?,?,?,?,?,?,?,?,?)",
                             (preset_id, name, owner, int(public), page, chart, int(series), json.dumps(settings),
                              json.dumps(filters), S.now_ms(), S.now_ms()))
            store.db.commit()


def rev65(store, agg, get):
    """The Rev 65 pages (eras, cohorts, play types, every chart's switches, Query Studio, dashboards) as the preview's
    frozen answers, keyed by the exact URLs console.js asks for (URLSearchParams order: build, days, then the rest).
    Writes stay off: the page answers them with "saving is off in this preview"."""
    from urllib.parse import urlencode
    eras, milestones = store.eras(), store.milestones()
    get["/v1/eras"] = V.eras_page(agg, eras, milestones)
    data = [e for e in eras if e.get("kind") != "planned"]
    if len(data) >= 2:
        get["/v1/eras/compare?" + urlencode({"a": data[0]["id"], "b": data[1]["id"], "build": "release"})] = \
            V.eras_compare(agg, data[0], data[1], "release")
    for build in BUILDS:
        for days in PAGE_DAYS:
            q = urlencode({"build": build, "days": days})
            page = V.cohorts(agg, build, days)
            sc = V.Scope(build, days, "all", None)
            page["lifespans"] = store.lifespans(build, sc.since, sc.until, sc.source, None, "all")
            get["/v1/cohorts?" + q] = page
            get["/v1/playtypes?" + q] = V.playtypes(agg, build, days)
            for tile in SAMPLE_DASHBOARD["tiles"]:
                if not tile["source"].startswith("builtin:"):
                    continue
                get["/v1/dashboards/tile?" + urlencode({"build": build, "days": days, "source": tile["source"]})] = \
                    V.builtin_tile(agg, tile["source"].split(":", 1)[1], build, days)
    for build in SERIES_BUILDS:
        for days in SERIES_DAYS:
            for measure in V.SERIES_MEASURES:
                for by in V.SERIES_ANSWERS[measure]:
                    for over in V.SERIES_OVER:
                        url = "/v1/series?" + urlencode({"build": build, "days": days, "measure": measure, "by": by, "over": over})
                        get[url] = V.series(agg, measure, by, over, build, days, "all", None, eras)
    for build in BUILDS:
        for days in PAGE_DAYS:
            get["/v1/freestyle?" + urlencode({"build": build, "days": days})] = V.freestyle(agg, build, days)
    presets = store.presets("preview")
    get["/v1/presets"] = {"presets": presets}
    for preset in presets:
        if not preset["series"]:
            continue
        tile = V.preset_tile(agg, preset, None, eras)
        for build in BUILDS:
            for days in PAGE_DAYS:
                get["/v1/dashboards/tile?" + urlencode({"build": build, "days": days, "source": "preset:" + preset["id"]})] = tile
    get["/v1/schema"] = V.schema(agg)
    get["/v1/queries"] = {"queries": []}
    get["/v1/segments"] = {"segments": []}
    get["/v1/dashboards"] = {"dashboards": [SAMPLE_DASHBOARD]}


def page_content(snap):
    """index.html's body with the CSS, the JS and the snapshot inlined, as artifact page content."""
    with open(os.path.join(HERE, "index.html"), encoding="utf-8") as handle:
        html = handle.read()
    body = html.split("<body>", 1)[1].split('<script src="console.js"></script>', 1)[0]
    with open(os.path.join(HERE, "theme.js"), encoding="utf-8") as handle:
        theme_js = handle.read()
    with open(os.path.join(HERE, "themes.css"), encoding="utf-8") as handle:
        css = handle.read() + "\n"
    with open(os.path.join(HERE, "console.css"), encoding="utf-8") as handle:
        css += handle.read()
    with open(os.path.join(HERE, "console.js"), encoding="utf-8") as handle:
        js = handle.read()
    data = json.dumps(snap, separators=(",", ":"), default=str).replace("</", "<\\/")
    return (f'<meta charset="utf-8">\n<title>Froglet Analytics</title>\n'
            f'<link rel="preconnect" href="https://fonts.googleapis.com">\n'
            f'<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>\n'
            f'<link rel="stylesheet" href="{FONTS}">\n'
            f"<script>\n{theme_js}</script>\n"
            f"<style>\n{css}\n</style>\n{body.strip()}\n"
            f'<script type="application/json" id="fa-snapshot">{data}</script>\n'
            f"<script>\n{js}\n</script>\n")


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--out", required=True)
    parser.add_argument("--data", help="a server's data dir; omit for invented sample data")
    args = parser.parse_args(argv)
    work = args.data or tempfile.mkdtemp(prefix="fa-preview-")
    store = S.Store(work, A.schema_events(A.REPO_SCHEMA), A.DEFAULTS["extra_events"])
    if not args.data:
        cutoff = S.now_ms() - SERVER_LIVE_DAYS * 86400 * 1000
        for player, (country, events) in sample_events().items():
            old = [e for e in events if (S.parse_timestamp(e["timestamp"]) or 0) < cutoff]
            new = [e for e in events if (S.parse_timestamp(e["timestamp"]) or 0) >= cutoff]
            for e in old:                                  # the PostHog import keeps each event's country itself
                e["country"] = country
            for i in range(0, len(old), 500):
                store.ingest(old[i:i + 500], trusted=True, max_age_days=365)
            for i in range(0, len(new), 500):
                store.ingest(new[i:i + 500], country=country)
        sample_presets(store)
    store.rebuild_aggregates()
    store.mark_job("rebuild", "preview")
    store.mark_job("backup", "preview")
    if not args.data:
        C.write_digest(store, store.agg_path)
    with open(args.out, "w", encoding="utf-8") as handle:
        handle.write(page_content(snapshot(store, store.agg_path)))
    print(f"wrote {args.out} ({os.path.getsize(args.out) // 1024} KB)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
