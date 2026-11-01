"""Join Froglet Compute from your own Cosmic-Shore checkout, in one line. The Compute Console's Install button shows it:

    macOS / Linux (Terminal), from your Cosmic-Shore folder:
        git fetch origin <ref> && git show origin/<ref>:Tools/Compute/setup.py | python3 -
    Windows (PowerShell), from your Cosmic-Shore folder:
        git fetch origin <ref>; git show origin/<ref>:Tools/Compute/setup.py | python -

<ref> is bleeding-edge once Froglet Compute is merged there, and the compute branch (APP_REF) until then.

Options go after the dash (`... | python3 - --nights`):
    --nights          work only 22:00-07:00 instead of whenever you are away
    --between A-B     work only inside these local hours, e.g. --between 20:00-06:00
    --only-sent       never pick work by itself: only runs someone sends from the console
    --always          a computer nobody sits at (a VM): work all the time
    --allow-remote-start   let a run sent from the console start at once, even while you use this computer
                      (without it, a run sent here waits until you are away; change it later with
                      compute.py remote-start on|off)
    --no-first-run    skip the short first session that puts this computer on the console
    --dry-run         copy the code into ~/.froglet-compute/app and print what it would register; register nothing
    --uninstall       stop it and remove what it added
    --hub URL --hub-key KEY   report live to the team's hub (the VM), so phones see this computer's runs in seconds

What it does, in order, and nothing else:
    1. checks that this folder is a Cosmic-Shore checkout and that git and Python 3.8+ are here (numpy is optional)
    2. copies Tools/Compute from the ref into ~/.froglet-compute/app (git archive: your checkout is not touched)
    3. registers the worker with your OS from there (compute.py install --repo <this checkout> --home <ref>): it
       starts when you have been away 10 minutes, runs at the lowest priority, stops the moment you come back, and
       updates itself from <ref> between sessions
    4. runs one short first session (about two minutes) so this computer shows on the console

It reads its whole script from stdin, so it asks nothing: every choice is an option above.
"""
import json
import os
import shutil
import subprocess
import sys
import tarfile

APP_REF = "claude/eager-franklin-e6g77h"     # the compute branch, until Froglet Compute is on bleeding-edge
APP = os.path.join(os.environ.get("FROGLET_COMPUTE_HOME") or os.path.join(os.path.expanduser("~"), ".froglet-compute"), "app")


SINK = None             # the installer window's log, when there is one (say() writes there too)
NO_WINDOW = {"creationflags": 0x08000000} if os.name == "nt" else {}


def say(msg=""):
    print(msg, flush=True)
    if SINK is not None:
        try:
            SINK(msg)
        except Exception:
            pass


def git(*args, check=True):
    p = subprocess.run(["git", *args], capture_output=True, text=True, **NO_WINDOW)
    if check and p.returncode:
        raise SystemExit(f"\nstopped: git {' '.join(args)} failed:\n{(p.stderr or p.stdout).strip()}")
    return p.stdout.strip()


def is_checkout(path):
    """A Cosmic-Shore checkout: a .git and the game's Assets folder."""
    return os.path.isdir(os.path.join(path, ".git")) and os.path.isdir(os.path.join(path, "Assets", "_Scripts"))


