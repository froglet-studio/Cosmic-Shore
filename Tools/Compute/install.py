"""Set and forget: start the worker whenever this computer is idle - `compute.py install` / `uninstall`.

    Windows  a Task Scheduler task "FrogletCompute" with an ON IDLE trigger (schtasks /SC ONIDLE /I <minutes>);
             Windows stops it when you come back, and the worker also yields by itself
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


def _cmd(minutes, between=None, always=False):
    cmd = [sys.executable, os.path.join(HERE, "compute.py"), "run", "--when-idle", str(minutes), "--quiet"]
    if between:
        cmd += ["--between", between]
    if always:
        cmd += ["--always"]
    return cmd


def install(minutes=10, dry=False, between=None, always=False):
    system = platform.system()
    if between:
        import compute_lib
        try:
            compute_lib.parse_window(between)       # fail here, not at 3 am
        except ValueError as e:
            print(e)
            return 2
    cmd = _cmd(minutes, between, always)
    log = os.path.join(os.path.expanduser("~"), ".froglet-compute", "worker.log")
    os.makedirs(os.path.dirname(log), exist_ok=True)
    if system == "Windows":
        tr = " ".join(f'"{c}"' for c in cmd)
        args = ["schtasks", "/Create", "/F", "/TN", "FrogletCompute", "/SC", "ONIDLE", "/I", str(minutes), "/TR", tr]
        return _do(args, dry, f"Task Scheduler: FrogletCompute (on idle {minutes} min)")
    if system == "Darwin":
        plist = os.path.join(os.path.expanduser("~"), "Library", "LaunchAgents", LABEL + ".plist")
        body = ('<?xml version="1.0" encoding="UTF-8"?>\n'
                '<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">\n'
                '<plist version="1.0"><dict>\n'
                f'  <key>Label</key><string>{LABEL}</string>\n'
                '  <key>ProgramArguments</key><array>' + "".join(f"<string>{c}</string>" for c in cmd) + '</array>\n'
                f'  <key>WorkingDirectory</key><string>{REPO}</string>\n'
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
                f"WorkingDirectory={REPO}\nExecStart={' '.join(cmd)}\nRestart=always\nRestartSec=60\nNice=19\n"
                "IOSchedulingClass=idle\n\n[Install]\nWantedBy=default.target\n")
        if dry:
            print(body)
            return 0
        os.makedirs(unit_dir, exist_ok=True)
        with open(unit, "w") as fh:
            fh.write(body)
        subprocess.run(["systemctl", "--user", "daemon-reload"], capture_output=True)
        return _do(["systemctl", "--user", "enable", "--now", "froglet-compute.service"], False, f"systemd user unit {unit}")
    line = f"@reboot cd {REPO} && {' '.join(cmd)} >> {log} 2>&1  # froglet-compute"
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
        return _do(["schtasks", "/Delete", "/F", "/TN", "FrogletCompute"], False, "removed FrogletCompute")
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


def _do(args, dry, what):
    if dry:
        print(" ".join(args))
        return 0
    p = subprocess.run(args, capture_output=True, text=True)
    if p.returncode != 0:
        print(f"failed: {' '.join(args)}\n{p.stderr or p.stdout}")
        return p.returncode
    print("installed: " + what)
    return 0
