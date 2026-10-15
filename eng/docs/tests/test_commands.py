import sys
from pathlib import Path

import pytest

from docstool.commands import Paths, check, generate_wiki, resolve

CONFIG = "site_name: Demo\ndocs_dir: build/docs-src\nnav:\n  - Accueil: index.md\n  - Guide: guide.md\n"


@pytest.fixture()
def paths(tmp_path: Path) -> Paths:
    (tmp_path / "mkdocs.yml").write_text(CONFIG, encoding="utf-8")
    site = tmp_path / "docs" / "site"
    site.mkdir(parents=True)
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "A.cs").write_text("var a = 1;\n", encoding="utf-8")
    (site / "index.md").write_text("# Accueil\n\n[Guide](guide.md)\n", encoding="utf-8")
    (site / "guide.md").write_text('# Guide\n\n```csharp\n--8<-- "src/A.cs"\n```\n', encoding="utf-8")
    return Paths(tmp_path)


def test_resolve_writes_included_code(paths: Paths) -> None:
    resolve(paths)

    assert (paths.resolved_dir / "guide.md").read_text(encoding="utf-8") == "# Guide\n\n```csharp\nvar a = 1;\n```\n"


def test_check_passes_when_wiki_is_up_to_date(paths: Paths) -> None:
    generate_wiki(paths)

    assert check(paths) == []


def test_check_ignores_crlf_line_endings(paths: Paths) -> None:
    generate_wiki(paths)
    page = paths.wiki_dir / "Guide.md"
    page.write_bytes(page.read_bytes().replace(b"\n", b"\r\n"))

    assert check(paths) == []


def test_check_detects_stale_wiki_after_code_change(paths: Paths) -> None:
    generate_wiki(paths)
    (paths.repo_root / "src" / "A.cs").write_text("var a = 2;\n", encoding="utf-8")

    assert check(paths) == ["docs/wiki is out of date (Guide.md): run python eng/docs/docs.py wiki"]


def test_check_reports_orphan_and_missing_pages(paths: Paths) -> None:
    (paths.site_dir / "orphan.md").write_text("# O\n", encoding="utf-8")
    (paths.site_dir / "guide.md").unlink()

    assert check(paths) == [
        "mkdocs.yml: navigation page not found: guide.md",
        "docs/site/orphan.md: page is not in the navigation",
    ]


def test_check_reports_broken_include(paths: Paths) -> None:
    (paths.repo_root / "src" / "A.cs").unlink()
    generate_wiki_errors = check(paths)

    assert generate_wiki_errors == ["guide.md:4: included file not found: src/A.cs"]


def run_main(monkeypatch: pytest.MonkeyPatch, error: Exception) -> int:
    import docs

    def fail(paths: Paths) -> None:
        raise error

    monkeypatch.setattr(docs, "build", fail)
    monkeypatch.setattr(sys, "argv", ["docs.py", "build"])
    return docs.main()


def test_main_reports_missing_zensical(monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]) -> None:
    assert run_main(monkeypatch, FileNotFoundError("zensical")) == 1
    assert "pip install -r eng/docs/requirements.txt" in capsys.readouterr().err


def test_main_reports_failed_command(monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]) -> None:
    import subprocess

    assert run_main(monkeypatch, subprocess.CalledProcessError(2, ["zensical", "build"])) == 1
    assert "exit code 2" in capsys.readouterr().err
