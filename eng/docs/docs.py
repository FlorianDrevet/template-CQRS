"""Documentation tooling for the template.

Usage: python eng/docs/docs.py {resolve|wiki|build|serve|check}
  resolve  copy docs/site to build/docs-src with code includes resolved
  wiki     regenerate the Azure DevOps code wiki in docs/wiki
  build    resolve + wiki + Zensical site in site/
  serve    resolve + Zensical preview server (re-run after editing docs/site)
  check    authoring rules, includes, navigation and docs/wiki freshness (no writes)
"""

import argparse
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from docstool.commands import Paths, build, check, generate_wiki, resolve, serve  # noqa: E402
from docstool.errors import DocsError  # noqa: E402


def main() -> int:
    parser = argparse.ArgumentParser(description="Documentation tooling for the template.")
    parser.add_argument("command", choices=["resolve", "wiki", "build", "serve", "check"])
    command = parser.parse_args().command
    paths = Paths(Path(__file__).resolve().parents[2])
    try:
        if command == "check":
            errors = check(paths)
            for error in errors:
                print(error, file=sys.stderr)
            if errors:
                return 1
            print("docs check: OK")
            return 0
        {"resolve": resolve, "wiki": generate_wiki, "build": build, "serve": serve}[command](paths)
    except DocsError as error:
        print(error, file=sys.stderr)
        return 1
    except FileNotFoundError as error:
        print(
            f"docs: command not found ({error.filename or error}); install the tooling with: "
            "python -m pip install -r eng/docs/requirements.txt",
            file=sys.stderr,
        )
        return 1
    except subprocess.CalledProcessError as error:
        print(f"docs: command failed with exit code {error.returncode}: {' '.join(map(str, error.cmd))}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
