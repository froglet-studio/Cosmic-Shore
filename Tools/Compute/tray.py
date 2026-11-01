"""The Froglet Compute tray: a small icon on each team computer that says what the computer is doing for the team's AI
training, pauses and resumes it, sets when it may work, opens the console and uninstalls everything. No terminal.

    python3 Tools/Compute/compute.py tray                 run it (the installer starts it and registers it at login)
    python3 Tools/Compute/compute.py tray --configure     run it and open the schedule window at once (the installer's last step)
    python3 Tools/Compute/compute.py tray --install-autostart | --remove-autostart

What it shows: Working on <run> / Free (waiting for you to be away) / In use / Paused / Outside its hours / the agent
is not running, read from the agent's own heartbeat (~/.froglet-compute/results/<user>/<host>/heartbeat.json). What
it does: Pause and Resume (a `paused` file the agent honours: nothing starts, the console shows "paused"; the run in
progress finishes), Schedule... (whenever away / only at night / every day between two hours / only when someone sends
a run / all the time, and whether a sent run may start at once: it re-registers the agent through `compute.py install`
with the matching flags, so the choice is the same one the installer and the paste line take), Open the console,
Uninstall... (asks, then `compute.py uninstall`, which stops every agent and tray, and the tray's own login entry; the tray
quits), Quit (stops the agent until the next login). One tray per computer (`tray.lock`).

With `pystray` and `Pillow` installed (the installer tries `pip install --user pystray pillow`; both optional) it is a
real tray icon with a menu; without them it is a small window with the same buttons, which the person may minimise.
Pure parts (`schedule_flags`, `status_text`) are self-tested; the windows are not (no display here).
Shombith, 2026-11-01: "build the tray app and the installer better ... with uninstall option ... after install users
can do the configuration of when they want their PC to be performed upon".
"""
import json
import os
import platform
import subprocess
import sys
import threading
import time
import webbrowser

import compute_lib as L

HERE = os.path.dirname(os.path.abspath(__file__))
COMPUTE = os.path.join(HERE, "compute.py")
TRAY_LABEL = "studio.froglet.compute.tray"
WHENS = [("away", "Whenever I'm away (10 minutes idle, on mains power)"), ("nights", "Only at night (22:00 to 07:00)"),
         ("hours", "Every day between two hours"), ("sent", "Only when someone sends a run from the console"),
         ("always", "All the time (nobody uses this computer)")]


# ── config and state ──
def config_path():
    return os.path.join(L.cache_dir(), "config.json")


def load_config():
    return L.read_json(config_path(), {}) or {}


def save_config(cfg):
    with open(config_path(), "w", encoding="utf-8") as fh:
        json.dump(cfg, fh, indent=1)


def pause_file():
    return os.path.join(L.cache_dir(), "paused")


def is_paused():
    return os.path.exists(pause_file())


def set_paused(on):
    if on:
        with open(pause_file(), "w", encoding="utf-8") as fh:
            fh.write(str(int(time.time())))
    elif os.path.exists(pause_file()):
        os.remove(pause_file())


def heartbeat():
    user, host = L.identity()[:2]
    return L.read_json(os.path.join(L.results_folder(user, host), "heartbeat.json"), {}) or {}


