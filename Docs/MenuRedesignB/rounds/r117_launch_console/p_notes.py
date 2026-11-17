from common import *


def col(title, items):
    return ('<div style="flex:1"><div class="lab" style="margin-bottom:16px">' + title + '</div>'
            + "".join('<div style="margin-bottom:14px"><div style="font-size:18px">%s</div><div class="sm soft" style="margin-top:4px;line-height:1.5">%s</div></div>' % (a, b) for a, b in items)
            + '</div>')


def build():
    pages = [("Mission", "The stage you are in, its gate, every platform's go or no-go, the next steps, and Start a release."),
             ("Pipeline", "Bleeding Edge, Development, Master, Release side by side with their vessels and what each feeds. Promote opens what would move."),
             ("Builds", "Every platform on every branch. A build's steps live, where it goes (Drive, Play, TestFlight, Steam, the Toybox), its size over time."),
             ("Release", "Perform a release in five steps: what, checks, notes, audience, launch. A check that blocks says how to get past it."),
             ("Signals", "Who is playing by access tier, the gate meter, retention by build, data from the builds, and the feedback inbox."),
             ("Theme", "Deep Sea, Shore, Nocturne and Dart Frog, each previewed. Motion full, calm or off; density; the sea and the cell behind.")]
    reads = [("Mission", "The launch-status board, READINESS.md and the stages in the launch-flow skill, as the console reads them today."),
             ("Pipeline and Builds", "The branches, the Bleeding-edge guard, the release-builds and sync-build-branches workflows, the build profiles."),
             ("Release", "The release-promote workflow, the checklist, the runbook and the changelog already in the console's store."),
             ("Signals", "Feedback from the Bug Ledger and the in-game button. Retention and sessions need an analytics source; none is connected yet."),
             ("Theme", "The style guide's tokens and the colour-set themes the analytics console already generates. Saved per person.")]
    opens = [("Every number is made up", "The boards say Sample data. Building it means wiring each page to the sources beside it."),
             ("The Release branch does not exist", "Pipeline, Builds and Release show it as not created. When is it made: at Gate 1?"),
             ("Gate 1 has no measure or target", "Mission and Signals show 42% against a target that is not set. Which number, and what bar?"),
             ("Vessel counts after Beta Phase 1", "X and Y on the stage plan are open, so the later stages show no counts."),
             ("Six pages, one board", "Frogma holds 512 files and had two free, so the six pages share one board: the rail switches them and the theme follows. Free some room and each page gets its own board.")]
    body = ('<div class="root" data-th="deep" style="width:1920px;height:900px;padding:56px 64px;box-sizing:border-box;background:var(--g)">'
            '<div class="lab">Rev 117 notes</div>'
            '<div class="h1" style="margin:10px 0 8px">The Launch Console, as the tool you release with</div>'
            '<div class="soft" style="max-width:1400px;line-height:1.5">Six pages in one look: press Play, the left rail switches pages, the swatches in the top bar switch the theme, '
            'and only the call to action glows. Status keeps the live console\'s rule: Lime for go, Fire dashed for at risk, Fire solid for no-go. No way back (Promote, Release) is Ruby.</div>'
            '<div style="display:flex;gap:56px;margin-top:36px">' + col("The six pages", pages) + col("What each would read", reads) + col("Sample data, and what is open", opens) + '</div></div>')
    return ('<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n<title>Rev 117 notes - the Launch Console as six pages</title>\n'
            '<script src="./support.js"></script>\n</head>\n<body>\n<x-dc>\n' + helmet() + body + '\n</x-dc>\n'
            '<script type="text/x-dc" data-dc-script data-props=\'{"$preview":{"width":1920,"height":900}}\'>\n'
            'class Component extends DCLogic {\n  renderVals() { return {}; }\n}\n</script>\n</body>\n</html>\n')
