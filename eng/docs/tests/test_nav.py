from pathlib import Path

import pytest

from docstool.errors import DocsError
from docstool.nav import Page, load_navigation, wiki_name

CONFIG = """site_name: Demo
markdown_extensions:
  - pymdownx.superfences:
      custom_fences:
        - name: mermaid
          class: mermaid
          format: !!python/name:pymdownx.superfences.fence_code_format
nav:
  - Accueil: index.md
  - Démarrer:
      - demarrer/index.md
      - Créer un projet: demarrer/creer-un-projet.md
  - Tests:
      - tests/index.md
"""


def write_config(tmp_path: Path, content: str) -> Path:
    path = tmp_path / "mkdocs.yml"
    path.write_text(content, encoding="utf-8")
    return path


def test_wiki_name() -> None:
    assert wiki_name("Créer un projet") == "Créer-un-projet"
    assert wiki_name("Qu'est ce : un *test* ?") == "Qu'est-ce-un-test"
    assert wiki_name("Pré-requis") == "Pré%2Drequis"


def test_load_navigation(tmp_path: Path) -> None:
    navigation = load_navigation(write_config(tmp_path, CONFIG))

    assert navigation.pages == [
        Page("index.md", "Accueil", "Accueil.md"),
        Page("demarrer/index.md", "Démarrer", "Démarrer.md"),
        Page("demarrer/creer-un-projet.md", "Créer un projet", "Démarrer/Créer-un-projet.md"),
        Page("tests/index.md", "Tests", "Tests.md"),
    ]
    assert navigation.orders == {"": ["Accueil", "Démarrer", "Tests"], "Démarrer": ["Créer-un-projet"]}


def test_untitled_entry_is_rejected(tmp_path: Path) -> None:
    with pytest.raises(DocsError, match="every navigation entry needs a title"):
        load_navigation(write_config(tmp_path, "nav:\n  - index.md\n"))


def test_section_must_start_with_index(tmp_path: Path) -> None:
    with pytest.raises(DocsError, match='section "Guides" must start with its index.md page'):
        load_navigation(write_config(tmp_path, "nav:\n  - Guides:\n      - Un: guides/un.md\n"))


def test_empty_wiki_name_is_rejected(tmp_path: Path) -> None:
    with pytest.raises(DocsError, match='navigation title "\?" gives an empty wiki page name'):
        load_navigation(write_config(tmp_path, 'nav:\n  - "?": index.md\n'))


def test_duplicate_wiki_name_is_rejected(tmp_path: Path) -> None:
    config = "nav:\n  - Un test: index.md\n  - Un  test: autre.md\n"

    with pytest.raises(DocsError, match='duplicate wiki page name "Un-test" in folder "\(root\)"'):
        load_navigation(write_config(tmp_path, config))
