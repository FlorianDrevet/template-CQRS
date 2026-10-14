"""Generation of the Azure DevOps code wiki (docs/wiki) from the documentation sources."""

import posixpath
import re
import shutil
from pathlib import Path

from docstool.convert import convert_for_wiki
from docstool.errors import DocsError
from docstool.fences import closes_fence, opening_fence
from docstool.includes import resolve_includes
from docstool.nav import Navigation, Page

LINK_PATTERN = re.compile(r"(?P<bang>!?)\[(?P<text>[^\]]*)\]\((?P<target>[^)\s]+)\)")
UNCHANGED_TARGETS = ("http://", "https://", "mailto:", "#")
GENERATED_NOTICE = (
    "This folder is generated from docs/site by: python eng/docs/docs.py wiki\n"
    "Do not edit it by hand: edit docs/site, then regenerate.\n"
)


def rewrite_links(text: str, page: Page, pages_by_source: dict[str, Page]) -> str:
    rewritten: list[str] = []
    marker: str | None = None
    for line in text.split("\n"):
        if marker is not None:
            if closes_fence(line, marker):
                marker = None
            rewritten.append(line)
            continue
        fence = opening_fence(line)
        if fence:
            marker = fence["marker"]
            rewritten.append(line)
            continue
        rewritten.append(LINK_PATTERN.sub(lambda match: _rewrite(match, page, pages_by_source), line))
    return "\n".join(rewritten)


def _rewrite(match: re.Match[str], page: Page, pages_by_source: dict[str, Page]) -> str:
    target = match["target"]
    if target.startswith(UNCHANGED_TARGETS):
        return match[0]
    path, _, anchor = target.partition("#")
    resolved = posixpath.normpath(posixpath.join(posixpath.dirname(page.source), path))
    suffix = f"#{anchor}" if anchor else ""
    if match["bang"]:
        if not resolved.startswith("assets/"):
            raise DocsError(f"{page.source}: images must live under docs/site/assets: {target}")
        return f"![{match['text']}](/.attachments/{posixpath.basename(resolved)})"
    linked = pages_by_source.get(resolved)
    if linked is None:
        raise DocsError(f"{page.source}: link to a page that is not in the navigation: {target}")
    return f"[{match['text']}](/{linked.wiki_path}{suffix})"


def build_wiki(site_dir: Path, repo_root: Path, navigation: Navigation, output_dir: Path) -> None:
    if output_dir.exists():
        shutil.rmtree(output_dir)
    output_dir.mkdir(parents=True)

    pages_by_source = {page.source: page for page in navigation.pages}
    for page in navigation.pages:
        text = (site_dir / page.source).read_text(encoding="utf-8")
        text = resolve_includes(text, page.source, repo_root)
        text = rewrite_links(text, page, pages_by_source)
        text = convert_for_wiki(text)
        target = output_dir / page.wiki_path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding="utf-8", newline="\n")

    for folder, names in navigation.orders.items():
        order_file = output_dir / folder / ".order"
        order_file.parent.mkdir(parents=True, exist_ok=True)
        order_file.write_text("\n".join(names) + "\n", encoding="utf-8", newline="\n")

    assets = site_dir / "assets"
    if assets.is_dir():
        attachments = output_dir / ".attachments"
        attachments.mkdir()
        seen: set[str] = set()
        for asset in sorted(path for path in assets.rglob("*") if path.is_file()):
            if asset.name in seen:
                raise DocsError(f"docs/site/assets: duplicate file name {asset.name}")
            seen.add(asset.name)
            shutil.copyfile(asset, attachments / asset.name)

    (output_dir / "GENERATED.txt").write_text(GENERATED_NOTICE, encoding="utf-8", newline="\n")
