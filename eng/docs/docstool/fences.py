import re

FENCE_PATTERN = re.compile(r"^(?P<indent>[ \t]*)(?P<marker>`{3,}|~{3,})(?P<info>.*)$")


def opening_fence(line: str) -> re.Match[str] | None:
    return FENCE_PATTERN.match(line)


def closes_fence(line: str, marker: str) -> bool:
    stripped = line.strip()
    return len(stripped) >= len(marker) and set(stripped) == {marker[0]}
