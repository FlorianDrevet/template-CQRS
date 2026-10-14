"""Material-flavoured Markdown -> Azure DevOps wiki Markdown (admonitions, tabs, code block titles)."""

import re

from docstool.errors import DocsError
from docstool.fences import closes_fence, opening_fence

ADMONITIONS: dict[str, tuple[str, str]] = {
    "note": ("📝", "Note"),
    "info": ("ℹ️", "Info"),
    "tip": ("💡", "Astuce"),
    "warning": ("⚠️", "Attention"),
    "danger": ("🛑", "Danger"),
    "example": ("🧪", "Exemple"),
    "question": ("❓", "Question"),
}
ADMONITION_PATTERN = re.compile(r'^(?:!!!|\?\?\?\+?) (?P<kind>[a-z]+)(?: "(?P<title>[^"]*)")?[ \t]*$')
TAB_PATTERN = re.compile(r'^=== "(?P<label>[^"]+)"[ \t]*$')
FENCE_TITLE_PATTERN = re.compile(r'^(?P<lang>[\w+#.-]*)\s+title="(?P<title>[^"]+)"\s*$')
INDENT = "    "


def convert_for_wiki(text: str) -> str:
    return "\n".join(_convert(text.split("\n")))


def _convert(lines: list[str]) -> list[str]:
    converted: list[str] = []
    index = 0
    while index < len(lines):
        line = lines[index]
        fence = opening_fence(line)
        if fence:
            end = _fence_end(lines, index, fence["marker"])
            converted.extend(_convert_fence(lines[index:end + 1], fence))
            index = end + 1
            continue
        admonition = ADMONITION_PATTERN.match(line)
        if admonition:
            body, index = _indented_block(lines, index + 1)
            converted.extend(_render_admonition(admonition["kind"], admonition["title"], _convert(body)))
            continue
        tab = TAB_PATTERN.match(line)
        if tab:
            body, index = _indented_block(lines, index + 1)
            converted.extend([f"**{tab['label']}**", ""])
            converted.extend(_convert(body))
            continue
        converted.append(line)
        index += 1
    return converted


def _fence_end(lines: list[str], start: int, marker: str) -> int:
    for index in range(start + 1, len(lines)):
        if closes_fence(lines[index], marker):
            return index
    return len(lines) - 1


def _indented_block(lines: list[str], start: int) -> tuple[list[str], int]:
    body: list[str] = []
    index = start
    while index < len(lines):
        line = lines[index]
        if line.strip() == "":
            body.append("")
        elif line.startswith(INDENT):
            body.append(line[len(INDENT):])
        else:
            break
        index += 1
    trailing = 0
    while body and body[-1] == "":
        body.pop()
        trailing += 1
    return body, index - trailing


def _render_admonition(kind: str, title: str | None, body: list[str]) -> list[str]:
    if kind not in ADMONITIONS:
        raise DocsError(f"unsupported admonition type: {kind}")
    emoji, default_title = ADMONITIONS[kind]
    rendered = [f"> {emoji} **{title or default_title}**", ">"]
    rendered.extend(f"> {line}" if line else ">" for line in body)
    return rendered


def _convert_fence(block: list[str], fence: re.Match[str]) -> list[str]:
    titled = FENCE_TITLE_PATTERN.match(fence["info"].strip())
    if titled is None:
        return block
    indent = fence["indent"]
    return [
        f"{indent}**Fichier :** `{titled['title']}`",
        "",
        f"{indent}{fence['marker']}{titled['lang']}",
        *block[1:],
    ]
