# UGS config-as-code

`Leaderboards/weekly_challenge.lb` — the weekly challenge board (file name = board id).
Ascending (score is a time), keep-best, recurring weekly reset every Monday 00:00 UTC with
archiving, matching `WeeklyChallengeCatalogSO.WeekStartUtc`.

Deploy (does not run from the game):

    ugs login
    ugs config set project-id <PROJECT_ID>
    ugs config set environment-name <ENV>   # e.g. production / development
    ugs deploy UGS/Leaderboards/weekly_challenge.lb

`Start` must be in the future at deploy time; bump it to the next Monday if needed.
Deploying overwrites the board's settings with this file.
