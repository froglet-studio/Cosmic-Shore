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
import time
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


THEME = {"bg": "#050505", "panel": "#0C0C0C", "field": "#161616", "line": "#222222", "text": "#ECECEC",
         "dim": "#7F7F7F", "accent": "#00D4FF", "ok": "#4CC38A", "bad": "#F2665A"}
# The console's dark theme on tkinter (Shombith, 2026-11-02: "the website theme with movie poster text"): the
# console's tokens, Impact for Bebas Neue, Consolas for Space Mono, Cosmic Shore's own card art behind the title.

ART_DIR = os.path.join(os.path.dirname(APP), "art")


def fetch_art(hub, key):
    """The window's two images from the hub (the banner, the logo) into ~/.froglet-compute/art; what is there
    already is kept. Never fatal: without them the window is plain."""
    import urllib.request
    out = {}
    for name in ("setup-banner.png", "cosmicshore.png"):
        path = os.path.join(ART_DIR, name)
        if not os.path.isfile(path) and hub and key:
            try:
                os.makedirs(ART_DIR, exist_ok=True)
                with urllib.request.urlopen(f"{hub.rstrip('/')}/install/{name}?key={key}", timeout=6) as r, open(path + ".part", "wb") as fh:
                    fh.write(r.read())
                os.replace(path + ".part", path)
            except Exception:
                continue
        if os.path.isfile(path):
            out[name] = path
    return out


def theme(root, tk, ttk):
    """The console's dark theme on tkinter: black ground, the console's text and cyan accent, a marquee title (Impact
    stands in for Bebas Neue, Consolas for Space Mono). Shombith, 2026-11-02."""
    T = THEME
    root.configure(bg=T["bg"])
    st = ttk.Style(root)
    try:
        st.theme_use("clam")
    except Exception:
        pass
    body = ("Segoe UI", 10) if os.name == "nt" else ("Helvetica", 11)
    st.configure(".", background=T["bg"], foreground=T["text"], font=body, bordercolor=T["line"], lightcolor=T["bg"], darkcolor=T["bg"])
    st.configure("TFrame", background=T["bg"])
    st.configure("Panel.TFrame", background=T["panel"])
    st.configure("TLabel", background=T["bg"], foreground=T["text"])
    st.configure("Dim.TLabel", foreground=T["dim"])
    st.configure("Ok.TLabel", foreground=T["ok"])
    st.configure("Bad.TLabel", foreground=T["bad"])
    st.configure("Title.TLabel", font=("Impact", 30) if os.name == "nt" else ("Helvetica", 28, "bold"), foreground=T["text"])
    st.configure("Banner.TFrame", background="#000000")
    st.configure("Head.TLabel", font=("Impact", 13) if os.name == "nt" else ("Helvetica", 12, "bold"), foreground=T["accent"])
    st.configure("TRadiobutton", background=T["bg"], foreground=T["text"], indicatorcolor=T["field"], indicatormargin=4)
    st.map("TRadiobutton", background=[("active", T["bg"])], indicatorcolor=[("selected", T["accent"])])
    st.configure("TCheckbutton", background=T["bg"], foreground=T["text"], indicatorcolor=T["field"])
    st.map("TCheckbutton", background=[("active", T["bg"])], indicatorcolor=[("selected", T["accent"])])
    st.configure("TEntry", fieldbackground=T["field"], foreground=T["text"], insertcolor=T["text"], bordercolor=T["line"])
    st.configure("TButton", background=T["panel"], foreground=T["text"], bordercolor=T["line"], padding=(14, 6), font=body)
    st.map("TButton", background=[("active", T["line"]), ("disabled", T["panel"])], foreground=[("disabled", T["dim"])])
    st.configure("Accent.TButton", background=T["accent"], foreground=T["bg"], font=(body[0], body[1], "bold"))
    st.map("Accent.TButton", background=[("active", "#5fe3ff"), ("disabled", T["line"])], foreground=[("disabled", T["dim"])])
    st.configure("Horizontal.TProgressbar", troughcolor=T["field"], background=T["accent"], bordercolor=T["bg"], lightcolor=T["accent"], darkcolor=T["accent"], thickness=6)


def installed_app():
    """The app copy's compute.py when Froglet Compute is installed on this computer, else None."""
    path = os.path.join(APP, "Tools", "Compute", "compute.py")
    return path if os.path.isfile(path) else None


