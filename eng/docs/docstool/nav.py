"""Navigation of mkdocs.yml -> documentation pages, Azure DevOps wiki paths and .order files."""

import re
from dataclasses import dataclass
from pathlib import Path

import yaml

from docstool.errors import DocsError

FORBIDDEN_CHARACTERS = set('\\/:*?"<>|#%')


@dataclass(frozen=True)
class Page:
    source: str
    title: str
    wiki_path: str


@dataclass(frozen=True)
class Navigation:
    pages: list[Page]
    orders: dict[str, list[str]]


class _ConfigLoader(yaml.SafeLoader):
    """Safe loader that ignores the !!python/name tags used by Markdown extensions."""


_ConfigLoader.add_multi_constructor("tag:yaml.org,2002:python/", lambda loader, suffix, node: None)


def wiki_name(title: str) -> str:
    cleaned = "".join(character for character in title if character not in FORBIDDEN_CHARACTERS).strip()
    cleaned = cleaned.replace("-", "%2D")
    return re.sub(r"\s+", "-", cleaned)


def load_navigation(config_path: Path) -> Navigation:
    config = yaml.load(config_path.read_text(encoding="utf-8"), Loader=_ConfigLoader)
    nav = config.get("nav") if isinstance(config, dict) else None
    if not isinstance(nav, list):
        raise DocsError(f"{config_path.name}: missing nav")
    pages: list[Page] = []
    orders: dict[str, list[str]] = {}
    _walk(nav, "", pages, orders)
    return Navigation(pages, orders)


def _walk(entries: list[object], folder: str, pages: list[Page], orders: dict[str, list[str]]) -> None:
    for entry in entries:
        if not (isinstance(entry, dict) and len(entry) == 1):
            raise DocsError(f"mkdocs.yml: every navigation entry needs a title: {entry!r}")
        ((title, value),) = entry.items()
        name = wiki_name(str(title))
        if not name:
            raise DocsError(f'mkdocs.yml: navigation title "{title}" gives an empty wiki page name')
        siblings = orders.setdefault(folder, [])
        if name in siblings:
            raise DocsError(f'mkdocs.yml: duplicate wiki page name "{name}" in folder "{folder or "(root)"}"')
        siblings.append(name)
        location = f"{folder}/{name}" if folder else name
        if isinstance(value, str):
            pages.append(Page(value, str(title), f"{location}.md"))
        elif isinstance(value, list):
            if not value or not isinstance(value[0], str) or not value[0].endswith("index.md"):
                raise DocsError(f'mkdocs.yml: section "{title}" must start with its index.md page')
            pages.append(Page(value[0], str(title), f"{location}.md"))
            if len(value) > 1:
                _walk(value[1:], location, pages, orders)
        else:
            raise DocsError(f"mkdocs.yml: unsupported navigation entry: {entry!r}")
