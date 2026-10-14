from pathlib import Path

import pytest

from docstool.errors import DocsError
from docstool.nav import Navigation, Page
from docstool.wiki import GENERATED_NOTICE, build_wiki, rewrite_links

HOME = Page("index.md", "Accueil", "Accueil.md")
CREATE = Page("demarrer/creer-un-projet.md", "Créer un projet", "Démarrer/Créer-un-projet.md")
START = Page("demarrer/index.md", "Démarrer", "Démarrer.md")
PAGES = {page.source: page for page in (HOME, CREATE, START)}


def test_relative_page_link_becomes_absolute_wiki_path() -> None:
    text = "Voir [la création](creer-un-projet.md#options) et [l'accueil](../index.md)."

    assert rewrite_links(text, START, PAGES) == (
        "Voir [la création](/Démarrer/Créer-un-projet.md#options) et [l'accueil](/Accueil.md)."
    )


def test_external_and_anchor_links_are_untouched() -> None:
    text = "[site](https://example.com) [ici](#section) [mail](mailto:a@b.c)"

    assert rewrite_links(text, HOME, PAGES) == text


def test_image_goes_to_attachments() -> None:
    assert rewrite_links("![schéma](../assets/flow.png)", CREATE, PAGES) == "![schéma](/.attachments/flow.png)"


def test_links_inside_code_blocks_are_untouched() -> None:
    text = "```markdown\n[x](nope.md)\n```"

    assert rewrite_links(text, HOME, PAGES) == text


def test_link_to_page_outside_navigation_fails() -> None:
    with pytest.raises(DocsError, match=r"index\.md: link to a page that is not in the navigation: nope\.md"):
        rewrite_links("[x](nope.md)", HOME, PAGES)


def test_build_wiki(tmp_path: Path) -> None:
    site = tmp_path / "docs" / "site"
    (site / "demarrer").mkdir(parents=True)
    (site / "assets").mkdir()
    (site / "assets" / "flow.png").write_bytes(b"png")
    (tmp_path / "src").mkdir()
    (tmp_path / "src" / "A.cs").write_text("var a = 1;\n", encoding="utf-8")
    (site / "index.md").write_text('# Accueil\n\n[Démarrer](demarrer/index.md)\n', encoding="utf-8")
    (site / "demarrer" / "index.md").write_text(
        '# Démarrer\n\n!!! tip\n    Astuce.\n\n```csharp title="src/A.cs"\n--8<-- "src/A.cs"\n```\n',
        encoding="utf-8",
    )
    (site / "demarrer" / "creer-un-projet.md").write_text("![x](../assets/flow.png)\n", encoding="utf-8")
    navigation = Navigation(
        pages=[HOME, START, CREATE],
        orders={"": ["Accueil", "Démarrer"], "Démarrer": ["Créer-un-projet"]},
    )
    output = tmp_path / "docs" / "wiki"
    output.mkdir(parents=True)
    (output / "stale.md").write_text("old", encoding="utf-8")

    build_wiki(site, tmp_path, navigation, output)

    assert not (output / "stale.md").exists()
    assert (output / "Accueil.md").read_text(encoding="utf-8") == "# Accueil\n\n[Démarrer](/Démarrer.md)\n"
    assert (output / "Démarrer.md").read_text(encoding="utf-8") == (
        "# Démarrer\n\n> 💡 **Astuce**\n>\n> Astuce.\n\n**Fichier :** `src/A.cs`\n\n```csharp\nvar a = 1;\n```\n"
    )
    assert (output / "Démarrer" / "Créer-un-projet.md").read_text(encoding="utf-8") == "![x](/.attachments/flow.png)\n"
    assert (output / ".order").read_text(encoding="utf-8") == "Accueil\nDémarrer\n"
    assert (output / "Démarrer" / ".order").read_text(encoding="utf-8") == "Créer-un-projet\n"
    assert (output / ".attachments" / "flow.png").read_bytes() == b"png"
    assert (output / "GENERATED.txt").read_text(encoding="utf-8") == GENERATED_NOTICE


def test_links_inside_code_block_of_an_admonition_are_untouched(tmp_path: Path) -> None:
    site = tmp_path / "docs" / "site"
    site.mkdir(parents=True)
    (site / "index.md").write_text("!!! note\n    ```markdown\n    [x](nope.md)\n    ```\n", encoding="utf-8")
    output = tmp_path / "docs" / "wiki"

    build_wiki(site, tmp_path, Navigation(pages=[HOME], orders={"": ["Accueil"]}), output)

    assert (output / "Accueil.md").read_text(encoding="utf-8") == (
        "> 📝 **Note**\n>\n> ```markdown\n> [x](nope.md)\n> ```\n"
    )
