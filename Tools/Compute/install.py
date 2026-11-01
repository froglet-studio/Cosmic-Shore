"""Set and forget: start the worker whenever this computer is idle - `compute.py install` / `uninstall`.

    Windows  FrogletCompute.vbs in the Startup folder: the agent starts, windowless, at every login and stays (it
             waits for idle itself and keeps the computer on the console while you use it). Until 2026-10-28 this
             was a Task Scheduler ON IDLE task, which only existed while the computer was idle, so the console could
             not see the computer, nor start a run on it, while its person used it; install removes that task.
    macOS    a LaunchAgent (~/Library/LaunchAgents/studio.froglet.compute.plist), kept alive; the worker waits
             for idle itself
    Linux    a systemd user service (~/.config/systemd/user/froglet-compute.service, Restart=always), or a crontab
             @reboot line when systemd is not there

Every one of them runs `python3 Tools/Compute/compute.py run --when-idle <minutes> --quiet` from THIS checkout, so it
uses this checkout's git credentials to publish results to refs/heads/compute/<user>/<host> - and nothing else: it
never touches this checkout's working tree, branch, index or config (the worker runs in its own exports under
~/.froglet-compute). `uninstall` removes exactly what `install` added.
"""
import os
import platform
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
LABEL = "studio.froglet.compute"


def _cmd(minutes, between=None, always=False, home=None, repo=None, queue=None, only_sent=False):
    cmd = [sys.executable, os.path.join(HERE, "compute.py"), "run", "--when-idle", str(minutes), "--quiet"]
    if between:
        cmd += ["--between", between]
    if always:
        cmd += ["--always"]
    if only_sent:
        cmd += ["--only-sent"]
    if home:
        cmd += ["--home", home]
    if repo:
        cmd += ["--repo", os.path.abspath(repo)]
    if queue:
        cmd += ["--queue", *queue]
    return cmd


def windowless(exe):
    """Windows: pythonw.exe beside python.exe, so the worker never opens a console window (python.exe would, every
    time the task starts). Falls back to `exe` when there is no pythonw beside it."""
    w = os.path.join(os.path.dirname(exe), "pythonw.exe")
    return w if os.path.exists(w) else exe


def windows_task(cmd, home_dir, pythonw):
    """(launcher path, launcher text, the task's /TR line). Task Scheduler limits /TR to 261 characters, and a
    Microsoft Store Python path alone is over 100, so the worker's full command lives in a small launcher file and the
    task runs only `"<pythonw>" "<launcher>"`. The launcher also sends the worker's output to worker.log, since a
    windowless Python has nowhere to print."""
    launcher = os.path.join(home_dir, "worker.pyw")
    log = os.path.join(home_dir, "worker.log")
    argv = [a for a in cmd[1:] if a != "--quiet"]          # the log is the only place a windowless worker can speak
    text = ("# Froglet Compute: what the FrogletCompute task in Task Scheduler runs. Written by `compute.py install`;\n"
            "# `compute.py uninstall` removes it. The task line is limited to 261 characters, so the command is here.\n"
            "import os, runpy, sys\n"
            f"LOG = {log!r}\n"
            "if os.path.exists(LOG) and os.path.getsize(LOG) > 5000000:\n"
            "    os.replace(LOG, LOG + '.old')\n"
            "sys.stdout = sys.stderr = open(LOG, 'a', buffering=1, encoding='utf-8', errors='replace')\n"
            f"sys.argv = {argv!r}\n"
            "runpy.run_path(sys.argv[0], run_name='__main__')\n")
    return launcher, text, f'"{pythonw}" "{launcher}"'


def startup_dir():
    return os.path.join(os.environ.get("APPDATA", os.path.expanduser("~")), "Microsoft", "Windows", "Start Menu",
                        "Programs", "Startup")


def windows_startup(pythonw, launcher):
    """(path, text) of the Startup-folder script that starts the agent at login, with no window (wscript runs it;
    every Windows has it, unlike a .pyw file association)."""
    q = lambda s: s.replace('"', '""')
    text = ("' Froglet Compute: starts the agent at login, with no window. `compute.py uninstall` removes this file.\r\n"
            f'CreateObject("WScript.Shell").Run """{q(pythonw)}"" ""{q(launcher)}""", 0, False\r\n')
    return os.path.join(startup_dir(), "FrogletCompute.vbs"), text


def _stop_windows_agents():
    """Stop running agents (the launcher's pythonw) so a new install starts the new code, once."""
    ps = ("Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like '*froglet-compute*worker.pyw*' -or $_.CommandLine -like '*froglet-compute*tray.pyw*' } | "
          "ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }")
    subprocess.run(["powershell", "-NoProfile", "-Command", ps], capture_output=True, creationflags=0x08000000)