def gui(argv):
    """The installer window in the console's look: the folder (Browse; the current one when it is a
    checkout, never searched for), when the computer may work, Install with a progress bar and a log, Uninstall when
    it is installed (the tray keeps Quit only: Shombith, 2026-11-02), then the tray. No terminal: the .bat and .sh
    the console serves start this (Shombith, 2026-11-01: "why can't there be a good looking installer")."""
    global SINK
    import threading
    import tkinter as tk
    from tkinter import ttk, filedialog, messagebox
    T = THEME
    root = tk.Tk()
    root.title("Froglet Compute Setup")
    root.resizable(False, False)
    theme(root, tk, ttk)
    hub = argv[argv.index("--hub") + 1] if "--hub" in argv and argv.index("--hub") + 1 < len(argv) else ""
    key = argv[argv.index("--hub-key") + 1] if "--hub-key" in argv and argv.index("--hub-key") + 1 < len(argv) else ""
    art = fetch_art(hub, key)
    have = installed_app()
    outer = ttk.Frame(root); outer.grid()
    # The banner: Cosmic Shore's card art behind the wordmark and a movie-poster title (a Canvas draws over an image).
    W, H = 860, 300
    banner = tk.Canvas(outer, width=W, height=H, bg="#000000", highlightthickness=0, bd=0)
    banner.grid(column=0, row=0, sticky="ew")
    keep = []
    if art.get("setup-banner.png"):
        try:
            img = tk.PhotoImage(file=art["setup-banner.png"]); keep.append(img)
            banner.create_image(0, 0, image=img, anchor="nw")
        except Exception:
            pass
    if art.get("cosmicshore.png"):
        try:
            logo = tk.PhotoImage(file=art["cosmicshore.png"]); keep.append(logo)
            banner.create_image(W - 26, 22, image=logo, anchor="ne")
        except Exception:
            pass
    banner.create_text(28, H - 62, text="FROGLET COMPUTE", anchor="sw", fill=T["text"], font=("Impact", 44) if os.name == "nt" else ("Helvetica", 36, "bold"))
    banner.create_text(30, H - 30, anchor="sw", fill=T["accent"], font=("Consolas", 10) if os.name == "nt" else ("Menlo", 10),
                       text=("INSTALLED ON THIS COMPUTER  ·  " if have else "") + "THIS COMPUTER TRAINS THE TEAM'S AI WHILE YOU ARE AWAY")
    banner.image_refs = keep
    f = ttk.Frame(outer, padding=(26, 18, 26, 22)); f.grid(column=0, row=1, sticky="ew")
    ttk.Label(f, text="YOUR COSMIC-SHORE FOLDER", style="Head.TLabel").grid(column=0, row=2, columnspan=3, sticky="w", pady=(0, 4))
    folder = tk.StringVar(value=os.getcwd() if is_checkout(os.getcwd()) else "")    # never searched for (Shombith, 2026-11-01)
    ent = ttk.Entry(f, textvariable=folder, width=64); ent.grid(column=0, row=3, columnspan=2, sticky="ew", padx=(0, 8))
    def browse():
        d = filedialog.askdirectory(title="Your Cosmic-Shore folder (the one you open in Unity)")
        if d: folder.set(d)
    ttk.Button(f, text="Browse…", command=browse).grid(column=2, row=3, sticky="ew")
    found = ttk.Label(f, text="this folder" if folder.get() else "press Browse and pick the folder you open in Unity", style="Ok.TLabel" if folder.get() else "Dim.TLabel")
    found.grid(column=0, row=4, columnspan=3, sticky="w")
    ttk.Label(f, text="WHEN MAY IT WORK?", style="Head.TLabel").grid(column=0, row=5, columnspan=3, sticky="w", pady=(16, 4))
    whens = [("away", "Whenever I'm away (10 minutes idle, on mains power)"), ("nights", "Only at night (22:00 to 07:00)"),
             ("hours", "Every day between two hours"), ("sent", "Only when someone sends a run from the console"),
             ("always", "All the time (nobody uses this computer)")]
    when = tk.StringVar(value="away")
    for i, (k, lab) in enumerate(whens):
        ttk.Radiobutton(f, text=lab, value=k, variable=when).grid(column=0, row=6 + i, columnspan=3, sticky="w", pady=1)
    hrs = ttk.Frame(f); hrs.grid(column=0, row=11, columnspan=3, sticky="w", padx=(24, 0))
    frm, to = tk.StringVar(value="20:00"), tk.StringVar(value="06:00")
    ttk.Label(hrs, text="from").grid(column=0, row=0); ttk.Entry(hrs, textvariable=frm, width=6).grid(column=1, row=0, padx=4)
    ttk.Label(hrs, text="to").grid(column=2, row=0); ttk.Entry(hrs, textvariable=to, width=6).grid(column=3, row=0, padx=4)
    ttk.Label(hrs, text="(only for \"every day between two hours\")", style="Dim.TLabel").grid(column=4, row=0, padx=(8, 0))
    remote = tk.BooleanVar(value=True)
    ttk.Checkbutton(f, text="A run sent from the console may start at once, even while I use this computer", variable=remote).grid(column=0, row=12, columnspan=3, sticky="w", pady=(8, 0))
    STEPS = 6
    bar = ttk.Progressbar(f, orient="horizontal", mode="determinate", maximum=STEPS, value=0)
    bar.grid(column=0, row=13, columnspan=3, sticky="ew", pady=(16, 0))
    stage = ttk.Label(f, text="", style="Dim.TLabel"); stage.grid(column=0, row=14, columnspan=3, sticky="w", pady=(4, 0))
    log = tk.Text(f, height=7, width=84, state="disabled", font=("Consolas" if os.name == "nt" else "Menlo", 9),
                  bg=T["panel"], fg=T["dim"], insertbackground=T["text"], relief="flat", highlightthickness=1, highlightbackground=T["line"], padx=10, pady=8)
    log.grid(column=0, row=15, columnspan=3, sticky="ew", pady=(8, 0))
    buttons = ttk.Frame(f); buttons.grid(column=0, row=16, columnspan=3, sticky="e", pady=(14, 0))
    install_btn = ttk.Button(buttons, text="Install", style="Accent.TButton")
    uninstall_btn = ttk.Button(buttons, text="Uninstall…")
    ttk.Button(buttons, text="Close", command=root.destroy).grid(column=0, row=0, padx=4)
    if have:
        uninstall_btn.grid(column=1, row=0, padx=4)
    install_btn.grid(column=2, row=0)

    def write(msg):
        def put():
            log.config(state="normal"); log.insert("end", msg + "\n"); log.see("end"); log.config(state="disabled")
            head = msg.strip()[:2]
            if len(head) == 2 and head[0].isdigit() and head[1] == ".":       # "3. registering the worker" -> step 3
                bar.config(value=min(STEPS, int(head[0]))); stage.config(text=msg.strip()[3:80])
        root.after(0, put)
    SINK = write

    def busy(on):
        for b in (install_btn, uninstall_btn):
            b.config(state="disabled" if on else "normal")
        if on:
            bar.config(mode="indeterminate"); bar.start(12)
        else:
            bar.stop(); bar.config(mode="determinate")

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
        busy(True); stage.config(text="Installing…")
        def work():
            try:
                rc = main(args)
                root.after(0, lambda: busy(False))
                write("" if rc == 0 else f"stopped ({rc})")
                if rc == 0:
                    write("Done. The tray icon is yours now: it shows what this computer does, pauses it, changes this schedule and opens the console.")
                    root.after(0, lambda: (bar.config(value=STEPS), stage.config(text="Installed. This computer is on the console.", style="Ok.TLabel"),
                                           install_btn.config(text="Done", style="Accent.TButton", command=root.destroy), uninstall_btn.grid(column=1, row=0, padx=4)))
                else:
                    root.after(0, lambda: stage.config(text=f"Stopped ({rc}): the log says why.", style="Bad.TLabel"))
            except SystemExit as e:
                write(str(e)); root.after(0, lambda: (busy(False), stage.config(text="Stopped: the log says why.", style="Bad.TLabel")))
            except Exception as e:
                write(f"{type(e).__name__}: {e}"); root.after(0, lambda: (busy(False), stage.config(text="Stopped: the log says why.", style="Bad.TLabel")))
        threading.Thread(target=work, daemon=True).start()
    install_btn.config(command=run)

    def uninstall():
        app = installed_app()
        if not app:
            messagebox.showinfo("Froglet Compute", "Froglet Compute is not installed on this computer."); return
        if not messagebox.askyesno("Froglet Compute", "Remove Froglet Compute from this computer? The agent and the tray stop and leave the login "
                                   "items. The console forgets the computer when its results branch is removed there (Remove this computer). "
                                   "Your Cosmic-Shore folder is untouched."):
            return
        busy(True); stage.config(text="Uninstalling…", style="Dim.TLabel")
        def work():
            p = subprocess.run([sys.executable, app, "uninstall"], capture_output=True, text=True, **NO_WINDOW)
            for line in (p.stdout + p.stderr).strip().splitlines():
                write(line)
            lock = os.path.join(os.path.dirname(APP), "agent.lock")
            still = False
            try:                                            # the agent's lock still held means an agent survived
                fh = open(lock, "a+")
                try:
                    if os.name == "nt":
                        import msvcrt
                        fh.seek(0); msvcrt.locking(fh.fileno(), msvcrt.LK_NBLCK, 1); msvcrt.locking(fh.fileno(), msvcrt.LK_UNLCK, 1)
                    else:
                        import fcntl
                        fcntl.flock(fh.fileno(), fcntl.LOCK_EX | fcntl.LOCK_NB); fcntl.flock(fh.fileno(), fcntl.LOCK_UN)
                except OSError:
                    still = True
                fh.close()
            except OSError:
                pass
            ok = p.returncode == 0 and not still
            write("Removed: the agent and the tray are stopped and will not start at login." if ok else
                  ("An agent is still running; sign out and in, or restart this computer, and it is gone." if still else f"uninstall stopped ({p.returncode})"))
            root.after(0, lambda: (busy(False), stage.config(text="Removed." if ok else "Not fully removed: the log says why.", style="Ok.TLabel" if ok else "Bad.TLabel"),
                                   uninstall_btn.grid_remove() if ok else None))
        threading.Thread(target=work, daemon=True).start()
    uninstall_btn.config(command=uninstall)

    root.update()
    os.makedirs(os.path.dirname(SETUP_LOG), exist_ok=True)
    root.lift(); root.attributes("-topmost", True); root.after(300, lambda: root.attributes("-topmost", False))
    opened = f"the setup window opened (python {sys.version.split()[0]}, {sys.executable})"
    print(opened, flush=True)                 # the .bat keeps this transcript and stays open when this line is missing
    try:
        with open(SETUP_LOG, "a", encoding="utf-8") as fh:
            fh.write(time.strftime("%Y-%m-%d %H:%M:%S ") + opened + "\n")
    except OSError:
        pass
    if os.name == "nt":
        # The .bat runs this under the console python, so a failure before this line is printed there and the .bat
        # pauses on it (pythonw swallowed it: Shombith, 2026-11-02, "checking PC... window closed"). From here the
        # console is hidden; the window is what the person sees.
        try:
            import ctypes
            hwnd = ctypes.windll.kernel32.GetConsoleWindow()
            if hwnd:
                ctypes.windll.user32.ShowWindow(hwnd, 0)
        except Exception:
            pass
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


