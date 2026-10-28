"""Join Froglet Compute from your own Cosmic-Shore checkout, in one line. The Compute Console's Install button shows it:

    macOS / Linux (Terminal), from your Cosmic-Shore folder:
        git fetch origin <ref> && git show origin/<ref>:Tools/Compute/setup.py | python3 -
    Windows (PowerShell), from your Cosmic-Shore folder:
        git fetch origin <ref>; git show origin/<ref>:Tools/Compute/setup.py | python -

<ref> is bleeding-edge once Froglet Compute is merged there, and the compute branch (APP_REF) until then.

Options go after the dash (`... | python3 - --nights`):
    --nights          work only 22:00-07:00 instead of whenever you are away
    --always          a computer nobody sits at (a VM): work all the time
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


def say(msg=""):
    print(msg, flush=True)


def git(*args, check=True):
    p = subprocess.run(["git", *args], capture_output=True, text=True)
    if check and p.returncode:
        raise SystemExit(f"\nstopped: git {' '.join(args)} failed:\n{(p.stderr or p.stdout).strip()}")
    return p.stdout.strip()


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
    if "--always" in opts:
        args += ["--always"]
    say("3. registering the worker: " + " ".join(args[2:]))
    if not dry:                     # so commands typed by hand (compute.py run --request ...) find this checkout
        cfg = {"repo": repo, "ref": ref, "queue": queue}
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
    say("\ndone. It now works whenever you are away. See it on the Compute Console (Machines and Runs).")
    say("Stop it any time:  git show origin/" + ref + ":Tools/Compute/setup.py | python3 - --uninstall")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
