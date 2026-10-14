"""Text-anchored code includes: --8<-- "path" start=V end=V | before=V, where V is "text" or 'text'."""

import re
import textwrap
from pathlib import Path

from docstool.errors import DocsError

VALUE = r'(?:"[^"]+"|\'[^\']+\')'
INCLUDE_PATTERN = re.compile(
    r'^(?P<indent>[ \t]*)--8<-- "(?P<path>[^"]+)"'
    rf"(?: start=(?P<start>{VALUE}))?"
    rf"(?: (?:end=(?P<end>{VALUE})|before=(?P<before>{VALUE})))?[ \t]*$"
)


def is_include(line: str) -> bool:
    return line.lstrip().startswith("--8<--")


def unquote(value: str | None) -> str | None:
    return None if value is None else value[1:-1]


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
        snippet = read_snippet(
            repo_root,
            match["path"],
            unquote(match["start"]),
            unquote(match["end"]),
            unquote(match["before"]),
            where,
        )
        resolved.extend(match["indent"] + content if content else "" for content in snippet)
    return "\n".join(resolved)


def read_snippet(
    repo_root: Path,
    relative_path: str,
    start: str | None,
    end: str | None,
    before: str | None,
    where: str,
) -> list[str]:
    path = repo_root / relative_path
    if not path.is_file():
        raise DocsError(f"{where}: included file not found: {relative_path}")

    lines = path.read_text(encoding="utf-8-sig").replace("\r\n", "\n").split("\n")
    if lines and lines[-1] == "":
        lines = lines[:-1]

    first = find_start(lines, start, relative_path, where)
    if end is not None:
        last = find_end(lines, first, end, relative_path, where)
    elif before is not None:
        last = find_before(lines, first, before, relative_path, where) - 1
    else:
        last = len(lines) - 1

    return textwrap.dedent("\n".join(lines[first:last + 1])).split("\n")


def find_start(lines: list[str], start: str | None, relative_path: str, where: str) -> int:
    if start is None:
        return 0
    matches = [index for index, content in enumerate(lines) if start in content]
    if not matches:
        raise DocsError(f'{where}: start anchor "{start}" not found in {relative_path}')
    if len(matches) > 1:
        numbers = ", ".join(str(index + 1) for index in matches)
        raise DocsError(f'{where}: start anchor "{start}" is ambiguous in {relative_path} (lines {numbers})')
    return matches[0]


def find_end(lines: list[str], first: int, end: str, relative_path: str, where: str) -> int:
    following = [index for index in range(first, len(lines)) if end in lines[index]]
    if not following:
        raise DocsError(f'{where}: end anchor "{end}" not found after line {first + 1} in {relative_path}')
    return following[0]


def find_before(lines: list[str], first: int, before: str, relative_path: str, where: str) -> int:
    following = [index for index in range(first + 1, len(lines)) if before in lines[index]]
    if not following:
        raise DocsError(f'{where}: before anchor "{before}" not found after line {first + 1} in {relative_path}')
    return following[0]
