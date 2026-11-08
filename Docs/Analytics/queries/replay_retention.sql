-- Replay retention by play type (PostHog HogQL). Six queries, run each on its own:
--   in PostHog's SQL editor, or through the PostHog MCP (`call execute-sql {"query": "..."}`).
--
-- WHAT "PLAY TYPE" MEANS TODAY. Read off game_started, one row per (player, match):
--   multi_human  more than one human in the match (coop and adversarial are NOT separable yet)
--   solo_vs_ai   one human, at least one AI
--   solo_alone   one human, no AI
-- `is_multiplayer` is true for every match (solo runs through the multiplayer pipeline), so it
-- is not used. The classification lives in ONE place, the `plays` subquery's `play_type`
-- expression, in each query below. When game_started carries the player's domain and a team
-- composition field, split `multi_human` into coop and adversarial there and nowhere else.
--
-- WHEN THE TEAM-COMPOSITION FIELDS SHIP (Docs/Analytics/proposals/team_composition_fields.patch),
-- replace the `play_type` expression in each `plays` subquery with the one below. It splits
-- multi_human into coop / adversarial / mixed, and labels rows recorded before the fields
-- existed 'multi_human_unknown' (tested against live data: all 309 older multi-human plays came
-- back unknown, none guessed). count(x) counts only rows that carry the property.
--
--   if(max(toFloat(properties.player_count_human)) >= 2,
--      if(count(properties.humans_on_other_domains) = 0, 'multi_human_unknown',
--         if(max(toFloat(properties.humans_on_other_domains)) = 0, 'coop',
--            if(max(toFloat(properties.humans_on_my_domain)) >= 2, 'mixed', 'adversarial'))),
--      if(max(toFloat(properties.player_count_ai)) >= 1, 'solo_vs_ai', 'solo_alone'))
--
-- DEDUPLICATION. Until PR #1639 is verified every game event is recorded twice, so everything
-- groups by (distinct_id, match_id) and counts matches, never raw rows. Query 1 shows when the
-- duplicates stop.
--
-- TWO DIFFERENT QUESTIONS, TWO QUERIES.
--   Query 2: REMATCH, did the player start another match within 10 minutes of finishing one.
--   Query 3: RETURN, did the player come back on a later day.
-- A fourth, separate question: Query 4, completion rate by game mode, with historical mode names
-- merged into the current ones. And Query 5, the player funnel from first event to a completed game
-- and a return visit. And Query 6, match health: completion, length and win rate by platform (6a)
-- and by play type and intensity (6b).
--
-- PARAMETERS (edit the literals): window `INTERVAL 90 DAY` (appears in every subquery of a
-- query), rematch gap `600` seconds (query 2), D7 maturity `today() - 7` (query 3).
--
-- EDITOR TOGGLE. Queries 2 and 3 exclude Unity-editor traffic (platform ends in 'Editor', for
-- example WindowsEditor) with the line marked `-- EDITOR TOGGLE`. It appears in each subquery
-- that reads game_started or game_completed. Delete those lines to include editor playtests.
-- Query 1 keeps editor traffic on purpose: duplicates are a facade bug, wherever it runs.
-- On 2026-10-29 editor traffic was 1,284 of about 1,400 matches (19 players); real builds were
-- about 25 players and 117 matches, so the toggle changes the answer a lot.
--
-- PORT TOGGLE (query 2 only). Query 2 also excludes the engine-port Windows build (app_version
-- ends in '-port') with the line marked `-- PORT TOGGLE`. That build records game_started but
-- almost never game_completed: on 2026-10-29 it had 1 completion in 43 plays (2%), against 63%
-- for the regular Windows build, so its matches would read as abandoned and drag the rematch
-- rate down. Query 3 reads only game_started, which the port does record, so it keeps the port.
-- Delete the lines (and re-check the port's completions) once the port reports game_completed.
--
-- READ THE NUMBERS WITH CARE. As of 2026-10-29 there are about 62 players in 90 days, mostly the
-- team playtesting. Rematch rates are inflated by testers replaying quickly and by tournament
-- rounds that chain automatically. Query 3 only counts players old enough to be measured, and
-- "first play" means first play inside the window, which may not be their first ever.


-- ============================================================================
-- Query 1. Duplicate check: rows per play, per day. Healthy value is 1.0.
-- Was exactly 2.0 on every day through 2026-10-29. Re-run after the fix is merged and a build
-- carrying it is in use.
-- ============================================================================
SELECT
    toDate(timestamp) AS day,
    count() AS rows,
    count(DISTINCT tuple(distinct_id, properties.match_id)) AS distinct_plays,
    round(count() / count(DISTINCT tuple(distinct_id, properties.match_id)), 2) AS rows_per_play
FROM events
WHERE event = 'game_started'
  AND timestamp > now() - INTERVAL 14 DAY
GROUP BY day
ORDER BY day DESC
LIMIT 14;


-- ============================================================================
-- Query 2. Rematch rate by play type.
-- A rematch is the same player starting a match within 600 s of completing the previous one.
-- Denominator is every completed match of that type; the numerator counts the matches that
-- were followed by a quick next start.
-- ============================================================================
WITH plays AS (
    SELECT
        distinct_id,
        properties.match_id AS match_id,
        min(timestamp) AS start_ts,
        if(max(toFloat(properties.player_count_human)) >= 2, 'multi_human',
           if(max(toFloat(properties.player_count_ai)) >= 1, 'solo_vs_ai', 'solo_alone')) AS play_type
    FROM events
    WHERE event = 'game_started'
      AND timestamp > now() - INTERVAL 90 DAY
      AND properties.platform NOT LIKE '%Editor'   -- EDITOR TOGGLE
      AND properties.app_version NOT LIKE '%-port'   -- PORT TOGGLE
    GROUP BY distinct_id, match_id
),
dones AS (
    SELECT distinct_id, properties.match_id AS match_id, min(timestamp) AS done_ts
    FROM events
    WHERE event = 'game_completed'
      AND timestamp > now() - INTERVAL 90 DAY
      AND properties.platform NOT LIKE '%Editor'   -- EDITOR TOGGLE
      AND properties.app_version NOT LIKE '%-port'   -- PORT TOGGLE
    GROUP BY distinct_id, match_id
),
joined AS (
    SELECT p.distinct_id AS distinct_id, p.match_id AS match_id, p.play_type AS play_type,
           p.start_ts AS start_ts, d.done_ts AS done_ts
    FROM plays AS p
    LEFT JOIN dones AS d ON p.distinct_id = d.distinct_id AND p.match_id = d.match_id
),
chained AS (
    SELECT
        distinct_id,
        start_ts,
        lagInFrame(done_ts) OVER (PARTITION BY distinct_id ORDER BY start_ts) AS prev_done,
        lagInFrame(play_type) OVER (PARTITION BY distinct_id ORDER BY start_ts) AS prev_type
    FROM joined
),
completed AS (
    -- ClickHouse fills a missing DateTime with 1970, not NULL, hence the > 2001 test.
    SELECT play_type, count() AS completed_matches, count(DISTINCT distinct_id) AS players
    FROM joined
    WHERE done_ts > toDateTime('2001-01-01')
    GROUP BY play_type
),
rematched AS (
    SELECT prev_type AS play_type,
           countIf(dateDiff('second', prev_done, start_ts) BETWEEN 0 AND 600) AS rematches
    FROM chained
    WHERE prev_type != '' AND prev_done > toDateTime('2001-01-01')
    GROUP BY prev_type
)
SELECT
    c.play_type AS play_type,
    c.players AS players,
    c.completed_matches AS completed_matches,
    r.rematches AS rematches_within_10min,
    round(100.0 * r.rematches / c.completed_matches, 1) AS rematch_pct
FROM completed AS c
LEFT JOIN rematched AS r ON c.play_type = r.play_type
ORDER BY c.completed_matches DESC;


-- ============================================================================
-- Query 3. Return rate by the play type of a player's FIRST match in the window.
--   returned_next_day    played on the calendar day (UTC) after their first play day
--   returned_within_7d   played on any of the 7 days after their first play day
-- Only players whose first play day is at least 7 days ago are counted, so nobody is scored
-- before they have had the chance to return.
-- ============================================================================
WITH plays AS (
    SELECT
        distinct_id,
        properties.match_id AS match_id,
        min(timestamp) AS start_ts,
        if(max(toFloat(properties.player_count_human)) >= 2, 'multi_human',
           if(max(toFloat(properties.player_count_ai)) >= 1, 'solo_vs_ai', 'solo_alone')) AS play_type
    FROM events
    WHERE event = 'game_started'
      AND timestamp > now() - INTERVAL 90 DAY
      AND properties.platform NOT LIKE '%Editor'   -- EDITOR TOGGLE
    GROUP BY distinct_id, match_id
),
firsts AS (
    SELECT distinct_id, toDate(min(start_ts)) AS first_day, argMin(play_type, start_ts) AS first_type
    FROM plays
    GROUP BY distinct_id
),
play_days AS (
    SELECT DISTINCT distinct_id, toDate(start_ts) AS play_day FROM plays
),
flagged AS (
    SELECT
        f.distinct_id AS distinct_id,
        f.first_type AS first_type,
        max(if(dateDiff('day', f.first_day, d.play_day) = 1, 1, 0)) AS back_d1,
        max(if(dateDiff('day', f.first_day, d.play_day) BETWEEN 1 AND 7, 1, 0)) AS back_within_7d
    FROM firsts AS f
    LEFT JOIN play_days AS d ON f.distinct_id = d.distinct_id
    WHERE f.first_day <= today() - 7
    GROUP BY f.distinct_id, f.first_type
)
SELECT
    first_type,
    count() AS matured_players,
    sum(back_d1) AS returned_next_day,
    round(100.0 * sum(back_d1) / count(), 1) AS d1_pct,
    sum(back_within_7d) AS returned_within_7d,
    round(100.0 * sum(back_within_7d) / count(), 1) AS d7_pct
FROM flagged
GROUP BY first_type
ORDER BY matured_players DESC;


-- ============================================================================
-- Query 4. Completion by game mode, historical names merged.
-- Older builds recorded game_mode under names the game has since renamed. The map below is
-- GameModeRenameMigration.Renames (Assets/_Scripts/System/CloudData/GameModeRenameMigration.cs);
-- keep the two in step when a mode is renamed. Without it HexRace, MultiplayerCrystalCapture and
-- PeelTheCage show up as separate modes with a few matches and no completions.
-- Keeps both toggles: the port records starts but almost no completions.
-- Read it with care: most modes have fewer than 10 matches, and a match counts as completed only
-- if the same player sent game_completed for the same match_id.
-- ============================================================================
WITH plays AS (
    SELECT
        distinct_id,
        properties.match_id AS match_id,
        transform(any(properties.game_mode),
            ['HexRace', 'MultiplayerCrystalCapture', 'Tournament', 'NucleusRush', 'Ribcage',
             'PeelTheCage', 'MultiplayerJoust', 'CellularDuel', 'MultiplayerWildlifeBlitzGame',
             'MazeRunner', 'Darts'],
            ['SkimRace', 'Scurry', 'Maelstrom', 'BroodRush', 'Cleave',
             'Cleave', 'Joust', 'DuelForTheCell', 'CoOpWildlifeBlitz',
             'MazeRun', 'DolphinDarts'],
            any(properties.game_mode)) AS game_mode
    FROM events
    WHERE event = 'game_started'
      AND timestamp > now() - INTERVAL 90 DAY
      AND properties.platform NOT LIKE '%Editor'   -- EDITOR TOGGLE
      AND properties.app_version NOT LIKE '%-port'   -- PORT TOGGLE
    GROUP BY distinct_id, match_id
),
dones AS (
    SELECT distinct_id, properties.match_id AS match_id, min(timestamp) AS done_ts
    FROM events
    WHERE event = 'game_completed'
      AND timestamp > now() - INTERVAL 90 DAY
      AND properties.platform NOT LIKE '%Editor'   -- EDITOR TOGGLE
      AND properties.app_version NOT LIKE '%-port'   -- PORT TOGGLE
    GROUP BY distinct_id, match_id
)
SELECT
    p.game_mode AS game_mode,
    count() AS matches,
    countIf(d.done_ts > toDateTime('2001-01-01')) AS completed,
    round(100.0 * countIf(d.done_ts > toDateTime('2001-01-01')) / count(), 0) AS completed_pct,
    count(DISTINCT p.distinct_id) AS players
FROM plays AS p
LEFT JOIN dones AS d ON p.distinct_id = d.distinct_id AND p.match_id = d.match_id
GROUP BY game_mode
ORDER BY matches DESC;


-- ============================================================================
-- Query 5. Player funnel, one row: how many real players reached each stage.
-- Stages are counted, not ordered: a player counts at a stage if they ever reached it. The cohort
-- is every player with any of the four events in the window, and "first event" is their earliest
-- one of those, so a player who predates menu_ready is still counted. game_first_launched is not
-- used (it covers only some players), and neither is ftue_step_completed (on 2026-10-29 it had
-- fired for exactly one real player, so an FTUE step funnel is not possible yet).
--   started_within_30min   first game_started within 30 minutes of the first event
--   played_on_2plus_days   played on at least two calendar days (UTC)
--   matured_7d / returned_within_7d   only players whose first day is at least 7 days ago, and
--                          how many of them played on any of the 7 days after it
-- Both toggles are on: the port build records starts but almost no completions.
-- 2026-10-29: 25 players, 23 menu, 21 freestyle, 19 started, 12 completed, 17 started within 30
-- min, 10 on 2+ days, 23 matured, 9 returned within 7 days.
-- ============================================================================
WITH p AS (
    SELECT
        distinct_id,
        minIf(timestamp, event = 'menu_ready') AS t_menu,
        minIf(timestamp, event = 'freestyle_entered') AS t_free,
        minIf(timestamp, event = 'game_started') AS t_start,
        minIf(timestamp, event = 'game_completed') AS t_done,
        min(timestamp) AS t_first,
        groupUniqArray(toDate(timestamp)) AS days
    FROM events
    WHERE timestamp > now() - INTERVAL 90 DAY
      AND properties.platform NOT LIKE '%Editor'   -- EDITOR TOGGLE
      AND properties.app_version NOT LIKE '%-port'   -- PORT TOGGLE
      AND event IN ('menu_ready', 'freestyle_entered', 'game_started', 'game_completed')
    GROUP BY distinct_id
)
-- ClickHouse fills a missing DateTime with 1970, not NULL, hence the > 2001 tests.
SELECT
    count() AS players,
    countIf(t_menu > toDateTime('2001-01-01')) AS reached_menu,
    countIf(t_free > toDateTime('2001-01-01')) AS entered_freestyle,
    countIf(t_start > toDateTime('2001-01-01')) AS started_game,
    countIf(t_done > toDateTime('2001-01-01')) AS completed_game,
    countIf(t_start > toDateTime('2001-01-01') AND dateDiff('minute', t_first, t_start) <= 30) AS started_within_30min,
    countIf(length(days) >= 2) AS played_on_2plus_days,
    countIf(toDate(t_first) <= today() - 7) AS matured_7d,
    countIf(toDate(t_first) <= today() - 7
            AND arrayExists(d -> dateDiff('day', toDate(t_first), d) BETWEEN 1 AND 7, days)) AS returned_within_7d
FROM p;


-- ============================================================================
-- Query 6a. Match health by platform. Run 6a and 6b each on its own.
-- Per match (deduped by player and match_id): did it complete, how long did the completed ones
-- run, how long was the player actually at the stick, and did they win.
--   median_dur_s      wall clock, pause included (game_completed.duration_seconds)
--   median_flight_s   time at the stick, pause and background excluded (flight_time_seconds).
--                     The two being close means players are not pausing or going AFK mid-match.
--   under_60s         completed matches shorter than a minute
--   wins / decided    player_won is only sent when the local result can be resolved, so `decided`
--                     is the number of completed matches that carried it
-- Both toggles are on. 2026-10-29: Android 30 matches / 16 completed / 102 s; iPhone 28 / 12 / 94 s;
-- Windows 19 / 12 / 190 s; 29 wins of 39 decided. Small samples: read as pointers.
-- ============================================================================
WITH s AS (
    SELECT distinct_id, properties.match_id AS match_id, any(properties.platform) AS platform
    FROM events
    WHERE event = 'game_started'
      AND timestamp > now() - INTERVAL 90 DAY
      AND properties.platform NOT LIKE '%Editor'   -- EDITOR TOGGLE
      AND properties.app_version NOT LIKE '%-port'   -- PORT TOGGLE
    GROUP BY distinct_id, match_id
),
d AS (
    SELECT distinct_id, properties.match_id AS match_id, min(timestamp) AS done_ts,
           any(toFloat(properties.duration_seconds)) AS dur,
           any(toFloat(properties.flight_time_seconds)) AS flight,
           any(properties.player_won) AS won
    FROM events
    WHERE event = 'game_completed'
      AND timestamp > now() - INTERVAL 90 DAY
      AND properties.platform NOT LIKE '%Editor'   -- EDITOR TOGGLE
      AND properties.app_version NOT LIKE '%-port'   -- PORT TOGGLE
    GROUP BY distinct_id, match_id
)
SELECT
    s.platform AS platform,
    count() AS matches,
    countIf(d.done_ts > toDateTime('2001-01-01')) AS completed,
    round(100.0 * countIf(d.done_ts > toDateTime('2001-01-01')) / count(), 0) AS completed_pct,
    round(medianIf(d.dur, d.done_ts > toDateTime('2001-01-01')), 0) AS median_dur_s,
    round(medianIf(d.flight, d.done_ts > toDateTime('2001-01-01')), 0) AS median_flight_s,
    countIf(d.done_ts > toDateTime('2001-01-01') AND d.dur < 60) AS under_60s,
    countIf(d.won = 'true' OR d.won = '1') AS wins,
    countIf(d.done_ts > toDateTime('2001-01-01') AND d.won IN ('true', 'false', '1', '0')) AS decided
FROM s
LEFT JOIN d ON s.distinct_id = d.distinct_id AND s.match_id = d.match_id
GROUP BY platform
ORDER BY matches DESC;


-- ============================================================================
-- Query 6b. Match health by play type and intensity.
-- Play type is the same three-way split as the other queries (swap in the commented expression
-- from the header when the team-composition fields ship). Most play is intensity 1 against an AI,
-- so intensities 2 to 4 are a handful of matches each.
-- 2026-10-29: solo_vs_ai I1 32 matches / 15 completed / 90 s; solo_alone I1 23 / 9 / 137 s;
-- solo_vs_ai I2 13 / 8 / 106 s; the rest have 7 matches between them.
-- ============================================================================
WITH s AS (
    SELECT
        distinct_id,
        properties.match_id AS match_id,
        max(toFloat(properties.player_count_human)) AS humans,
        max(toFloat(properties.player_count_ai)) AS ais,
        max(toFloat(properties.intensity)) AS intensity
    FROM events
    WHERE event = 'game_started'
      AND timestamp > now() - INTERVAL 90 DAY
      AND properties.platform NOT LIKE '%Editor'   -- EDITOR TOGGLE
      AND properties.app_version NOT LIKE '%-port'   -- PORT TOGGLE
    GROUP BY distinct_id, match_id
),
d AS (
    SELECT distinct_id, properties.match_id AS match_id, min(timestamp) AS done_ts,
           any(toFloat(properties.duration_seconds)) AS dur
    FROM events
    WHERE event = 'game_completed'
      AND timestamp > now() - INTERVAL 90 DAY
      AND properties.platform NOT LIKE '%Editor'   -- EDITOR TOGGLE
      AND properties.app_version NOT LIKE '%-port'   -- PORT TOGGLE
    GROUP BY distinct_id, match_id
)
SELECT
    if(s.humans >= 2, 'multi_human', if(s.ais >= 1, 'solo_vs_ai', 'solo_alone')) AS play_type,
    concat('I', toString(s.intensity)) AS intensity,
    count() AS matches,
    countIf(d.done_ts > toDateTime('2001-01-01')) AS completed,
    round(medianIf(d.dur, d.done_ts > toDateTime('2001-01-01')), 0) AS median_dur_s
FROM s
LEFT JOIN d ON s.distinct_id = d.distinct_id AND s.match_id = d.match_id
GROUP BY play_type, intensity
ORDER BY matches DESC;
