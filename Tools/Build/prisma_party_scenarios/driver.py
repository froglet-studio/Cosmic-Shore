"""Drive one headless Prisma instance of the game over its control port.

Each instance is a separate process with its own NetworkManager, so the game code under test is
the real code, end to end - this module only presses what a person would press and reads what a
person (or the DiagnosticsHUD console) would read:

  - `console(inst, line)` types a line into the DiagnosticsHUD console and returns the command's
    result as the game logged it (`[DiagnosticsHUD] <line> → <result>`).
  - `party(inst)` is `console(inst, "party")` parsed into a dict (PartyConsoleCommand.Describe).
  - `inst.do(...)` is the port's input script: `click x,y`, `type text`, `key Enter`, and the
    port's inspector verbs (`arcade <Mode>`, `arcade start`, `arcade ready`, `score`, `vessels`).

Screen coordinates: the port's `dump_ui` reports rects bottom-left-origin; clicks are
top-left-origin, so a rect is flipped against the 900 px window height before clicking.
"""
import json
import pathlib
import re
import time
import urllib.request

WINDOW_HEIGHT = 900


class Inst:
    """One running instance: a control port plus the file its stdout goes to."""

    def __init__(self, label, port, log_path, pid=None):
        self.label, self.port, self.log_path, self.pid = label, port, pathlib.Path(log_path), pid

    def __repr__(self):
        return f"Pilot{self.label}@{self.port}"

    @property
    def name(self):
        return f"Pilot{self.label}"

    def cmd(self, cmd, arg=None, timeout=30):
        body = {"cmd": cmd} if arg is None else {"cmd": cmd, "arg": arg}
        req = urllib.request.Request(f"http://127.0.0.1:{self.port}/", data=json.dumps(body).encode(), method="POST")
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode())

    def out(self, cmd, arg=None):
        return self.cmd(cmd, arg).get("output") or ""

    def state(self):
        return self.cmd("state")

    def do(self, action):
        return self.out("do", action)

    def wait(self, frames):
        return self.cmd("wait", str(frames), timeout=120)

    def logs(self, n=400):
        return self.out("logs", str(n))

    def log_lines(self):
        return self.log_path.read_text(encoding="utf-8", errors="replace").splitlines()

    def log_mark(self):
        """A position in this instance's log; pass it to `log_since` to read only what came after."""
        return len(self.log_lines())

    def log_since(self, mark):
        return self.log_lines()[mark:]

    def rect(self, name):
        """First ACTIVE match's rect, top-left origin: (x0, y0, x1, y1), or None."""
        for line in self.out("dump_ui", f"{name}:0").splitlines():
            m = re.search(r"- (.+?) active=(\w+)/(\w+) rect=\((-?\d+),(-?\d+)\)-\((-?\d+),(-?\d+)\)", line)
            if not m or m.group(1) != name or m.group(3) != "True":
                continue
            x0, y0, x1, y1 = map(int, m.groups()[3:])
            return (x0, WINDOW_HEIGHT - y1, x1, WINDOW_HEIGHT - y0)
        return None

    def click(self, name):
        r = self.rect(name)
        if r is None:
            raise RuntimeError(f"{self}: no active UI object '{name}'")
        self.do(f"click {(r[0] + r[2]) // 2},{(r[1] + r[3]) // 2}")


def until(pred, timeout_s, what, step=1.0):
    """Poll `pred` until truthy; returns its value. Exceptions inside `pred` count as 'not yet'."""
    t0, last_err = time.time(), None
    while time.time() - t0 < timeout_s:
        try:
            v = pred()
            if v:
                return v
        except Exception as e:  # an instance mid-scene-load can refuse a control request
            last_err = e
        time.sleep(step)
    raise TimeoutError(f"timed out after {timeout_s}s waiting for {what}" + (f" (last error: {last_err})" if last_err else ""))


def console(inst, line, settle=10):
    """Run a DiagnosticsHUD console line; return the result the game logged for it (or None)."""
    inst.click("CmdInput")
    inst.wait(3)
    inst.do(f"type {line}")
    inst.wait(3)
    inst.do("key Enter")
    inst.wait(settle)
    tag = f"[DiagnosticsHUD] {line} → "
    for l in reversed(inst.logs(400).splitlines()):
        if tag in l:
            return l.split(tag, 1)[1]
    return None