def gui(argv):
    """The installer window: the folder (Browse; the current one when it is a checkout, never searched for), when the computer may work, Install with a log, then the
    tray. No terminal: the .bat and .sh the console serves start this (Shombith, 2026-11-01: "why can't there be a
    good looking installer").""" 
    global SINK
    import threading
    import tkinter as tk
    from tkinter import ttk, filedialog, messagebox
    root = tk.Tk()
    root.title("Froglet Compute Setup")
    root.resizable(False, False)
    f = ttk.Frame(root, padding=18); f.grid()
    ttk.Label(f, text="Froglet Compute", font=("", 15, "bold")).grid(column=0, row=0, columnspan=3, sticky="w")
    ttk.Label(f, text="This computer trains the team's AI while you are away, steered from the console.", foreground="#666").grid(column=0, row=1, columnspan=3, sticky="w", pady=(0, 12))
    ttk.Label(f, text="Your Cosmic-Shore folder").grid(column=0, row=2, sticky="w")
    folder = tk.StringVar(value=os.getcwd() if is_checkout(os.getcwd()) else "")    # never searched for (Shombith, 2026-11-01)
    ent = ttk.Entry(f, textvariable=folder, width=58); ent.grid(column=1, row=2, sticky="ew", padx=(8, 6))
    def browse():
        d = filedialog.askdirectory(title="Your Cosmic-Shore folder (the one you open in Unity)")
        if d: folder.set(d)
    ttk.Button(f, text="Browse…", command=browse).grid(column=2, row=2)
    found = ttk.Label(f, text="this folder" if folder.get() else "press Browse and pick the folder you open in Unity", foreground="#2a7" if folder.get() else "#666")
    found.grid(column=1, row=3, sticky="w", padx=(8, 0))
    ttk.Label(f, text="When may it work?", font=("", 10, "bold")).grid(column=0, row=4, columnspan=3, sticky="w", pady=(14, 4))
    whens = [("away", "Whenever I'm away (10 minutes idle, on mains power)"), ("nights", "Only at night (22:00 to 07:00)"),
             ("hours", "Every day between two hours"), ("sent", "Only when someone sends a run from the console"),
             ("always", "All the time (nobody uses this computer)")]
    when = tk.StringVar(value="away")
    for i, (k, lab) in enumerate(whens):
        ttk.Radiobutton(f, text=lab, value=k, variable=when).grid(column=0, row=5 + i, columnspan=3, sticky="w", pady=1)
    hrs = ttk.Frame(f); hrs.grid(column=0, row=10, columnspan=3, sticky="w", padx=(24, 0))
    frm, to = tk.StringVar(value="20:00"), tk.StringVar(value="06:00")
    ttk.Label(hrs, text="from").grid(column=0, row=0); ttk.Entry(hrs, textvariable=frm, width=6).grid(column=1, row=0, padx=4)
    ttk.Label(hrs, text="to").grid(column=2, row=0); ttk.Entry(hrs, textvariable=to, width=6).grid(column=3, row=0, padx=4)
    ttk.Label(hrs, text="(only for \"every day between two hours\")", foreground="#666").grid(column=4, row=0, padx=(8, 0))
    remote = tk.BooleanVar(value=True)
    ttk.Checkbutton(f, text="A run sent from the console may start at once, even while I use this computer", variable=remote).grid(column=0, row=11, columnspan=3, sticky="w", pady=(8, 0))
    log = tk.Text(f, height=9, width=80, state="disabled", font=("Consolas" if os.name == "nt" else "Menlo", 9))
    log.grid(column=0, row=12, columnspan=3, sticky="ew", pady=(12, 0))
    buttons = ttk.Frame(f); buttons.grid(column=0, row=13, columnspan=3, sticky="e", pady=(10, 0))
    install_btn = ttk.Button(buttons, text="Install")
    ttk.Button(buttons, text="Close", command=root.destroy).grid(column=0, row=0, padx=4)
    install_btn.grid(column=1, row=0)

    def write(msg):
        def put():
            log.config(state="normal"); log.insert("end", msg + "\n"); log.see("end"); log.config(state="disabled")
        root.after(0, put)
    SINK = write

    def run():
        repo = folder.get().strip()
        if not is_checkout(repo):
            messagebox.showerror("Froglet Compute", "That is not a Cosmic-Shore checkout: pick the folder you open in Unity (it has Assets and .git)."); return
        args = [a for a in argv if a not in ("--gui",)]
        if "--repo" in args:
            i = args.index("--repo"); del args[i:i + 2]
        args += ["--repo", repo, "--no-first-run"]
        w = when.get()
        if w == "nights": args.append("--nights")
        elif w == "hours": args += ["--between", f"{frm.get().strip()}-{to.get().strip()}"]
        elif w == "sent": args.append("--only-sent")
        elif w == "always": args.append("--always")
        if remote.get() and "--allow-remote-start" not in args: args.append("--allow-remote-start")
        install_btn.config(state="disabled")
        def work():
            try:
                rc = main(args)
                write("" if rc == 0 else f"stopped ({rc})")
                if rc == 0:
                    write("Done. The tray icon is yours now: it shows what this computer does, pauses it, changes this schedule, opens the console and uninstalls.")
                    root.after(0, lambda: install_btn.config(text="Done", command=root.destroy, state="normal"))
            except SystemExit as e:
                write(str(e)); root.after(0, lambda: install_btn.config(state="normal"))
            except Exception as e:
                write(f"{type(e).__name__}: {e}"); root.after(0, lambda: install_btn.config(state="normal"))
        threading.Thread(target=work, daemon=True).start()
    install_btn.config(command=run)
    root.mainloop()
    return 0


