#!/usr/bin/env python3
"""Party / presence / match request discipline: every UGS call goes through ONE executor.

    python3 Tools/Build/check_ugs_request_discipline.py            # scan, exit 1 on any finding
    python3 Tools/Build/check_ugs_request_discipline.py --check    # same (CI spelling)
    python3 Tools/Build/check_ugs_request_discipline.py --self-test

The rules are Docs/MultiplayerArchitecture/REVIEW_INVITE_AND_RESILIENCE.md Phases 0-1, which
replaced five private failure classifiers and nine inline retry loops with
`UgsRequestPolicy` (one classifier, jittered back-off, a per-client retry budget, single-flight
by operation key, and the `ugs[...]` counters on every NetDiag line). A call that bypasses the
executor is invisible to the counters, unbounded by the budget and free to race a duplicate -
and nothing about it fails to compile. The converge join in PresenceLobbyService did exactly
that for a day after Phase 1 claimed "every UGS call", found only by re-checking at a merge.

What is reported (comments and string literals are blanked first; line numbers are kept):

  R1  a UGS create / join / query / save call that is not inside a `...ExecuteAsync(` call's
      argument list (the policy's lambda).
  R2  a private failure classifier: a method named Is{RateLimit,Transient,HostConflict,
      SessionGone,BenignLobbyPatcher}... outside UgsRequestPolicy.cs, or `.Message.Contains(`
      on one of the SDK's failure phrases. `UgsRequestPolicy.Classify` is the only reader.
  R3  an inline `for (int attempt ...)` retry loop under Controller/Party or
      Controller/Multiplayer.
  R4  the retired PENDING acceptance handshake (AcceptanceSignalService, accepted_invite,
      PENDING_SESSION_ID, UpdatePayloadsWithRealSessionId).
  R5  a SCALED `UniTask.Delay` in the party / multiplayer services, the UGS policy or
      NetworkMonitor. Every non-HOME Menu_Main screen sets Time.timeScale = 0, so a scaled
      backend wait never completes there (upstream c2825eb4).
  R6  an awaited UGS call in Controller/Party that does not resume with `.AsMainThread()`
      (the threading contract: Docs/claude/ARCHITECTURE_CORE.md, Threading & Main-Thread Affinity).

Reviewed exceptions are in ALLOW, one row per file + rule, each with its reason.

What it does NOT prove: that the policy's classification is right for a new SDK error (that is
UgsRequestPolicyTests), or that a call made through the policy is made at the right time.
"""
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
assert os.path.isdir(os.path.join(ROOT, "Assets")), "ROOT must contain Assets/"

SCRIPTS = "Assets/_Scripts"

UGS_CALL = re.compile(
    r"\b(CreateSessionAsync|JoinSessionByIdAsync|JoinSessionByCodeAsync|CreateOrJoinSessionAsync|"
    r"MatchmakeSessionAsync|QuerySessionsAsync|SaveCurrentPlayerDataAsync|SavePropertiesAsync|"
    r"QueryLobbiesAsync|JoinLobbyByIdAsync|CreateLobbyAsync|UpdatePlayerAsync|UpdateLobbyAsync)\s*\(")
DECLARATION = re.compile(r"\b(public|private|protected|internal|static|override|virtual|abstract)\b[^;{=]*$")
EXECUTE = re.compile(r"\bExecuteAsync\s*(<[^>]*>)?\s*\(")
CLASSIFIER = re.compile(
    r"\bbool\s+Is(RateLimit|Transient|HostConflict|SessionGone|BenignLobbyPatcher)\w*\s*\(")
SNIFF = re.compile(
    r"\.Message\s*\.\s*Contains\s*\(\s*\"(Too Many Requests|rate limit|Index was out of range|is full|lobby full)")
RETRY_LOOP = re.compile(r"\bfor\s*\(\s*int\s+attempt\b")
PENDING = re.compile(r"\b(AcceptanceSignalService|ACCEPTED_INVITE_KEY|PENDING_SESSION_ID|UpdatePayloadsWithRealSessionId)\b")
DELAY = re.compile(r"\bUniTask\s*\.\s*Delay\s*\(")
UNSCALED = re.compile(r"UnscaledDeltaTime|ignoreTimeScale\s*:\s*true|DelayType\s*\.\s*Realtime")

