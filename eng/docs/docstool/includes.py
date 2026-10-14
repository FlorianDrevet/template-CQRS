"""Text-anchored code includes: --8<-- "path" start="..." end="..."."""

import re
import textwrap
from pathlib import Path

from docstool.errors import DocsError

INCLUDE_PATTERN = re.compile(
    r'^(?P<indent>[ \t]*)--8<-- "(?P<path>[^"]+)"'
    r'(?: start="(?P<start>[^"]+)")?'
    r'(?: end="(?P<end>[^"]+)")?[ \t]*$'
)


def is_include(line: str) -> bool:
    return line.lstrip().startswith("--8<--")


def resolve_includes(text: str, page: str, repo_root: Path) -> str:
    resolved: list[str] = []
    for number, line in enumerate(text.split("\n"), start=1):
        if not is_include(line):
            resolved.append(line)
            continue
        where = f"{page}:{number}"
        match = INCLUDE_PATTERN.match(line)
        if match is None:
            raise DocsError(f"{where}: invalid include directive: {line.strip()}")
        snippet = read_snippet(repo_root, match["path"], match["start"], match["end"], where)
        resolved.extend(match["indent"] + content if content else "" for content in snippet)
    return "\n".join(resolved)


def read_snippet(
    repo_root: Path,
    relative_path: str,
    start: str | None,
    end: str | None,
    where: str,
) -> list[str]:
    path = repo_root / relative_path
    if not path.is_file():
        raise DocsError(f"{where}: included file not found: {relative_path}")

    lines = path.read_text(encoding="utf-8-sig").replace("\r\n", "\n").split("\n")
    if lines and lines[-1] == "":
        lines = lines[:-1]

    first = 0
    if start is not None:
        matches = [index for index, content in enumerate(lines) if start in content]
        if not matches:
            raise DocsError(f'{where}: start anchor "{start}" not found in {relative_path}')
        if len(matches) > 1:
            numbers = ", ".join(str(index + 1) for index in matches)
            raise DocsError(f'{where}: start anchor "{start}" is ambiguous in {relative_path} (lines {numbers})')
        first = matches[0]

    last = len(lines) - 1
    if end is not None:
        following = [index for index in range(first, len(lines)) if end in lines[index]]
        if not following:
            raise DocsError(f'{where}: end anchor "{end}" not found after line {first + 1} in {relative_path}')
        last = following[0]

    return textwrap.dedent("\n".join(lines[first:last + 1])).split("\n")