def ref_with_compute():
    """bleeding-edge when Froglet Compute is merged there, the compute branch until then."""
    git("fetch", "--quiet", "origin", "bleeding-edge", APP_REF, check=False)
    for ref in ("bleeding-edge", APP_REF):
        if subprocess.run(["git", "cat-file", "-e", f"origin/{ref}:Tools/Compute/compute.py"],
                          capture_output=True).returncode == 0:
            return ref
    raise SystemExit("\nstopped: neither origin/bleeding-edge nor origin/" + APP_REF + " has Tools/Compute. "
                     "Is this the Cosmic-Shore repository, and can you fetch from it?")


def copy_app(ref):
    say(f"2. copying Tools/Compute from origin/{ref} into {APP}")
    tmp = APP + ".part"
    shutil.rmtree(tmp, ignore_errors=True)
    os.makedirs(tmp)
    # To a file, not through a pipe: tarfile stops reading at the archive's end marker while git still writes its
    # padding, and a Windows pipe is too small to hold it, so both would wait on each other for ever.
    tar_path = tmp + ".tar"
    if subprocess.run(["git", "archive", "--format=tar", "-o", tar_path, f"origin/{ref}", "--", "Tools/Compute"]).returncode:
        raise SystemExit("\nstopped: git archive failed")
    with tarfile.open(tar_path) as tar:
        if hasattr(tarfile, "data_filter"):
            tar.extractall(tmp, filter="data")
        else:
            tar.extractall(tmp)
    os.remove(tar_path)
    shutil.rmtree(APP, ignore_errors=True)
    os.replace(tmp, APP)
    # Which Tools/Compute this copy is: compute.py runs a newer one for a pasted command when the ref moves on.
    tree = git("rev-parse", f"origin/{ref}:Tools/Compute", check=False).strip()
    if tree:
        with open(os.path.join(APP, ".tree"), "w", encoding="utf-8") as fh:
            fh.write(tree + "\n")


