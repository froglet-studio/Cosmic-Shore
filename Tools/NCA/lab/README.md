# NCA Ecology Lab: the part that can run on Froglet Compute

Garrett's lab (a Notion backlog, a runner on his PC, his GPU) is described in `Docs/Compute/NCA_LAB.md`, which also
explains why only these files live here. The runner, `jobs.py`, `notion_client.py` and `bootstrap.ps1` stay in his
Drive folder `nca_ecology_lab`.

| file | run it |
|---|---|
| `nca3d_trainer.py` | `python3 nca3d_trainer.py --config "job nca3d \| target sphere \| iters 4000" --out runs/a1` (`--resume` to continue) |
| `diamond_nca.py` | `python3 diamond_nca.py --config "job diamond_nca \| complexity params" --out runs/a4` (A4) |
| `pareto_complexity.py` | `python3 pareto_complexity.py` (the optimizer's toy demonstration) |
| `notion_writeback.py` | `python3 notion_writeback.py push` (dry run; `--apply` with `NOTION_TOKEN` set) / `steer` |
| `selftest.py` | `python3 selftest.py` (`NCA_LAB_PYTHON=<python with torch>` adds the trainer and worker checks) |

Requirements for training: `numpy`, `torch` (CUDA build for a real run), and `imageio` for the GIF. The Notion token
goes in the environment or a `.env` here (git-ignored), never in a commit.
