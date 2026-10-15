from pathlib import Path

import pytest

from docstool.errors import DocsError
from docstool.includes import resolve_includes

SOURCE = """namespace Demo;

public static class Sample
{
    public static int First() => 1;

    public static int Second()
    {
        return 2;
    }
}
"""


@pytest.fixture()
def repo(tmp_path: Path) -> Path:
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "Sample.cs").write_text(SOURCE, encoding="utf-8")
    return tmp_path


def test_whole_file(repo: Path) -> None:
    text = '```csharp\n--8<-- "src/Sample.cs"\n```\n'

    resolved = resolve_includes(text, "page.md", repo)

    assert resolved == "```csharp\n" + SOURCE + "```\n"


def test_anchored_range_is_inclusive_and_dedented(repo: Path) -> None:
    text = '```csharp\n--8<-- "src/Sample.cs" start="Second()" end="}"\n```'

    resolved = resolve_includes(text, "page.md", repo)

    assert resolved == "```csharp\npublic static int Second()\n{\n    return 2;\n}\n```"


def test_start_only_runs_to_end_of_file(repo: Path) -> None:
    text = '--8<-- "src/Sample.cs" start="return 2;"'

    resolved = resolve_includes(text, "page.md", repo)

    assert resolved == "        return 2;\n    }\n}"


def test_end_on_start_line_gives_single_line(repo: Path) -> None:
    text = '--8<-- "src/Sample.cs" start="First()" end="=> 1;"'

    assert resolve_includes(text, "page.md", repo) == "public static int First() => 1;"


def test_directive_indentation_is_applied_to_every_line(repo: Path) -> None:
    text = '    --8<-- "src/Sample.cs" start="Second()" end="}"'

    resolved = resolve_includes(text, "page.md", repo)

    assert resolved == "    public static int Second()\n    {\n        return 2;\n    }"


def test_missing_file_reports_page_and_line(repo: Path) -> None:
    with pytest.raises(DocsError, match=r"page\.md:2: included file not found: src/Missing\.cs"):
        resolve_includes('```\n--8<-- "src/Missing.cs"\n```', "page.md", repo)


def test_missing_start_anchor(repo: Path) -> None:
    with pytest.raises(DocsError, match='start anchor "Third" not found in src/Sample.cs'):
        resolve_includes('--8<-- "src/Sample.cs" start="Third"', "page.md", repo)


def test_ambiguous_start_anchor_lists_lines(repo: Path) -> None:
    with pytest.raises(DocsError, match=r'start anchor "public static int" is ambiguous in src/Sample\.cs \(lines 5, 7\)'):
        resolve_includes('--8<-- "src/Sample.cs" start="public static int"', "page.md", repo)


def test_missing_end_anchor(repo: Path) -> None:
    with pytest.raises(DocsError, match='end anchor "never" not found after line 7'):
        resolve_includes('--8<-- "src/Sample.cs" start="Second()" end="never"', "page.md", repo)


def test_invalid_directive(repo: Path) -> None:
    with pytest.raises(DocsError, match="page.md:1: invalid include directive"):
        resolve_includes("--8<-- src/Sample.cs", "page.md", repo)


def test_bom_and_crlf_are_normalized(tmp_path: Path) -> None:
    (tmp_path / "f.json").write_bytes("﻿{\r\n  \"a\": 1\r\n}\r\n".encode("utf-8"))

    assert resolve_includes('--8<-- "f.json"', "p.md", tmp_path) == '{\n  "a": 1\n}'


def test_single_quoted_anchor_may_contain_double_quotes(tmp_path: Path) -> None:
    (tmp_path / "data.json").write_text('{\n  "a": 1,\n  "b": {\n    "c": 2\n  }\n}\n', encoding="utf-8")
    text = "--8<-- \"data.json\" start='\"b\": {' end='  }'"

    assert resolve_includes(text, "page.md", tmp_path) == '"b": {\n  "c": 2\n}'


def test_before_is_exclusive(repo: Path) -> None:
    text = '--8<-- "src/Sample.cs" start="First()" before="Second()"'

    assert resolve_includes(text, "page.md", repo) == "public static int First() => 1;\n"


def test_before_without_start_starts_at_file_beginning(repo: Path) -> None:
    text = '--8<-- "src/Sample.cs" before="public static class"'

    assert resolve_includes(text, "page.md", repo) == "namespace Demo;\n"


def test_missing_before_anchor(repo: Path) -> None:
    with pytest.raises(DocsError, match='before anchor "never" not found'):
        resolve_includes('--8<-- "src/Sample.cs" start="Second()" before="never"', "page.md", repo)


def test_end_and_before_together_is_invalid(repo: Path) -> None:
    with pytest.raises(DocsError, match="invalid include directive"):
        resolve_includes('--8<-- "src/Sample.cs" start="First()" end="}" before="Second()"', "page.md", repo)


def test_text_fence_keeps_directives_literal(repo: Path) -> None:
    text = '```text\n--8<-- "src/Sample.cs" start="First()"\n--8<-- invalid\n```\n\n```csharp\n--8<-- "src/Sample.cs" start="First()" end=";"\n```'

    resolved = resolve_includes(text, "page.md", repo)

    assert resolved.startswith('```text\n--8<-- "src/Sample.cs" start="First()"\n--8<-- invalid\n```\n')
    assert "public static int First() => 1;" in resolved