def install(minutes=10, dry=False, between=None, always=False, home=None, repo=None, queue=None, only_sent=False):
    system = platform.system()
    if between:
        import compute_lib
        try:
            compute_lib.parse_window(between)       # fail here, not at 3 am
        except ValueError as e:
            print(e)
            return 2
    cmd = _cmd(minutes, between, always, home, repo, queue, only_sent)
    workdir = os.path.abspath(repo) if repo else REPO
    log = os.path.join(os.path.expanduser("~"), ".froglet-compute", "worker.log")
    os.makedirs(os.path.dirname(log), exist_ok=True)
    if system == "Windows":
        launcher, text, tr = windows_task(cmd, os.path.dirname(log), windowless(sys.executable))
        pythonw = windowless(sys.executable)
        vbs, vtext = windows_startup(pythonw, launcher)
        if dry:
            print(f"{launcher}:\n{text}\n{vbs}:\n{vtext}")
            return 0
        with open(launcher, "w", encoding="utf-8") as fh:
            fh.write(text)
        os.makedirs(os.path.dirname(vbs), exist_ok=True)
        with open(vbs, "w", encoding="utf-8") as fh:
            fh.write(vtext)
        # The old ON IDLE task (installs before 2026-10-28) and any running agent go; the new one starts now.
        subprocess.run(["schtasks", "/Delete", "/F", "/TN", "FrogletCompute"], capture_output=True, creationflags=0x08000000)
        _stop_windows_agents()
        subprocess.Popen([pythonw, launcher], cwd=workdir, close_fds=True,
                         creationflags=0x00000008 | 0x00000200 | 0x08000000)   # detached, own group, no window
        print(f"installed: the agent starts at login ({vbs}) and runs now; its log: {log}")
        return 0
    if system == "Darwin":
        plist = os.path.join(os.path.expanduser("~"), "Library", "LaunchAgents", LABEL + ".plist")
        body = ('<?xml version="1.0" encoding="UTF-8"?>\n'
                '<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">\n'
                '<plist version="1.0"><dict>\n'
                f'  <key>Label</key><string>{LABEL}</string>\n'
                '  <key>ProgramArguments</key><array>' + "".join(f"<string>{c}</string>" for c in cmd) + '</array>\n'
                f'  <key>WorkingDirectory</key><string>{workdir}</string>\n'
                '  <key>RunAtLoad</key><true/><key>KeepAlive</key><true/>\n'
                '  <key>ProcessType</key><string>Background</string><key>LowPriorityIO</key><true/><key>Nice</key><integer>19</integer>\n'
                f'  <key>StandardOutPath</key><string>{log}</string><key>StandardErrorPath</key><string>{log}</string>\n'
                '</dict></plist>\n')
        if dry:
            print(body)
            return 0
        os.makedirs(os.path.dirname(plist), exist_ok=True)
        with open(plist, "w") as fh:
            fh.write(body)
        subprocess.run(["launchctl", "unload", plist], capture_output=True)
        return _do(["launchctl", "load", plist], False, f"LaunchAgent {plist}")
    unit_dir = os.path.join(os.path.expanduser("~"), ".config", "systemd", "user")
    if shutil.which("systemctl"):
        unit = os.path.join(unit_dir, "froglet-compute.service")
        body = ("[Unit]\nDescription=Froglet Compute worker (runs when the machine is idle)\n\n[Service]\n"
                f"WorkingDirectory={workdir}\nExecStart={' '.join(cmd)}\nRestart=always\nRestartSec=60\nNice=19\n"
                "IOSchedulingClass=idle\n\n[Install]\nWantedBy=default.target\n")
        if dry:
            print(body)
            return 0
        os.makedirs(unit_dir, exist_ok=True)
        with open(unit, "w") as fh:
            fh.write(body)
        subprocess.run(["systemctl", "--user", "daemon-reload"], capture_output=True)
        return _do(["systemctl", "--user", "enable", "--now", "froglet-compute.service"], False, f"systemd user unit {unit}")
    line = f"@reboot cd {workdir} && {' '.join(cmd)} >> {log} 2>&1  # froglet-compute"
    if dry:
        print(line)
        return 0
    cur = subprocess.run(["crontab", "-l"], capture_output=True, text=True).stdout
    if "froglet-compute" not in cur:
        subprocess.run(["crontab", "-"], input=cur + line + "\n", text=True)
    print("crontab: @reboot line added (start it now: " + " ".join(cmd) + " &)")
    return 0


def uninstall():
    system = platform.system()
    if system == "Windows":
        subprocess.run(["schtasks", "/Delete", "/F", "/TN", "FrogletCompute"], capture_output=True, creationflags=0x08000000)
        _stop_windows_agents()
        for path in (os.path.join(startup_dir(), "FrogletCompute.vbs"),
                     os.path.join(os.path.expanduser("~"), ".froglet-compute", "worker.pyw")):
            if os.path.exists(path):
                os.remove(path)
        print("removed the Froglet Compute agent (Startup folder) and stopped it")
        return 0
    if system == "Darwin":
        plist = os.path.join(os.path.expanduser("~"), "Library", "LaunchAgents", LABEL + ".plist")
        subprocess.run(["launchctl", "unload", plist], capture_output=True)
        if os.path.exists(plist):
            os.remove(plist)
        print("removed " + plist)
        return 0
    if shutil.which("systemctl"):
        subprocess.run(["systemctl", "--user", "disable", "--now", "froglet-compute.service"], capture_output=True)
        unit = os.path.join(os.path.expanduser("~"), ".config", "systemd", "user", "froglet-compute.service")
        if os.path.exists(unit):
            os.remove(unit)
        print("removed " + unit)
        return 0
    cur = subprocess.run(["crontab", "-l"], capture_output=True, text=True).stdout
    subprocess.run(["crontab", "-"], input="".join(l + "\n" for l in cur.splitlines() if "froglet-compute" not in l), text=True)
    print("removed the crontab line")
    return 0


def _do(args, dry, what, verb="installed"):
    if dry:
        print(" ".join(args))
        return 0
    p = subprocess.run(args, capture_output=True, text=True)
    if p.returncode != 0:
        print(f"failed: {' '.join(args)}\n{p.stderr or p.stdout}")
        return 1
    print(f"{verb}: " + what if verb == "installed" else what)
    return 0