def party(inst):
    """`party` as a dict: role, state, session, members, conns, humans, spectators, partyHost,
    spectating, offline, scene, invite, names."""
    line = console(inst, "party") or ""
    return dict(kv.split("=", 1) for kv in line.split()[1:] if "=" in kv)


def wait_party(inst, pred, what, timeout_s=240):
    """Poll `party(inst)` until `pred(state)`; returns the state. The timeout names the last state."""
    last = {}

    def ok():
        nonlocal last
        last = party(inst)
        return pred(last)

    try:
        until(ok, timeout_s, what, step=2)
    except TimeoutError as e:
        raise TimeoutError(f"{inst}: {e}; last party state: {last}") from None
    return last


def is_solo(s):
    """A player standing in their own working menu: hosting a party of one on Menu_Main."""
    return s.get("role") == "host" and s.get("members") == "1/4" and s.get("scene") == "Menu_Main"


def boot_to_menu(inst, timeout_s=900):
    """Answer each first-run prompt that comes up until Menu_Main shows the console.

    The prompts are the age gate (birth year 1990), analytics consent (always "No thanks" - consent
    is never accepted on a person's behalf) and the username prompt (`Pilot<label>`).
    """
    t0 = time.time()
    while time.time() - t0 < timeout_s:
        try:
            st = inst.state()
            if st.get("scene") == "Menu_Main" and inst.rect("CmdInput") is not None:
                return
            if inst.rect("BirthYear"):
                inst.click("BirthYear"); inst.wait(5); inst.do("type 1990"); inst.wait(5)
                inst.click("Button_Continue"); inst.wait(30)
            elif inst.rect("Button_No thanks"):
                inst.click("Button_No thanks"); inst.wait(30)
            elif inst.rect("UsernameInputField") and inst.rect("ConfirmUsernameButton"):
                inst.click("UsernameInputField"); inst.wait(5); inst.do(f"type {inst.name}"); inst.wait(5)
                inst.click("ConfirmUsernameButton"); inst.wait(60)
        except Exception:
            pass  # control port not up yet, or a scene mid-load
        time.sleep(1)
    raise TimeoutError(f"{inst}: never reached Menu_Main")


def press_enter_together(insts, line):
    """Stage `line` in each console, then press Enter on all of them behind one barrier: the
    closest this harness gets to several people pressing the same button at once. Returns the
    wall-clock skew between the first and last Enter, in ms."""
    import threading
    for i in insts:
        i.click("CmdInput"); i.wait(3); i.do(f"type {line}"); i.wait(3)
    bar, stamps = threading.Barrier(len(insts)), {}

    def go(i):
        bar.wait()
        stamps[i.port] = time.time()
        i.do("key Enter")

    threads = [threading.Thread(target=go, args=(i,)) for i in insts]
    [t.start() for t in threads]
    [t.join() for t in threads]
    return round((max(stamps.values()) - min(stamps.values())) * 1000, 2)


def double_tap(inst, line):
    """Run `line` twice back to back - the second press lands while the first is in flight."""
    r = inst.rect("CmdInput")
    x, y = (r[0] + r[2]) // 2, (r[1] + r[3]) // 2
    for _ in range(2):
        inst.do(f"click {x},{y}"); inst.do(f"type {line}"); inst.do("key Enter")


def vessels(inst):
    """{net_id: (owner_client_id, (x, y, z))} for every spawned vessel this instance sees."""
    mark = inst.log_mark()
    inst.do("vessels")
    inst.wait(5)
    time.sleep(1)
    out = {}
    for l in inst.log_since(mark):
        m = re.search(r"^\[vessels\] .*? net#(\d+) owner=(\d+).*? at \((-?[\d.]+), (-?[\d.]+), (-?[\d.]+)\)", l)
        if m:
            out[int(m.group(1))] = (int(m.group(2)), tuple(float(v) for v in m.groups()[2:]))
    return out


def score_rows(inst):
    """{pilot name: score line} from the port's `score` verb (RoundStats as this process sees them)."""
    mark = inst.log_mark()
    inst.do("score")
    inst.wait(5)
    time.sleep(1)
    out = {}
    for l in inst.log_since(mark):
        m = re.match(r"^\[score\] stats '(.+?)' (.*)$", l)
        if m:
            out[m.group(1)] = m.group(2)
    return out
