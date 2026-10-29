-- Replay retention by play type (PostHog HogQL). Three queries, run each on its own:
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
-- DEDUPLICATION. Until PR #1639 is verified every game event is recorded twice, so everything
-- groups by (distinct_id, match_id) and counts matches, never raw rows. Query 1 shows when the
-- duplicates stop.
--
-- TWO DIFFERENT QUESTIONS, TWO QUERIES.
--   Query 2: REMATCH, did the player start another match within 10 minutes of finishing one.
--   Query 3: RETURN, did the player come back on a later day.
--
-- PARAMETERS (edit the literals): window `INTERVAL 90 DAY` (appears in every subquery of a
-- query), rematch gap `600` seconds (query 2), D7 maturity `today() - 7` (query 3).
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
    GROUP BY distinct_id, match_id
),
dones AS (
    SELECT distinct_id, properties.match_id AS match_id, min(timestamp) AS done_ts
    FROM events
    WHERE event = 'game_completed'
      AND timestamp > now() - INTERVAL 90 DAY
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