PARTY = re.compile(r"^Assets/_Scripts/Controller/Party/")
NET_SERVICES = re.compile(r"^Assets/_Scripts/(Controller/(Party|Multiplayer)/|Utility/Ugs|System/NetworkMonitor\.cs)")
RETRY_SCOPE = re.compile(r"^Assets/_Scripts/Controller/(Party|Multiplayer)/")

# (file, rule) -> reason. Keep each row narrow and say why.
ALLOW = {
    ("Assets/_Scripts/Utility/BenignLobbyLogFilter.cs", "R2"):
        "decides what the CONSOLE shows, not what is retried; delegates exceptions to "
        "UgsRequestPolicy.IsLobbyPatcherStaleIndex and only adds a pre-rendered-string match",
    ("Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs", "R3"):
        "a Netcode roster-pull poll (RequestRosterFromHost_ServerRpc), not a UGS request",
}


def strip_noise(src):
    """Blank comments, strings and chars, keeping every newline so positions map to lines."""
    out, i, n = [], 0, len(src)
    while i < n:
        c = src[i]
        if src.startswith("//", i):
            j = src.find("\n", i)
            j = n if j < 0 else j
            out.append(" " * (j - i)); i = j
        elif src.startswith("/*", i):
            j = src.find("*/", i + 2)
            j = n if j < 0 else j + 2
            out.append(re.sub(r"[^\n]", " ", src[i:j])); i = j
        elif c == '"' or (c in "@$" and i + 1 < n and src[i + 1] == '"') or src.startswith('$@"', i) or src.startswith('@$"', i):
            start = i
            verbatim = False
            while src[i] != '"':
                verbatim |= src[i] == "@"
                i += 1
            i += 1
            while i < n:
                if verbatim and src.startswith('""', i): i += 2; continue
                if not verbatim and src[i] == "\\": i += 2; continue
                if src[i] == '"': i += 1; break
                i += 1
            out.append('"' + re.sub(r"[^\n]", " ", src[start + 1:i - 1]) + '"')
        elif c == "'":
            j = i + 1
            while j < n and src[j] != "'":
                j += 2 if src[j] == "\\" else 1
            out.append(" " * (j + 1 - i)); i = j + 1
        else:
            out.append(c); i += 1
    return "".join(out)


def closing_paren(code, open_idx):
    depth = 0
    for k in range(open_idx, len(code)):
        if code[k] == "(": depth += 1
        elif code[k] == ")":
            depth -= 1
            if depth == 0: return k
    return len(code)


def line_of(code, pos):
    return code.count("\n", 0, pos) + 1