def agent_running():
    """The agent's single-instance lock is held (agent.single_instance): a second lock attempt fails."""
    path = os.path.join(L.cache_dir(), "agent.lock")
    if not os.path.exists(path):
        return False
    try:
        fh = open(path, "a+")
        try:
            if os.name == "nt":
                import msvcrt
                fh.seek(0)
                msvcrt.locking(fh.fileno(), msvcrt.LK_NBLCK, 1)
                msvcrt.locking(fh.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                import fcntl
                fcntl.flock(fh.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
                fcntl.flock(fh.fileno(), fcntl.LOCK_UN)
            return False
        except OSError:
            return True
        finally:
            fh.close()
    except OSError:
        return False


def status_text(hb, running, paused, now=None):
    """One line for the menu, from the agent's heartbeat."""
    now = now or time.time()
    if paused:
        cur = (hb or {}).get("current") or {}
        return "Paused" + (" (finishing " + str(cur.get("unit") or cur.get("job")) + ")" if cur.get("job") else "")
    if not running:
        return "Not running (open the Schedule to start it)"
    if not hb or now - float(hb.get("updated") or 0) > 600:
        return "Starting up"
    cur = hb.get("current") or {}
    if cur.get("job"):
        what = cur.get("unit") or str(cur.get("job")).split("~")[0]
        evals = cur.get("evals")
        return f"Working on {what}" + (f" ({evals} done)" if evals else "")
    return {"free": "Free: waiting for a run", "in use": "In use: works when you are away",
            "outside hours": "Outside its hours", "working": "Working", "starting": "Starting a run"}.get(hb.get("status"), str(hb.get("status") or "Idle"))


# ── the schedule, the same flags the installer takes ──
def schedule_flags(when, between=None, remote=True):
    """The `compute.py install` flags for a schedule choice; raises ValueError on a bad window."""
    flags = []
    if when == "nights":
        flags += ["--between", "22:00-07:00"]
    elif when == "hours":
        L.parse_window(between or "")
        flags += ["--between", between]
    elif when == "sent":
        flags += ["--only-sent"]
    elif when == "always":
        flags += ["--always"]
    elif when != "away":
        raise ValueError(f"unknown schedule {when!r}")
    return flags


def apply_schedule(when, between=None, remote=True):
    """Re-register the agent with the schedule (compute.py install, which restarts it) and remember the choice."""
    cfg = load_config()
    flags = schedule_flags(when, between, remote)
    args = [sys.executable, COMPUTE, "install", "--repo", cfg.get("repo") or L.REPO]
    if cfg.get("ref"):
        args += ["--home", "origin/" + str(cfg["ref"]).replace("origin/", "")]
    if cfg.get("queue"):
        args += ["--queue", *cfg["queue"]]
    args += flags
    p = subprocess.run(args, cwd=cfg.get("repo") or L.REPO, capture_output=True, text=True, **L.NO_WINDOW)
    cfg.update({"schedule": {"when": when, "between": between if when == "hours" else None}, "remote_start": bool(remote)})
    save_config(cfg)
    return p.returncode == 0, (p.stdout or p.stderr).strip()[-400:]


def console_url():
    cfg = load_config()
    return cfg.get("hub") or "https://claude.ai/artifact/54nfQFCxNBFHYQ2mCErPt1"


# ── autostart ──
def tray_launcher_text():
    return ("import os, runpy, sys\n"
            f"sys.argv = {[COMPUTE, 'tray']!r}\n"
            "runpy.run_path(sys.argv[0], run_name='__main__')\n")


def install_autostart():
    system = platform.system()
    home = L.cache_dir()
    if system == "Windows":
        import install as I
        launcher = os.path.join(home, "tray.pyw")
        with open(launcher, "w", encoding="utf-8") as fh:
            fh.write(tray_launcher_text())
        pythonw = I.windowless(sys.executable)
        q = lambda s: s.replace('"', '""')
        vbs = os.path.join(I.startup_dir(), "FrogletComputeTray.vbs")
        os.makedirs(os.path.dirname(vbs), exist_ok=True)
        with open(vbs, "w", encoding="utf-8") as fh:
            fh.write("' Froglet Compute: the tray icon, at login. `compute.py uninstall` removes this file.\r\n"
                     f'CreateObject("WScript.Shell").Run """{q(pythonw)}"" ""{q(launcher)}""", 0, False\r\n')
        return vbs
    if system == "Darwin":
        plist = os.path.join(os.path.expanduser("~"), "Library", "LaunchAgents", TRAY_LABEL + ".plist")
        os.makedirs(os.path.dirname(plist), exist_ok=True)
        with open(plist, "w", encoding="utf-8") as fh:
            fh.write('<?xml version="1.0" encoding="UTF-8"?>\n<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" '
                     '"http://www.apple.com/DTDs/PropertyList-1.0.dtd">\n<plist version="1.0"><dict>\n'
                     f"<key>Label</key><string>{TRAY_LABEL}</string>\n<key>ProgramArguments</key><array>"
                     f"<string>{sys.executable}</string><string>{COMPUTE}</string><string>tray</string></array>\n"
                     "<key>RunAtLoad</key><true/>\n</dict></plist>\n")
        subprocess.run(["launchctl", "load", plist], capture_output=True)
        return plist
    desk = os.path.join(os.path.expanduser("~"), ".config", "autostart", "froglet-compute-tray.desktop")
    os.makedirs(os.path.dirname(desk), exist_ok=True)
    with open(desk, "w", encoding="utf-8") as fh:
        fh.write(f"[Desktop Entry]\nType=Application\nName=Froglet Compute\nExec={sys.executable} {COMPUTE} tray\nX-GNOME-Autostart-enabled=true\n")
    return desk


def remove_autostart():
    system = platform.system()
    removed = []
    if system == "Windows":
        import install as I
        for p in (os.path.join(I.startup_dir(), "FrogletComputeTray.vbs"), os.path.join(L.cache_dir(), "tray.pyw")):
            if os.path.exists(p):
                os.remove(p)
                removed.append(p)
    elif system == "Darwin":
        plist = os.path.join(os.path.expanduser("~"), "Library", "LaunchAgents", TRAY_LABEL + ".plist")
        subprocess.run(["launchctl", "unload", plist], capture_output=True)
        if os.path.exists(plist):
            os.remove(plist)
            removed.append(plist)
    else:
        desk = os.path.join(os.path.expanduser("~"), ".config", "autostart", "froglet-compute-tray.desktop")
        if os.path.exists(desk):
            os.remove(desk)
            removed.append(desk)
    return removed


def stop_agent():
    """Stop the agent now (it comes back at the next login, or from Start in the menu)."""
    system = platform.system()
    if system == "Windows":
        import install as I
        I._stop_windows_agents()
    elif system == "Darwin":
        import install as I
        subprocess.run(["launchctl", "unload", os.path.join(os.path.expanduser("~"), "Library", "LaunchAgents", I.LABEL + ".plist")], capture_output=True)
    else:
        subprocess.run(["systemctl", "--user", "stop", "froglet-compute.service"], capture_output=True)


def start_agent():
    """Start the agent now, the way the login entry does."""
    system = platform.system()
    if system == "Windows":
        import install as I
        launcher = os.path.join(L.cache_dir(), "worker.pyw")
        if os.path.exists(launcher):
            subprocess.Popen([I.windowless(sys.executable), launcher], cwd=load_config().get("repo") or L.REPO, close_fds=True,
                             creationflags=0x00000008 | 0x00000200 | 0x08000000)
    elif system == "Darwin":
        import install as I
        subprocess.run(["launchctl", "load", os.path.join(os.path.expanduser("~"), "Library", "LaunchAgents", I.LABEL + ".plist")], capture_output=True)
    else:
        subprocess.run(["systemctl", "--user", "start", "froglet-compute.service"], capture_output=True)


def start_detached(configure=False):
    """Start the tray now, with no window of its own (the installer's last step)."""
    args = [sys.executable, COMPUTE, "tray"] + (["--configure"] if configure else [])
    if os.name == "nt":
        import install as I
        args[0] = I.windowless(sys.executable)
        subprocess.Popen(args, close_fds=True, creationflags=0x00000008 | 0x00000200 | 0x08000000)
    else:
        subprocess.Popen(args, close_fds=True, start_new_session=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)


# ── the windows ──
def _tk():
    import tkinter as tk
    from tkinter import ttk, messagebox
    return tk, ttk, messagebox


def schedule_window(parent=None, on_done=None):
    tk, ttk, messagebox = _tk()
    cfg = load_config()
    sched = cfg.get("schedule") or {}
    win = tk.Toplevel(parent) if parent else tk.Tk()
    win.title("Froglet Compute: when may this computer work?")
    win.resizable(False, False)
    frame = ttk.Frame(win, padding=16)
    frame.grid()
    ttk.Label(frame, text="This computer trains the team's AI. When may it work?", font=("", 11, "bold")).grid(column=0, row=0, columnspan=3, sticky="w", pady=(0, 10))
    when = tk.StringVar(value=sched.get("when") or "away")
    for i, (key, label) in enumerate(WHENS):
        ttk.Radiobutton(frame, text=label, value=key, variable=when).grid(column=0, row=1 + i, columnspan=3, sticky="w", pady=2)
    hours = ttk.Frame(frame)
    hours.grid(column=0, row=1 + len(WHENS), columnspan=3, sticky="w", padx=(24, 0))
    frm, to = tk.StringVar(value=(sched.get("between") or "20:00-06:00").split("-")[0]), tk.StringVar(value=(sched.get("between") or "20:00-06:00").split("-")[-1])
    ttk.Label(hours, text="from").grid(column=0, row=0); ttk.Entry(hours, textvariable=frm, width=6).grid(column=1, row=0, padx=4)
    ttk.Label(hours, text="to").grid(column=2, row=0); ttk.Entry(hours, textvariable=to, width=6).grid(column=3, row=0, padx=4)
    ttk.Label(hours, text="(24-hour, e.g. 20:00 to 06:00)").grid(column=4, row=0, padx=(8, 0))
    remote = tk.BooleanVar(value=cfg.get("remote_start", True) is not False)
    ttk.Checkbutton(frame, text="A run sent from the console may start at once, even while I use this computer", variable=remote).grid(column=0, row=2 + len(WHENS), columnspan=3, sticky="w", pady=(10, 0))
    note = ttk.Label(frame, text="", foreground="#666")
    note.grid(column=0, row=3 + len(WHENS), columnspan=3, sticky="w", pady=(8, 0))

    def save():
        w = when.get()
        between = f"{frm.get().strip()}-{to.get().strip()}" if w == "hours" else None
        try:
            ok, out = apply_schedule(w, between, remote.get())
        except ValueError as e:
            note.config(text=str(e)); return
        if ok:
            # remote start is the agent's config too (setup.py writes it from --allow-remote-start)
            c = load_config(); c["remote_start"] = bool(remote.get()); save_config(c)
            note.config(text="Saved. The agent restarted with this schedule.")
            win.after(900, lambda: (win.destroy(), on_done() if on_done else None))
        else:
            note.config(text="Could not re-register the agent: " + out[-160:])
    buttons = ttk.Frame(frame)
    buttons.grid(column=0, row=4 + len(WHENS), columnspan=3, sticky="e", pady=(12, 0))
    ttk.Button(buttons, text="Cancel", command=win.destroy).grid(column=0, row=0, padx=4)
    ttk.Button(buttons, text="Save", command=save).grid(column=1, row=0)
    if not parent:
        win.mainloop()
    return win


def do_uninstall(ask):
    """Uninstall the agent and the tray (asks first through `ask`); returns True when it happened."""
    if not ask("Remove Froglet Compute from this computer? The agent stops and leaves the login items; the console "
               "forgets the computer when its results branch is removed there (Remove this computer). Your "
               "Cosmic-Shore folder is untouched."):
        return False
    subprocess.run([sys.executable, COMPUTE, "uninstall"], capture_output=True, text=True, **L.NO_WINDOW)
    remove_autostart()
    stop_agent()
    return True


_TRAY_LOCK = None


def tray_lock(path=None):
    """True when this is the computer's only tray (a lock file held while it runs, like the agent's). The installer
    starts a tray every time it runs and the login entry starts another, and each showed its own Uninstall dialog
    (Shombith, 2026-11-01, "got two screens"): a second tray now exits at once."""
    global _TRAY_LOCK
    path = path or os.path.join(L.cache_dir(), "tray.lock")
    fh = open(path, "a+")
    try:
        if os.name == "nt":
            import msvcrt
            fh.seek(0)
            msvcrt.locking(fh.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl
            fcntl.flock(fh.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB)
    except OSError:
        fh.close()
        return False
    _TRAY_LOCK = fh
    return True


def run_tray(configure=False):
    """The tray icon (pystray) or, without it, a small window with the same controls. A second tray on the same
    computer only opens the Schedule window when asked to configure, then leaves."""
    if not tray_lock():
        if configure:
            schedule_window()
        return 0
    try:
        import pystray
        from PIL import Image, ImageDraw
    except ImportError:
        pystray = None
    if configure:
        threading.Thread(target=lambda: None, daemon=True).start()
    if pystray is None:
        return run_window(configure)

    def icon_image(color):
        im = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        d.rounded_rectangle((6, 6, 58, 58), radius=14, fill=(11, 12, 16, 255))
        d.ellipse((22, 22, 42, 42), fill=color)
        return im
    colors = {"working": (0, 212, 255, 255), "free": (76, 195, 138, 255), "paused": (232, 180, 76, 255), "off": (120, 120, 120, 255)}

    def state():
        hb, running, paused = heartbeat(), agent_running(), is_paused()
        text = status_text(hb, running, paused)
        key = "paused" if paused else ("off" if not running else ("working" if text.startswith("Working") else "free"))
        return text, key

    def menu():
        text, _ = state()
        paused = is_paused()
        return pystray.Menu(
            pystray.MenuItem(text, None, enabled=False),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("Start the agent", lambda: (start_agent(), time.sleep(1), icon.update_menu()), visible=not agent_running()),
            pystray.MenuItem("Resume" if paused else "Pause (the current run finishes)", lambda: (set_paused(not is_paused()), icon.update_menu()), visible=agent_running()),
            pystray.MenuItem("Schedule…", lambda: threading.Thread(target=lambda: schedule_window(), daemon=True).start()),
            pystray.MenuItem("Open the console", lambda: webbrowser.open(console_url())),
            pystray.Menu.SEPARATOR,
            pystray.MenuItem("Uninstall…", lambda: uninstall()),
            pystray.MenuItem("Quit (stops the agent until the next login)", lambda: (stop_agent(), icon.stop())))

    def uninstall():
        tk, ttk, messagebox = _tk()
        root = tk.Tk(); root.withdraw()
        if do_uninstall(lambda q: messagebox.askyesno("Froglet Compute", q)):
            root.destroy(); icon.stop(); os._exit(0)      # the tray is part of what was removed
        else:
            root.destroy()

    icon = pystray.Icon("froglet-compute", icon_image(colors["free"]), "Froglet Compute", menu())

    def refresh():
        while icon.visible or True:
            time.sleep(10)
            try:
                text, key = state()
                icon.icon = icon_image(colors[key]); icon.title = "Froglet Compute: " + text
                icon.update_menu()
            except Exception:
                pass
    threading.Thread(target=refresh, daemon=True).start()
    if configure:
        threading.Thread(target=lambda: (time.sleep(0.5), schedule_window()), daemon=True).start()
    icon.run()
    return 0


def run_window(configure=False):
    tk, ttk, messagebox = _tk()
    root = tk.Tk()
    root.title("Froglet Compute")
    root.resizable(False, False)
    frame = ttk.Frame(root, padding=16); frame.grid()
    status = ttk.Label(frame, text="", font=("", 11, "bold")); status.grid(column=0, row=0, columnspan=2, sticky="w")
    sub = ttk.Label(frame, text="This computer trains the team's AI while you are away.", foreground="#666"); sub.grid(column=0, row=1, columnspan=2, sticky="w", pady=(0, 10))
    pause_btn = ttk.Button(frame, text="Pause"); pause_btn.grid(column=0, row=2, sticky="ew", pady=2)
    ttk.Button(frame, text="Quit (stops the agent until the next login)", command=lambda: (stop_agent(), root.destroy())).grid(column=0, row=5, columnspan=2, sticky="ew", pady=(8, 0))
    ttk.Button(frame, text="Schedule…", command=lambda: schedule_window(root, refresh)).grid(column=1, row=2, sticky="ew", pady=2, padx=(6, 0))
    ttk.Button(frame, text="Open the console", command=lambda: webbrowser.open(console_url())).grid(column=0, row=3, sticky="ew", pady=2)
    ttk.Button(frame, text="Uninstall…", command=lambda: do_uninstall(lambda q: messagebox.askyesno("Froglet Compute", q)) and (root.destroy(), os._exit(0))).grid(column=1, row=3, sticky="ew", pady=2, padx=(6, 0))
    ttk.Label(frame, text="Closing this window keeps the computer working; Quit stops it.", foreground="#666").grid(column=0, row=4, columnspan=2, sticky="w", pady=(10, 0))

    def refresh():
        text = status_text(heartbeat(), agent_running(), is_paused())
        status.config(text=text); pause_btn.config(text="Start the agent" if not agent_running() else ("Resume" if is_paused() else "Pause (the current run finishes)"))
        root.after(10000, refresh)
    pause_btn.config(command=lambda: ((start_agent() if not agent_running() else set_paused(not is_paused())), refresh()))
    refresh()
    if configure:
        root.after(300, lambda: schedule_window(root, refresh))
    root.mainloop()
    return 0


def self_test():
    bad = []
    if schedule_flags("away") != [] or schedule_flags("nights") != ["--between", "22:00-07:00"] or schedule_flags("sent") != ["--only-sent"] \
            or schedule_flags("always") != ["--always"] or schedule_flags("hours", "20:00-06:00") != ["--between", "20:00-06:00"]:
        bad.append("the schedule flags are wrong")
    for bad_when, bet in (("hours", "25:00-06:00"), ("weekly", None)):
        try:
            schedule_flags(bad_when, bet); bad.append(f"{bad_when} {bet} was accepted")
        except ValueError:
            pass
    now = 1000.0
    if status_text({}, False, False, now) != "Not running (open the Schedule to start it)": bad.append("not running")
    if not status_text({"updated": 990, "status": "working", "current": {"job": "unity-train~r1", "unit": "skimrace-squirrel-i4", "evals": 3}}, True, False, now).startswith("Working on skimrace-squirrel-i4 (3 done)"): bad.append("working")
    if status_text({"updated": 990, "status": "in use"}, True, False, now) != "In use: works when you are away": bad.append("in use")
    if status_text({"updated": 100, "status": "free"}, True, False, now) != "Starting up": bad.append("stale heartbeat")
    if status_text({"updated": 990, "status": "free", "current": {"job": "x", "unit": "u"}}, True, True, now) != "Paused (finishing u)": bad.append("paused")
    if "tray" not in tray_launcher_text(): bad.append("launcher")
    import tempfile
    lock = os.path.join(tempfile.mkdtemp(prefix="tray-lock-"), "tray.lock")
    if not tray_lock(lock): bad.append("the first tray could not take the lock")
    second = subprocess.run([sys.executable, "-c", f"import sys; sys.path.insert(0, {HERE!r}); import tray; sys.exit(0 if tray.tray_lock({lock!r}) else 3)"],
                            capture_output=True, text=True)
    if second.returncode != 3: bad.append(f"a second tray took the lock too (exit {second.returncode}: {second.stderr[-200:]})")
    return bad


def main(argv):
    if "--self-test" in argv:
        bad = self_test()
        print("tray self-test: " + ("ok" if not bad else "FAILED\n  " + "\n  ".join(bad)))
        return 1 if bad else 0
    if "--install-autostart" in argv:
        print("the tray starts at login: " + install_autostart()); return 0
    if "--remove-autostart" in argv:
        print("removed: " + ", ".join(remove_autostart() or ["nothing"])); return 0
    if "--start" in argv:
        start_detached(configure="--configure" in argv); return 0
    return run_tray(configure="--configure" in argv)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