SETUP_LOG = os.path.join(os.path.expanduser("~"), ".froglet-compute", "setup.log")


def alert(text):
    """Show `text` to the person even with no console (the .bat starts the window under pythonw): a Windows message
    box, else stderr; and the setup log. Shombith, 2026-11-02: "something opens and closes" - a silent failure."""
    try:
        os.makedirs(os.path.dirname(SETUP_LOG), exist_ok=True)
        with open(SETUP_LOG, "a", encoding="utf-8") as fh:
            fh.write(time.strftime("%Y-%m-%d %H:%M:%S ") + text.rstrip() + "\n")
    except OSError:
        pass
    sys.stderr.write(text + "\n")          # the console, when there is one (the .bat pauses on it)
    if os.name == "nt":
        try:
            import ctypes
            ctypes.windll.user32.MessageBoxW(0, text + "\n\nThis is also in " + SETUP_LOG, "Froglet Compute Setup", 0x10)
        except Exception:
            pass


def gui_check():
    """What the .bat runs in its console before starting the window: Python and tkinter must work, or it says why
    and stays open."""
    print(f"python {sys.version.split()[0]} at {sys.executable}")
    if sys.version_info < (3, 8):
        print("stopped: Python 3.8 or newer is needed"); return 1
    try:
        import tkinter
        from tkinter import ttk  # noqa: F401
        tkinter.Tcl()
    except Exception as e:
        print(f"stopped: this Python has no working tkinter ({e}). Install Python 3 from python.org and tick "
              "\"tcl/tk and IDLE\", then run this again."); return 1
    if not shutil.which("git"):
        print("stopped: git is not on this computer's PATH"); return 1
    print("ok: the setup window can open")
    return 0


def main(argv):
    opts = set(argv)
    if "--gui-check" in opts:
        return gui_check()
    if "--gui" in opts:
        try:
            return gui(argv)
        except SystemExit:
            raise
        except BaseException as e:               # pythonw has nowhere to print: show it
            import traceback
            alert("The setup window could not open:\n" + "".join(traceback.format_exception(type(e), e, e.__traceback__))[-1500:])
            return 1
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
