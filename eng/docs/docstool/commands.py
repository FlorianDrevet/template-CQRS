"""Documentation commands: resolve, wiki, build, serve and check."""

import shutil
import subprocess
import tempfile
from dataclasses import dataclass
from pathlib import Path

from docstool.errors import DocsError
from docstool.includes import resolve_includes
from docstool.nav import load_navigation
from docstool.style import check_page
from docstool.wiki import build_wiki


@dataclass(frozen=True)
class Paths:
    repo_root: Path

    @property
    def site_dir(self) -> Path:
        return self.repo_root / "docs" / "site"

    @property
    def wiki_dir(self) -> Path:
        return self.repo_root / "docs" / "wiki"

    @property
    def resolved_dir(self) -> Path:
        return self.repo_root / "build" / "docs-src"

    @property
    def config(self) -> Path:
        return self.repo_root / "mkdocs.yml"


def resolve(paths: Paths) -> None:
    if paths.resolved_dir.exists():
        shutil.rmtree(paths.resolved_dir)
    shutil.copytree(paths.site_dir, paths.resolved_dir)
    for page in sorted(paths.resolved_dir.rglob("*.md")):
        source = page.relative_to(paths.resolved_dir).as_posix()
        text = resolve_includes(page.read_text(encoding="utf-8"), source, paths.repo_root)
        page.write_text(text, encoding="utf-8", newline="\n")


def generate_wiki(paths: Paths, output_dir: Path | None = None) -> None:
    build_wiki(paths.site_dir, paths.repo_root, load_navigation(paths.config), output_dir or paths.wiki_dir)


def build(paths: Paths) -> None:
    resolve(paths)
    generate_wiki(paths)
    subprocess.run(["zensical", "build", "--strict", "-f", str(paths.config)], cwd=paths.repo_root, check=True)


def serve(paths: Paths) -> None:
    resolve(paths)
    subprocess.run(["zensical", "serve", "-f", str(paths.config)], cwd=paths.repo_root, check=True)


def check(paths: Paths) -> list[str]:
    try:
        navigation = load_navigation(paths.config)
    except DocsError as error:
        return [str(error)]

    errors: list[str] = []
    in_navigation = {page.source for page in navigation.pages}
    on_disk = {page.relative_to(paths.site_dir).as_posix() for page in paths.site_dir.rglob("*.md")}
    errors.extend(f"mkdocs.yml: navigation page not found: {source}" for source in sorted(in_navigation - on_disk))
    errors.extend(f"docs/site/{source}: page is not in the navigation" for source in sorted(on_disk - in_navigation))
    for source in sorted(on_disk):
        text = (paths.site_dir / source).read_text(encoding="utf-8")
        errors.extend(check_page(text, source))
        try:
            resolve_includes(text, source, paths.repo_root)
        except DocsError as error:
            errors.append(str(error))
    if errors:
        return errors

    with tempfile.TemporaryDirectory() as temporary:
        expected = Path(temporary) / "wiki"
        try:
            generate_wiki(paths, expected)
        except DocsError as error:
            return [str(error)]
        return _compare_trees(expected, paths.wiki_dir)


def _compare_trees(expected: Path, actual: Path) -> list[str]:
    if not actual.is_dir():
        return ["docs/wiki is missing: run python eng/docs/docs.py wiki"]
    expected_files = _files(expected)
    actual_files = _files(actual)
    stale = sorted(expected_files ^ actual_files)
    stale += sorted(
        name for name in expected_files & actual_files
        if _normalized(expected / name) != _normalized(actual / name)
    )
    return [f"docs/wiki is out of date ({name}): run python eng/docs/docs.py wiki" for name in stale]


def _files(root: Path) -> set[str]:
    return {path.relative_to(root).as_posix() for path in root.rglob("*") if path.is_file()}


def _normalized(path: Path) -> bytes:
    return path.read_bytes().replace(b"\r\n", b"\n")