def main(argv):
    opts = set(argv)
    if "--gui" in opts:
        return gui(argv)
    if "--repo" in argv and argv.index("--repo") + 1 < len(argv):
        os.chdir(argv[argv.index("--repo") + 1])
    if ("--hub" in opts) != ("--hub-key" in opts):
        raise SystemExit("stopped: --hub and --hub-key go together (the console's Install sheet shows both)")
    dry = "--dry-run" in opts
    say("Froglet Compute setup")
    if sys.version_info < (3, 8):
        raise SystemExit("stopped: Python 3.8 or newer is needed (python3 --version)")
    if not shutil.which("git"):
        raise SystemExit("stopped: git is not on this computer's PATH")
    repo = git("rev-parse", "--show-toplevel", check=False)
    remote = git("remote", "get-url", "origin", check=False)
    if not repo or "cosmic-shore" not in remote.lower():
        raise SystemExit("stopped: run this from inside your Cosmic-Shore folder (cd into it first)")
    say(f"1. this checkout: {repo}")
    compute = os.path.join(APP, "Tools", "Compute", "compute.py")
    if "--uninstall" in opts:
        if os.path.exists(compute) and not dry:
            subprocess.call([sys.executable, compute, "uninstall"])
        say("removed. To remove its files too, delete " + os.path.dirname(APP))
        return 0
    try:
        import numpy  # noqa: F401
        say("   numpy: yes (every Regatta job can run here)")
    except ImportError:
        say("   numpy: no. Most jobs need it: " + os.path.basename(sys.executable) + " -m pip install --user numpy")
    ref = ref_with_compute()
    copy_app(ref)
    args = [sys.executable, compute, "install", "--repo", repo, "--home", f"origin/{ref}"]
    # Until Froglet Compute is on bleeding-edge its job files live on the compute branch: read the queue there too.
    queue = ["bleeding-edge", "ai", "compute-queue"] + ([ref] if ref != "bleeding-edge" else [])
    args += ["--queue", *queue]
    if "--nights" in opts:
        args += ["--between", "22:00-07:00"]
    elif "--between" in argv and argv.index("--between") + 1 < len(argv):
        args += ["--between", argv[argv.index("--between") + 1]]
    if "--always" in opts:
        args += ["--always"]
    if "--only-sent" in opts:
        args += ["--only-sent"]
    say("3. registering the worker: " + " ".join(args[2:]))
    if not dry:                     # so commands typed by hand (compute.py run --request ...) find this checkout
        cfg_path = os.path.join(os.path.dirname(APP), "config.json")
        try:
            with open(cfg_path, encoding="utf-8") as fh:
                cfg = json.load(fh)                 # keep what an earlier setup or `remote-start` said
        except Exception:
            cfg = {}
        cfg.update({"repo": repo, "ref": ref, "queue": queue})
        cfg["remote_start"] = "--allow-remote-start" in opts
        cfg["schedule"] = {"when": "nights" if "--nights" in opts else "always" if "--always" in opts else "sent" if "--only-sent" in opts
                           else "hours" if "--between" in argv else "away",
                           "between": argv[argv.index("--between") + 1] if "--between" in argv and argv.index("--between") + 1 < len(argv) else None}
        if "--hub" in argv and "--hub-key" in argv:
            cfg["hub"], cfg["hub_key"] = argv[argv.index("--hub") + 1], argv[argv.index("--hub-key") + 1]
        with open(os.path.join(os.path.dirname(APP), "config.json"), "w", encoding="utf-8") as fh:
            json.dump(cfg, fh, indent=1)
    if dry:
        args.append("--dry-run")
    rc = subprocess.call(args, cwd=repo)
    if rc:
        raise SystemExit(f"\nstopped: install exited {rc}")
    if "--no-first-run" in opts or dry:
        say("4. first session skipped")
    else:
        say("4. a short first session (about two minutes), so this computer shows on the console...")
        subprocess.call([sys.executable, compute, "run", "--repo", repo, "--no-update", "--always", "--once",
                         "--max-evals", "60", "--session", "3", "--progress-minutes", "1", "--queue", *queue], cwd=repo)
    if not dry:                     # the app: the console on this computer, a run started here at once (README section 18)
        out = subprocess.run([sys.executable, compute, "app", "--shortcut"], cwd=repo, capture_output=True, text=True)
        say("5. " + (out.stdout.strip() or "no desktop launcher: " + out.stderr.strip()[-200:]))
    if not dry:                     # the tray: status, pause, schedule, uninstall, no terminal (tray.py)
        if "--no-tray" not in opts:
            pip = subprocess.run([sys.executable, "-m", "pip", "install", "--user", "--quiet", "pystray", "pillow"],
                                 capture_output=True, text=True)
            tray_note = "a tray icon" if pip.returncode == 0 else "a small window (pystray could not be installed: pip said " + (pip.stderr or pip.stdout).strip()[-120:] + ")"
            subprocess.run([sys.executable, compute, "tray", "--install-autostart"], cwd=repo, capture_output=True)
            subprocess.run([sys.executable, compute, "tray", "--start"] + ([] if SINK is not None else ["--configure"]), cwd=repo, capture_output=True)
            say(f"6. Froglet Compute runs as {tray_note}: it opens now and asks when this computer may work; it starts at login.")
    say("\ndone. It now works whenever you are away, or as you set it in the tray's Schedule. The tray also pauses, opens the"
        " console and uninstalls.")
    say("Stop it any time: the tray's Uninstall, or  git show origin/" + ref + ":Tools/Compute/setup.py | python3 - --uninstall")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