def scan_text(rel, src, raw=None):
    """Findings for one file's text: a list of (line, rule, message)."""
    code = strip_noise(src)
    raw = src if raw is None else raw
    found = []
    allow = lambda rule: (rel, rule) in ALLOW

    # The argument spans of every ExecuteAsync(...) in the file.
    spans = []
    for m in EXECUTE.finditer(code):
        open_idx = code.index("(", m.end() - 1)
        spans.append((open_idx, closing_paren(code, open_idx)))

    for m in UGS_CALL.finditer(code):
        line_start = code.rfind("\n", 0, m.start()) + 1
        if DECLARATION.search(code[line_start:m.start()]):
            continue  # a declaration of a method with this name, not a call
        if not code[line_start:m.start()].rstrip().endswith((".", "await", "=>")) and "." not in code[max(0, m.start() - 2):m.start()]:
            continue  # only member calls (x.CreateSessionAsync(...)) are SDK calls
        if not allow("R1") and not any(a < m.start() < b for a, b in spans):
            found.append((line_of(code, m.start()), "R1",
                          f"{m.group(1)} is not called through UgsRequestPolicy.ExecuteAsync"))
        if PARTY.search(rel) and not allow("R6") and re.search(r"\bawait\b", code[max(0, line_start - 200):m.start()]):
            open_idx = code.index("(", m.end() - 1)
            after = code[closing_paren(code, open_idx) + 1:closing_paren(code, open_idx) + 40]
            if not re.match(r"\s*\.\s*AsMainThread\s*\(", after):
                found.append((line_of(code, m.start()), "R6",
                              f"awaited {m.group(1)} does not resume with .AsMainThread()"))

    if not rel.endswith("/UgsRequestPolicy.cs") and not allow("R2"):
        for m in CLASSIFIER.finditer(code):
            found.append((line_of(code, m.start()), "R2",
                          f"private failure classifier `{m.group(0).strip()}` - use UgsRequestPolicy.Classify"))
        for m in SNIFF.finditer(strip_noise_keep_strings(raw)):
            found.append((line_of(code, m.start()), "R2",
                          "an SDK failure phrase sniffed from Exception.Message - use UgsRequestPolicy.Classify"))

    if RETRY_SCOPE.search(rel) and not allow("R3"):
        for m in RETRY_LOOP.finditer(code):
            found.append((line_of(code, m.start()), "R3",
                          "inline retry loop - retry through UgsRequestPolicy.ExecuteAsync, at ONE layer"))

    if not allow("R4"):
        for m in PENDING.finditer(code):
            found.append((line_of(code, m.start()), "R4",
                          f"{m.group(1)} - the PENDING acceptance handshake was retired (review Phase 1b)"))

    if NET_SERVICES.search(rel) and not allow("R5"):
        for m in DELAY.finditer(code):
            open_idx = code.index("(", m.end() - 1)
            call = code[m.start():closing_paren(code, open_idx) + 1]
            if not UNSCALED.search(call):
                found.append((line_of(code, m.start()), "R5",
                              "scaled UniTask.Delay in a backend service - pass DelayType.UnscaledDeltaTime"))
    return found


def strip_noise_keep_strings(src):
    """Comments blanked, strings kept: R2's phrase match reads string literals."""
    out, i, n = [], 0, len(src)
    while i < n:
        if src.startswith("//", i):
            j = src.find("\n", i); j = n if j < 0 else j
            out.append(" " * (j - i)); i = j
        elif src.startswith("/*", i):
            j = src.find("*/", i + 2); j = n if j < 0 else j + 2
            out.append(re.sub(r"[^\n]", " ", src[i:j])); i = j
        else:
            out.append(src[i]); i += 1
    return "".join(out)


def runtime_files():
    for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, SCRIPTS)):
        parts = dirpath.replace("\\", "/").split("/")
        if "Editor" in parts or "Tests" in parts:
            continue
        for f in filenames:
            if f.endswith(".cs"):
                full = os.path.join(dirpath, f)
                yield os.path.relpath(full, ROOT).replace("\\", "/"), full


def run():
    findings, count = [], 0
    for rel, full in sorted(runtime_files()):
        count += 1
        with open(full, encoding="utf-8-sig", errors="replace") as fh:
            src = fh.read()
        for ln, rule, msg in scan_text(rel, src):
            findings.append(f"{rel}:{ln}: [{rule}] {msg}")
    for f in findings:
        print(f)
    print(f"ugs-request-discipline: {'OK' if not findings else str(len(findings)) + ' FINDING(S)'} "
          f"({count} files, {len(ALLOW)} reviewed exceptions)")
    return 1 if findings else 0


