#!/usr/bin/env python3
"""Report on, or delete, inactive branches of froglet-studio/Cosmic-Shore through the GitHub API.

Run by .github/workflows/branch-cleanup.yml. The rules live in policy.json next to this file.

Modes
  report   list every inactive branch by group; changes nothing (the monthly scheduled run).
  dry-run  check the requested branches against the rules and say what delete would do; changes nothing.
  delete   for each requested branch that passes the rules: save tag archive/<branch>, then delete it.

A requested branch is SKIPPED, never deleted, when it is a trunk or pipeline branch, has a commit
newer than inactiveDays, has an open pull request, is LARGE (>= largeCommitThreshold commits in no
trunk) without --allow-large, or cannot be measured against any trunk.

Needs GITHUB_TOKEN (contents: write for delete, pull-requests: read) and GITHUB_REPOSITORY.
"""
import argparse
import datetime as dt
import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))


class GitHub:
    def __init__(self, token, repo):
        self.token, self.owner, self.name = token, *repo.split("/", 1)

    def _call(self, method, url, body=None):
        req = urllib.request.Request(url, method=method, data=json.dumps(body).encode() if body is not None else None,
                                     headers={"Authorization": f"Bearer {self.token}",
                                              "Accept": "application/vnd.github+json",
                                              "Content-Type": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=60) as r:
                raw = r.read()
                return r.status, json.loads(raw) if raw else None
        except urllib.error.HTTPError as e:
            raw = e.read()
            try:
                return e.code, json.loads(raw)
            except ValueError:
                return e.code, {"message": raw.decode(errors="replace")[:300]}

    def graphql(self, query, variables=None):
        status, data = self._call("POST", "https://api.github.com/graphql", {"query": query, "variables": variables or {}})
        if status != 200 or data is None or "data" not in data:
            raise RuntimeError(f"GraphQL {status}: {data}")
        return data["data"]  # per-alias errors (unrelated histories) leave that alias null

    def rest(self, method, path, body=None):
        return self._call(method, f"https://api.github.com/repos/{self.owner}/{self.name}{path}", body)


def list_branches(gh):
    out, cursor = [], None
    while True:
        d = gh.graphql("""query($o:String!,$r:String!,$c:String){repository(owner:$o,name:$r){
          refs(refPrefix:"refs/heads/",first:100,after:$c){pageInfo{hasNextPage endCursor}
            nodes{name target{... on Commit{oid committedDate messageHeadline author{name}}}
              associatedPullRequests(states:OPEN,first:1){nodes{number}}}}}}""",
                       {"o": gh.owner, "r": gh.name, "c": cursor})
        refs = d["repository"]["refs"]
        for n in refs["nodes"]:
            t = n["target"] or {}
            prs = n["associatedPullRequests"]["nodes"]
            out.append({"name": n["name"], "sha": t.get("oid"), "date": t.get("committedDate"),
                        "msg": t.get("messageHeadline", ""), "author": (t.get("author") or {}).get("name", ""),
                        "pr": prs[0]["number"] if prs else None})
        if not refs["pageInfo"]["hasNextPage"]:
            return out
        cursor = refs["pageInfo"]["endCursor"]


def measure(gh, branches, trunks, batch=30):
    """Set b['unique'] = commits on b in no trunk (min of aheadBy over trunks it shares history with), or None."""
    for i in range(0, len(branches), batch):
        chunk = branches[i:i + batch]
        parts = []
        for ti, t in enumerate(trunks):
            compares = " ".join(f'c{bi}: compare(headRef:{json.dumps("refs/heads/" + b["name"])}){{aheadBy}}'
                                for bi, b in enumerate(chunk))
            parts.append(f't{ti}: ref(qualifiedName:{json.dumps("refs/heads/" + t)}){{{compares}}}')
        d = gh.graphql(f"query($o:String!,$r:String!){{repository(owner:$o,name:$r){{{' '.join(parts)}}}}}",
                       {"o": gh.owner, "r": gh.name})
        repo = d.get("repository") or {}
        for bi, b in enumerate(chunk):
            vals = [((repo.get(f"t{ti}") or {}).get(f"c{bi}") or {}).get("aheadBy") for ti in range(len(trunks))]
            vals = [v for v in vals if isinstance(v, int)]
            b["unique"] = min(vals) if vals else None


def classify(b, policy, now, allow_large=False):
    """Return (group, lock_reason). lock_reason None means the branch may be deleted."""
    name = b["name"]
    if name in policy["neverDelete"] or any(re.search(p, name) for p in policy["neverDeletePatterns"]):
        return "locked", "trunk / pipeline branch"
    if policy.get("lockOpenPullRequests", True) and b.get("pr"):
        return "locked", f"open PR #{b['pr']}"
    age = (now - dt.datetime.fromisoformat(b["date"].replace("Z", "+00:00"))).days
    if age < policy["inactiveDays"]:
        return "active", f"commit {age} days ago"
    u = b.get("unique")
    if u is None:
        return "large", "history could not be measured"
    if u == 0:
        return "merged", None
    if u <= 3:
        return "small", None
    if u < policy["largeCommitThreshold"]:
        return "medium", None
    return "large", (None if allow_large else f"large: {u} unique commits (rerun with allow_large)")


def parse_names(text):
    """Branch names separated by spaces, commas or new lines; lines starting with # are comments."""
    lines = [l for l in (text or "").splitlines() if not l.lstrip().startswith("#")]
    return [n for n in re.split(r"[\s,]+", "\n".join(lines)) if n]


def summary(lines):
    path = os.environ.get("GITHUB_STEP_SUMMARY")
    text = "\n".join(lines) + "\n"
    if path:
        with open(path, "a") as f:
            f.write(text)
    print(text)


def run(gh, mode, requested, allow_large, policy, now):
    all_branches = list_branches(gh)
    by_name = {b["name"]: b for b in all_branches}
    if mode == "report":
        targets = [b for b in all_branches
                   if classify({**b, "unique": 0}, policy, now)[0] not in ("locked", "active")]
    else:
        missing = [n for n in requested if n not in by_name]
        targets = [by_name[n] for n in requested if n in by_name]
    measure(gh, targets, policy["trunks"])

    if mode == "report":
        groups = {}
        for b in sorted(targets, key=lambda b: b["date"]):
            groups.setdefault(classify(b, policy, now)[0], []).append(b)
        out = [f"## Inactive branches ({len(targets)} of {len(all_branches)})", ""]
        for g in ("merged", "small", "medium", "large"):
            rows = groups.get(g, [])
            out += [f"### {g.title()}: {len(rows)}", "", "| Branch | Last commit | Unique commits | Author |", "|---|---|---|---|"]
            out += [f"| `{b['name']}` | {b['date'][:10]} | {b['unique'] if b['unique'] is not None else '?'} | {b['author']} |" for b in rows]
            out.append("")
        summary(out)
        return 0

    out = [f"## Branch cleanup: {mode}", "", "| Branch | Group | Unique commits | Result |", "|---|---|---|---|"]
    failures = 0
    for n in missing:
        out.append(f"| `{n}` | — | — | skipped: no such branch |")
    for b in targets:
        group, lock = classify(b, policy, now, allow_large)
        u = b["unique"] if b["unique"] is not None else "?"
        if lock:
            out.append(f"| `{b['name']}` | {group} | {u} | skipped: {lock} |")
            continue
        if mode == "dry-run":
            out.append(f"| `{b['name']}` | {group} | {u} | would delete (tag `archive/{b['name']}`) |")
            continue
        status, body = gh.rest("POST", "/git/refs", {"ref": f"refs/tags/archive/{b['name']}", "sha": b["sha"]})
        if status not in (201,) and not (status == 422 and "already exists" in str(body)):
            out.append(f"| `{b['name']}` | {group} | {u} | FAILED to save tag ({status}); branch kept |")
            failures += 1
            continue
        status, body = gh.rest("DELETE", "/git/refs/heads/" + urllib.parse.quote(b["name"], safe="/"))
        if status == 204:
            out.append(f"| `{b['name']}` | {group} | {u} | deleted, tag `archive/{b['name']}` |")
        else:
            out.append(f"| `{b['name']}` | {group} | {u} | FAILED to delete ({status}: {(body or {}).get('message', '')}) |")
            failures += 1
    summary(out)
    return 1 if failures else 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--mode", choices=["report", "dry-run", "delete"], default="report")
    ap.add_argument("--branches", default="", help="branch names separated by spaces, commas or newlines")
    ap.add_argument("--branches-file", help="file with branch names, one per line")
    ap.add_argument("--allow-large", action="store_true")
    a = ap.parse_args(argv)
    names = parse_names(a.branches)
    if a.branches_file:
        names += parse_names(open(a.branches_file).read())
    if a.mode != "report" and not names:
        ap.error("dry-run and delete need --branches or --branches-file")
    policy = json.load(open(os.path.join(HERE, "policy.json")))
    gh = GitHub(os.environ["GITHUB_TOKEN"], os.environ.get("GITHUB_REPOSITORY", "froglet-studio/Cosmic-Shore"))
    return run(gh, a.mode, list(dict.fromkeys(names)), a.allow_large, policy, dt.datetime.now(dt.timezone.utc))


if __name__ == "__main__":
    sys.exit(main())
