"""Authoring rules: only syntax that both Zensical and the Azure DevOps wiki can render."""

import re

from docstool.convert import ADMONITIONS
from docstool.fences import closes_fence, opening_fence
from docstool.includes import INCLUDE_PATTERN, is_include, is_literal_fence

FENCE_INFO_PATTERN = re.compile(r'^[\w+#.-]*(\s+title="[^"]+")?$')
ADMONITION_START = re.compile(r"^\s*(?:!!!|\?\?\?\+?)\s+(?P<kind>\S+)")
RULES = [
    (re.compile(r":(?:material|fontawesome|octicons|simple)-[a-z0-9-]+:"), "icons are not supported by the Azure DevOps wiki"),
    (re.compile(r"\{\s*[.#:][^}]*\}\s*$"), "attribute lists are not supported by the Azure DevOps wiki"),
    (re.compile(r"^\s*///"), "block syntax (///) is not supported by the Azure DevOps wiki"),
    (re.compile(r"^\s*<div"), "raw HTML blocks are not allowed"),
]


def check_page(text: str, page: str) -> list[str]:
    errors: list[str] = []
    marker: str | None = None
    fence_line = 0
    literal = False
    for number, line in enumerate(text.split("\n"), start=1):
        where = f"{page}:{number}"
        if marker is not None:
            if closes_fence(line, marker):
                marker = None
            elif not literal and is_include(line) and INCLUDE_PATTERN.match(line) is None:
                errors.append(f"{where}: invalid include directive")
            continue
        fence = opening_fence(line)
        if fence:
            marker = fence["marker"]
            fence_line = number
            literal = is_literal_fence(fence["info"])
            if FENCE_INFO_PATTERN.match(fence["info"].strip()) is None:
                errors.append(f'{where}: code blocks only accept a language and title="..."')
            continue
        if is_include(line):
            errors.append(f"{where}: includes must be inside a code block")
            continue
        admonition = ADMONITION_START.match(line)
        if admonition and admonition["kind"] not in ADMONITIONS:
            errors.append(f"{where}: unsupported admonition type: {admonition['kind']}")
        for pattern, message in RULES:
            if pattern.search(line):
                errors.append(f"{where}: {message}")
    if marker is not None:
        errors.append(f"{page}:{fence_line}: code block is never closed")
    return errors