def self_test():
    party = "Assets/_Scripts/Controller/Party/Services/Fake.cs"
    other = "Assets/_Scripts/Gameplay/Fake.cs"
    must_fire = {
        "R1 bare create": (party, "async UniTask F(){ var s = await _svc.CreateSessionAsync(o).AsMainThread(); }"),
        "R1 bare join outside party": (other, "void F(){ _ = svc.JoinSessionByIdAsync(id, o); }"),
        "R1 bare save": (party, "async UniTask F(){ await s.SaveCurrentPlayerDataAsync().AsMainThread(); }"),
        "R1 call AFTER the policy lambda closed": (party,
            "async UniTask F(){ await _p.ExecuteAsync(\"k\", async () => await A().AsMainThread()); "
            "await s.SaveCurrentPlayerDataAsync().AsMainThread(); }"),
        "R2 private classifier": (party, "static bool IsRateLimitException(Exception e) => false;"),
        "R2 message sniff": (party, "if (e.Message.Contains(\"Too Many Requests\")) {}"),
        "R3 retry loop": (party, "for (int attempt = 0; ; attempt++) { }"),
        "R4 handshake": (party, "var x = AcceptanceSignalService.Instance;"),
        "R5 scaled delay": (party, "async UniTask F(){ await UniTask.Delay(500); }"),
        "R5 scaled delay with token": (party, "async UniTask F(){ await UniTask.Delay(500, cancellationToken: ct); }"),
        "R6 no AsMainThread": (party,
            "async UniTask F(){ await _p.ExecuteAsync(\"k\", async () => await _svc.QuerySessionsAsync(q)); }"),
    }
    must_not_fire = {
        "R1 through the policy": (party,
            "async UniTask F(){ var s = await _p.ExecuteAsync(\"party:create\", "
            "async () => await _svc.CreateSessionAsync(o).AsMainThread()); }"),
        "R1 multi-line lambda": (party,
            "async UniTask F(){\n await _p.ExecuteAsync(null, async () =>\n {\n  if (!first) {}\n"
            "  await s.SaveCurrentPlayerDataAsync().AsMainThread();\n });\n}"),
        "R1 generic ExecuteAsync": (party,
            "async UniTask F(){ await _p.ExecuteAsync<ISession>(\"k\", async () => await _svc.JoinSessionByIdAsync(id, o).AsMainThread()); }"),
        "R1 a declaration": (party, "public async UniTask<ISession> CreateSessionAsync(SessionOptions o) { return null; }"),
        "R1 in a comment": (party, "// await _svc.CreateSessionAsync(o);"),
        "R1 in a string": (party, "var s = \"CreateSessionAsync(o) failed\";"),
        "R2 in the policy itself": ("Assets/_Scripts/Utility/UgsRequestPolicy.cs",
            "if (e.Message.Contains(\"Too Many Requests\")) {} static bool IsRateLimitX(Exception e) => false;"),
        "R2 reviewed exception": ("Assets/_Scripts/Utility/BenignLobbyLogFilter.cs",
            "static bool IsBenignLobbyPatcherLogFormat(string f, object[] a) => false;"),
        "R3 outside the scope": (other, "for (int attempt = 0; attempt < 3; attempt++) { }"),
        "R3 reviewed exception": ("Assets/_Scripts/Controller/Multiplayer/ClientPlayerVesselInitializer.cs",
            "for (int attempt = 0; attempt < max; attempt++) { }"),
        "R5 unscaled": (party, "async UniTask F(){ await UniTask.Delay(500, DelayType.UnscaledDeltaTime); }"),
        "R5 ignoreTimeScale": (party, "async UniTask F(){ await UniTask.Delay(ms, ignoreTimeScale: true, cancellationToken: ct); }"),
        "R5 multi-line unscaled": (party, "async UniTask F(){ await UniTask.Delay(\n TimeSpan.FromSeconds(1),\n DelayType.UnscaledDeltaTime); }"),
        "R5 gameplay delay is not a backend wait": (other, "async UniTask F(){ await UniTask.Delay(500); }"),
        "R6 outside Party": ("Assets/_Scripts/Controller/Multiplayer/Fake.cs",
            "async UniTask F(){ await _p.ExecuteAsync(\"k\", async () => await _svc.QuerySessionsAsync(q)); }"),
    }
    bad = 0
    for name, (rel, code) in must_fire.items():
        rule = name.split()[0]
        if not any(r == rule for _, r, _ in scan_text(rel, code)):
            print(f"SELF-TEST FAIL: did not fire on {name}: {code}")
            bad += 1
    for name, (rel, code) in must_not_fire.items():
        f = scan_text(rel, code)
        if f:
            print(f"SELF-TEST FAIL: fired on {name}: {code} -> {f}")
            bad += 1
    print("self-test", "OK" if not bad else f"FAILED ({bad})")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(self_test() if "--self-test" in sys.argv else run())
