# Documentation (GitHub Pages + wiki de code Azure DevOps) — Plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publier une documentation complète en français, à la fois guide et cours pour novices, depuis une source Markdown unique vers GitHub Pages (Zensical) et un wiki de code Azure DevOps, avec des extraits de code lus dans les vrais fichiers du dépôt.

**Architecture:** `docs/site/` est la source. Le script `eng/docs/docs.py` (paquet `eng/docs/docstool/`) résout les inclusions de code ancrées sur du texte, produit `build/docs-src/` pour Zensical et `docs/wiki/` (commité) pour Azure DevOps en appliquant des règles de conversion fixes, et vérifie le tout (`check`) en CI GitHub et Azure DevOps.

**Tech Stack:** Python 3.12+ (PyYAML 6.0.3, pytest 9.1.1), Zensical 0.0.70 (lit `mkdocs.yml`), GitHub Actions + GitHub Pages, Azure Pipelines, wiki de code Azure DevOps, modèle `dotnet new`.

**Spec:** `docs/superpowers/specs/2026-10-14-documentation-design.md`

## Global Constraints

- Branche : `feat/documentation` (créée depuis `main` après fusion du sous-projet 1). Commandes lancées depuis la racine du dépôt, Git Bash. Commits terminés par la ligne `Co-Authored-By:` du modèle qui écrit le commit.
- Python : `python` (3.12+). Dépendances : `pip install -r eng/docs/requirements.txt` (`zensical==0.0.70`, `PyYAML==6.0.3`, `pytest==9.1.1`). Tests : `python -m pytest eng/docs/tests -q`.
- Source : `docs/site/`. Wiki généré : `docs/wiki/` (commité, jamais modifié à la main). Résolu pour le site : `build/docs-src/` (ignoré). Site : `site/` (ignoré). `docs/superpowers/` n'est jamais publié.
- Inclusions : `--8<-- "chemin/depuis/la/racine"` éventuellement suivi de ` start="…"` puis ` end="…"`, seules sur leur ligne, **dans un bloc de code**. `start` doit apparaître une seule fois dans le fichier ; `end` est cherché à partir de la ligne de `start` incluse ; bornes incluses ; indentation commune retirée.
- Encadrés autorisés : `note`, `info`, `tip`, `warning`, `danger`, `example`, `question` (`!!!` ou `???`). Onglets `=== "Libellé"`. Corps indentés de 4 espaces. Encadrés et onglets uniquement au niveau racine ou à l'intérieur d'un autre encadré/onglet.
- Blocs de code : seul attribut autorisé `title="…"`. Mermaid via ` ```mermaid `.
- Interdits (refusés par `check`) : icônes `:material-…:` etc., listes d'attributs `{ .x }`, blocs `///`, HTML brut `<div`, annotations de code.
- Navigation (`mkdocs.yml`) : chaque entrée a un titre ; chaque section commence par son `index.md` sans titre ; **aucun tiret dans les titres** (le wiki Azure DevOps encode `-` en `%2D`).
- Rédaction : français, vouvoiement, public novice. Chaque page de concept suit les sections `## Le concept simplement`, `## Pourquoi c'est important`, `## Comment le template le fait`, `## À essayer` (puis `## Pour aller plus loin` facultatif avec liens officiels). Noms de code en anglais. Exemples de génération avec `Contoso.Orders`.
- Le code du dépôt est la **source du template** : il contient des directives `#if (EnableAspire)`, `#if (UseGroups)`… La doc l'explique dès qu'un extrait en montre (encadré `info` : « dans un projet généré, seules les branches correspondant à vos options sont conservées »).
- Faits d'environnement vérifiés : Keycloak local en `https://localhost:8080` (certificat de développement, `dotnet dev-certs https --trust`), API en `https://localhost:7246` (le port HTTP 5257 redirige et perd l'en-tête `Authorization`), realm `template`, clients `template-api` / `template-scalar` / `template-cli`, personas `admin@local.test` et `user@local.test` (`Passw0rd!`), GUID de groupe `6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11`, volumes `<apphost-en-minuscules>-<hash>-keycloak-data`, realm importé uniquement au premier démarrage.
- Après toute modification de `docs/site/` ou du code inclus : `python eng/docs/docs.py wiki` puis `python eng/docs/docs.py check` doit afficher `docs check: OK`.

## Review Focus

- Code inclus dont une ancre apparaît deux fois dans le fichier : `check` doit refuser avec les numéros de ligne (test en Task 1).
- Encadré contenant un bloc de code avec des lignes vides : le wiki doit garder tout le bloc dans la citation (test en Task 2).
- Fichiers de `docs/wiki/` convertis en CRLF par git sous Windows : `check` ne doit pas signaler de faux écart (`docs/.gitattributes` + comparaison normalisée, test en Task 3).
- Lien interne vers une page absente de la navigation : la génération du wiki échoue avec la page et le lien (test en Task 2).
- Projet généré : il ne doit contenir ni `mkdocs.yml`, ni le README du dépôt template, mais un `README.md` propre au projet (vérifié par `eng/verify-template.sh` en Task 4).

---

## File Structure

```
eng/docs/docs.py                  CLI : resolve | wiki | build | serve | check
eng/docs/requirements.txt         versions épinglées
eng/docs/docstool/__init__.py
eng/docs/docstool/errors.py       DocsError
eng/docs/docstool/fences.py       détection des blocs de code
eng/docs/docstool/includes.py     inclusions ancrées
eng/docs/docstool/convert.py      encadrés, onglets, titres de blocs → wiki
eng/docs/docstool/nav.py          navigation mkdocs.yml → pages, noms wiki, .order
eng/docs/docstool/wiki.py         liens, images, génération de docs/wiki
eng/docs/docstool/style.py        guide de style
eng/docs/docstool/commands.py     resolve / generate_wiki / build / serve / check
eng/docs/tests/conftest.py, test_*.py
mkdocs.yml                        configuration du site + navigation
docs/.gitattributes               wiki en LF
docs/site/**                      source de la doc (≈ 40 pages)
docs/wiki/**                      wiki généré (commité)
.github/workflows/docs.yml        check + déploiement Pages
eng/azure-pipelines/docs-ci.yml   check Azure DevOps
README.md                         accueil du dépôt template (exclu de la génération)
README.generated.md               README des projets générés (renommé en README.md)
.template.config/template.json    exclusions/renommage
eng/verify-template.sh            vérifications README / mkdocs.yml
.gitignore                        build/ site/
```

---

### Task 1: Socle Python et inclusions de code ancrées

**Files:**
- Create: `eng/docs/requirements.txt`, `eng/docs/docstool/__init__.py`, `eng/docs/docstool/errors.py`, `eng/docs/docstool/fences.py`, `eng/docs/docstool/includes.py`
- Create: `eng/docs/tests/conftest.py`, `eng/docs/tests/test_includes.py`
- Modify: `.gitignore`

**Interfaces:**
- Produces:
  - `docstool.errors.DocsError(Exception)`.
  - `docstool.fences.opening_fence(line: str) -> re.Match | None` (groupes `indent`, `marker`, `info`) ; `docstool.fences.closes_fence(line: str, marker: str) -> bool`.
  - `docstool.includes.INCLUDE_PATTERN`, `is_include(line: str) -> bool`, `resolve_includes(text: str, page: str, repo_root: Path) -> str`, `read_snippet(repo_root: Path, relative_path: str, start: str | None, end: str | None, where: str) -> list[str]`.

- [ ] **Step 1: Préparer l'environnement**

`eng/docs/requirements.txt` :

```
zensical==0.0.70
PyYAML==6.0.3
pytest==9.1.1
```

Ajouter à la fin de `.gitignore` :

```
# Documentation build outputs
/build/
/site/
```

Run: `python -m pip install -r eng/docs/requirements.txt`
Expected: installation réussie (`zensical --help` affiche les commandes `build`, `new`, `serve`).

- [ ] **Step 2: Écrire les tests en échec**

`eng/docs/tests/conftest.py` :

```python
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
```

`eng/docs/tests/test_includes.py` :

```python
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

    assert resolved == "return 2;\n}\n}"


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
```

Run: `python -m pytest eng/docs/tests -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'docstool'`.

- [ ] **Step 3: Implémenter**

`eng/docs/docstool/__init__.py` :

```python
"""Documentation tooling for the template: code includes, Azure DevOps wiki generation and checks."""
```

`eng/docs/docstool/errors.py` :

```python
class DocsError(Exception):
    """A problem in the documentation sources, reported with its location."""
```

`eng/docs/docstool/fences.py` :

```python
import re

FENCE_PATTERN = re.compile(r"^(?P<indent>[ \t]*)(?P<marker>`{3,}|~{3,})(?P<info>.*)$")


def opening_fence(line: str) -> re.Match[str] | None:
    return FENCE_PATTERN.match(line)


def closes_fence(line: str, marker: str) -> bool:
    stripped = line.strip()
    return len(stripped) >= len(marker) and set(stripped) == {marker[0]}
```

`eng/docs/docstool/includes.py` :

```python
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
```

- [ ] **Step 4: Vérifier**

Run: `python -m pytest eng/docs/tests -q`
Expected: `11 passed`.

- [ ] **Step 5: Commit**

```bash
git add .gitignore eng/docs
git commit -m "feat(docs): add text-anchored code includes for the documentation tooling"
```

---

### Task 2: Navigation, conversion et génération du wiki Azure DevOps

**Files:**
- Create: `eng/docs/docstool/convert.py`, `eng/docs/docstool/nav.py`, `eng/docs/docstool/wiki.py`
- Test: `eng/docs/tests/test_convert.py`, `eng/docs/tests/test_nav.py`, `eng/docs/tests/test_wiki.py`

**Interfaces:**
- Consumes: `DocsError`, `opening_fence`, `closes_fence`, `resolve_includes` (Task 1).
- Produces:
  - `docstool.convert.ADMONITIONS: dict[str, tuple[str, str]]` (type → (emoji, titre français)), `convert_for_wiki(text: str) -> str`.
  - `docstool.nav.Page(source: str, title: str, wiki_path: str)`, `docstool.nav.Navigation(pages: list[Page], orders: dict[str, list[str]])`, `wiki_name(title: str) -> str`, `load_navigation(config_path: Path) -> Navigation`.
  - `docstool.wiki.rewrite_links(text: str, page: Page, pages_by_source: dict[str, Page]) -> str`, `build_wiki(site_dir: Path, repo_root: Path, navigation: Navigation, output_dir: Path) -> None`, `GENERATED_NOTICE: str`.

- [ ] **Step 1: Écrire les tests en échec**

`eng/docs/tests/test_convert.py` :

```python
from docstool.convert import convert_for_wiki


def test_admonition_with_title() -> None:
    text = '!!! tip "Bon à savoir"\n    Première ligne.\n\n    Seconde.\n\nAprès.'

    assert convert_for_wiki(text) == "> 💡 **Bon à savoir**\n>\n> Première ligne.\n>\n> Seconde.\n\nAprès."


def test_admonition_without_title_uses_french_name() -> None:
    assert convert_for_wiki("!!! warning\n    Attention.") == "> ⚠️ **Attention**\n>\n> Attention."


def test_collapsible_admonition() -> None:
    assert convert_for_wiki('??? example "Détail"\n    Corps.') == "> 🧪 **Détail**\n>\n> Corps."


def test_admonition_keeps_code_block_with_blank_lines() -> None:
    text = '!!! note\n    ```csharp\n    var a = 1;\n\n    var b = 2;\n    ```'

    assert convert_for_wiki(text) == "> 📝 **Note**\n>\n> ```csharp\n> var a = 1;\n>\n> var b = 2;\n> ```"


def test_tabs_become_bold_labels() -> None:
    text = '=== "PowerShell"\n    ```powershell\n    dir\n    ```\n\n=== "Bash"\n    ls'

    assert convert_for_wiki(text) == "**PowerShell**\n\n```powershell\ndir\n```\n\n**Bash**\n\nls"


def test_code_block_title_becomes_file_line() -> None:
    text = '```csharp title="src/A.cs"\nvar a = 1;\n```'

    assert convert_for_wiki(text) == "**Fichier :** `src/A.cs`\n\n```csharp\nvar a = 1;\n```"


def test_admonition_syntax_inside_code_block_is_untouched() -> None:
    text = '```markdown\n!!! tip "x"\n    y\n```'

    assert convert_for_wiki(text) == text


def test_mermaid_is_untouched() -> None:
    text = "```mermaid\ngraph LR\n  A --> B\n```"

    assert convert_for_wiki(text) == text
```

`eng/docs/tests/test_nav.py` :

```python
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
```

`eng/docs/tests/test_wiki.py` :

```python
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
```

Run: `python -m pytest eng/docs/tests -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'docstool.convert'` (et `nav`, `wiki`).

- [ ] **Step 2: Implémenter `convert.py`**

```python
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
```

- [ ] **Step 3: Implémenter `nav.py`**

```python
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
        orders.setdefault(folder, []).append(name)
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
```

- [ ] **Step 4: Implémenter `wiki.py`**

```python
"""Generation of the Azure DevOps code wiki (docs/wiki) from the documentation sources."""

import posixpath
import re
import shutil
from pathlib import Path

from docstool.convert import convert_for_wiki
from docstool.errors import DocsError
from docstool.fences import closes_fence, opening_fence
from docstool.includes import resolve_includes
from docstool.nav import Navigation, Page

LINK_PATTERN = re.compile(r"(?P<bang>!?)\[(?P<text>[^\]]*)\]\((?P<target>[^)\s]+)\)")
UNCHANGED_TARGETS = ("http://", "https://", "mailto:", "#")
GENERATED_NOTICE = (
    "This folder is generated from docs/site by: python eng/docs/docs.py wiki\n"
    "Do not edit it by hand: edit docs/site, then regenerate.\n"
)


def rewrite_links(text: str, page: Page, pages_by_source: dict[str, Page]) -> str:
    rewritten: list[str] = []
    marker: str | None = None
    for line in text.split("\n"):
        if marker is not None:
            if closes_fence(line, marker):
                marker = None
            rewritten.append(line)
            continue
        fence = opening_fence(line)
        if fence:
            marker = fence["marker"]
            rewritten.append(line)
            continue
        rewritten.append(LINK_PATTERN.sub(lambda match: _rewrite(match, page, pages_by_source), line))
    return "\n".join(rewritten)


def _rewrite(match: re.Match[str], page: Page, pages_by_source: dict[str, Page]) -> str:
    target = match["target"]
    if target.startswith(UNCHANGED_TARGETS):
        return match[0]
    path, _, anchor = target.partition("#")
    resolved = posixpath.normpath(posixpath.join(posixpath.dirname(page.source), path))
    suffix = f"#{anchor}" if anchor else ""
    if match["bang"]:
        if not resolved.startswith("assets/"):
            raise DocsError(f"{page.source}: images must live under docs/site/assets: {target}")
        return f"![{match['text']}](/.attachments/{posixpath.basename(resolved)})"
    linked = pages_by_source.get(resolved)
    if linked is None:
        raise DocsError(f"{page.source}: link to a page that is not in the navigation: {target}")
    return f"[{match['text']}](/{linked.wiki_path}{suffix})"


def build_wiki(site_dir: Path, repo_root: Path, navigation: Navigation, output_dir: Path) -> None:
    if output_dir.exists():
        shutil.rmtree(output_dir)
    output_dir.mkdir(parents=True)

    pages_by_source = {page.source: page for page in navigation.pages}
    for page in navigation.pages:
        text = (site_dir / page.source).read_text(encoding="utf-8")
        text = resolve_includes(text, page.source, repo_root)
        text = convert_for_wiki(text)
        text = rewrite_links(text, page, pages_by_source)
        target = output_dir / page.wiki_path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(text, encoding="utf-8", newline="\n")

    for folder, names in navigation.orders.items():
        order_file = output_dir / folder / ".order"
        order_file.parent.mkdir(parents=True, exist_ok=True)
        order_file.write_text("\n".join(names) + "\n", encoding="utf-8", newline="\n")

    assets = site_dir / "assets"
    if assets.is_dir():
        attachments = output_dir / ".attachments"
        attachments.mkdir()
        seen: set[str] = set()
        for asset in sorted(path for path in assets.rglob("*") if path.is_file()):
            if asset.name in seen:
                raise DocsError(f"docs/site/assets: duplicate file name {asset.name}")
            seen.add(asset.name)
            shutil.copyfile(asset, attachments / asset.name)

    (output_dir / "GENERATED.txt").write_text(GENERATED_NOTICE, encoding="utf-8", newline="\n")
```

- [ ] **Step 5: Vérifier**

Run: `python -m pytest eng/docs/tests -q`
Expected: `29 passed` (11 de Task 1 + 18 nouveaux).

- [ ] **Step 6: Commit**

```bash
git add eng/docs
git commit -m "feat(docs): generate the Azure DevOps code wiki from the documentation sources"
```

---

### Task 3: Guide de style, commandes, configuration du site et squelette des pages

**Files:**
- Create: `eng/docs/docstool/style.py`, `eng/docs/docstool/commands.py`, `eng/docs/docs.py`
- Create: `mkdocs.yml`, `docs/.gitattributes`, `docs/site/**` (squelette : 46 pages listées ci-dessous)
- Create: `docs/wiki/**` (généré)
- Test: `eng/docs/tests/test_style.py`, `eng/docs/tests/test_commands.py`

**Interfaces:**
- Consumes: Tasks 1-2.
- Produces:
  - `docstool.style.check_page(text: str, page: str) -> list[str]`.
  - `docstool.commands.Paths(repo_root: Path)` (propriétés `site_dir`, `wiki_dir`, `resolved_dir`, `config`), `resolve(paths) -> None`, `generate_wiki(paths, output_dir: Path | None = None) -> None`, `check(paths) -> list[str]`, `build(paths) -> None`, `serve(paths) -> None`.
  - CLI `python eng/docs/docs.py {resolve|wiki|build|serve|check}` ; `check` affiche `docs check: OK` ou la liste des erreurs puis sort en code 1.

- [ ] **Step 1: Écrire les tests en échec**

`eng/docs/tests/test_style.py` :

```python
from docstool.style import check_page


def test_valid_page_has_no_errors() -> None:
    text = (
        "# Titre\n\n!!! tip \"Astuce\"\n    Corps.\n\n=== \"Bash\"\n    ls\n\n"
        "```csharp title=\"src/A.cs\"\n--8<-- \"src/A.cs\" start=\"a\" end=\"b\"\n```\n\n"
        "```mermaid\ngraph LR\n```\n"
    )

    assert check_page(text, "p.md") == []


def test_icons_attribute_lists_blocks_and_html_are_rejected() -> None:
    text = "Voir :material-check: ici\n[Bouton](x.md){ .md-button }\n/// details\n<div class=\"grid\">\n"

    errors = check_page(text, "p.md")

    assert errors == [
        "p.md:1: icons are not supported by the Azure DevOps wiki",
        "p.md:2: attribute lists are not supported by the Azure DevOps wiki",
        "p.md:3: block syntax (///) is not supported by the Azure DevOps wiki",
        "p.md:4: raw HTML blocks are not allowed",
    ]


def test_unknown_admonition_type_is_rejected() -> None:
    assert check_page("!!! abstract \"x\"\n    y\n", "p.md") == [
        "p.md:1: unsupported admonition type: abstract",
    ]


def test_code_block_attributes_other_than_title_are_rejected() -> None:
    assert check_page("```csharp hl_lines=\"2\"\nx\n```\n", "p.md") == [
        "p.md:1: code blocks only accept a language and title=\"...\"",
    ]


def test_include_outside_code_block_and_invalid_include() -> None:
    text = "--8<-- \"src/A.cs\"\n```\n--8<-- src/A.cs\n```\n"

    assert check_page(text, "p.md") == [
        "p.md:1: includes must be inside a code block",
        "p.md:3: invalid include directive",
    ]


def test_forbidden_syntax_inside_code_block_is_allowed() -> None:
    assert check_page("```markdown\n:material-x: { .y }\n!!! abstract\n```\n", "p.md") == []


def test_unclosed_code_block() -> None:
    assert check_page("```csharp\nx\n", "p.md") == ["p.md:1: code block is never closed"]
```

`eng/docs/tests/test_commands.py` :

```python
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
```

Run: `python -m pytest eng/docs/tests -q`
Expected: FAIL — `ModuleNotFoundError: No module named 'docstool.style'` / `'docstool.commands'`.

- [ ] **Step 2: Implémenter `style.py`**

```python
"""Authoring rules: only syntax that both Zensical and the Azure DevOps wiki can render."""

import re

from docstool.convert import ADMONITIONS
from docstool.fences import closes_fence, opening_fence
from docstool.includes import INCLUDE_PATTERN, is_include

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
    for number, line in enumerate(text.split("\n"), start=1):
        where = f"{page}:{number}"
        if marker is not None:
            if closes_fence(line, marker):
                marker = None
            elif is_include(line) and INCLUDE_PATTERN.match(line) is None:
                errors.append(f"{where}: invalid include directive")
            continue
        fence = opening_fence(line)
        if fence:
            marker = fence["marker"]
            fence_line = number
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
```

- [ ] **Step 3: Implémenter `commands.py` et `docs.py`**

`eng/docs/docstool/commands.py` :

```python
"""Documentation commands: resolve, wiki, build, serve and check."""

import shutil
import subprocess
import tempfile
from dataclasses import dataclass
from pathlib import Path

from docstool.errors import DocsError
from docstool.includes import resolve_includes
from docstool.nav import load_navigation
from docstool.style import check_page
from docstool.wiki import build_wiki


@dataclass(frozen=True)
class Paths:
    repo_root: Path

    @property
    def site_dir(self) -> Path:
        return self.repo_root / "docs" / "site"

    @property
    def wiki_dir(self) -> Path:
        return self.repo_root / "docs" / "wiki"

    @property
    def resolved_dir(self) -> Path:
        return self.repo_root / "build" / "docs-src"

    @property
    def config(self) -> Path:
        return self.repo_root / "mkdocs.yml"


def resolve(paths: Paths) -> None:
    if paths.resolved_dir.exists():
        shutil.rmtree(paths.resolved_dir)
    shutil.copytree(paths.site_dir, paths.resolved_dir)
    for page in sorted(paths.resolved_dir.rglob("*.md")):
        source = page.relative_to(paths.resolved_dir).as_posix()
        text = resolve_includes(page.read_text(encoding="utf-8"), source, paths.repo_root)
        page.write_text(text, encoding="utf-8", newline="\n")


def generate_wiki(paths: Paths, output_dir: Path | None = None) -> None:
    build_wiki(paths.site_dir, paths.repo_root, load_navigation(paths.config), output_dir or paths.wiki_dir)


def build(paths: Paths) -> None:
    resolve(paths)
    generate_wiki(paths)
    subprocess.run(["zensical", "build", "--strict", "-f", str(paths.config)], cwd=paths.repo_root, check=True)


def serve(paths: Paths) -> None:
    resolve(paths)
    subprocess.run(["zensical", "serve", "-f", str(paths.config)], cwd=paths.repo_root, check=True)


def check(paths: Paths) -> list[str]:
    try:
        navigation = load_navigation(paths.config)
    except DocsError as error:
        return [str(error)]

    errors: list[str] = []
    in_navigation = {page.source for page in navigation.pages}
    on_disk = {page.relative_to(paths.site_dir).as_posix() for page in paths.site_dir.rglob("*.md")}
    errors.extend(f"mkdocs.yml: navigation page not found: {source}" for source in sorted(in_navigation - on_disk))
    errors.extend(f"docs/site/{source}: page is not in the navigation" for source in sorted(on_disk - in_navigation))
    for source in sorted(on_disk):
        text = (paths.site_dir / source).read_text(encoding="utf-8")
        errors.extend(check_page(text, source))
        try:
            resolve_includes(text, source, paths.repo_root)
        except DocsError as error:
            errors.append(str(error))
    if errors:
        return errors

    with tempfile.TemporaryDirectory() as temporary:
        expected = Path(temporary) / "wiki"
        try:
            generate_wiki(paths, expected)
        except DocsError as error:
            return [str(error)]
        return _compare_trees(expected, paths.wiki_dir)


def _compare_trees(expected: Path, actual: Path) -> list[str]:
    if not actual.is_dir():
        return ["docs/wiki is missing: run python eng/docs/docs.py wiki"]
    expected_files = _files(expected)
    actual_files = _files(actual)
    stale = sorted(expected_files ^ actual_files)
    stale += sorted(
        name for name in expected_files & actual_files
        if _normalized(expected / name) != _normalized(actual / name)
    )
    return [f"docs/wiki is out of date ({name}): run python eng/docs/docs.py wiki" for name in stale]


def _files(root: Path) -> set[str]:
    return {path.relative_to(root).as_posix() for path in root.rglob("*") if path.is_file()}


def _normalized(path: Path) -> bytes:
    return path.read_bytes().replace(b"\r\n", b"\n")
```

`eng/docs/docs.py` :

```python
"""Documentation tooling for the template.

Usage: python eng/docs/docs.py {resolve|wiki|build|serve|check}
  resolve  copy docs/site to build/docs-src with code includes resolved
  wiki     regenerate the Azure DevOps code wiki in docs/wiki
  build    resolve + wiki + Zensical site in site/
  serve    resolve + Zensical preview server (re-run after editing docs/site)
  check    authoring rules, includes, navigation and docs/wiki freshness (no writes)
"""

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from docstool.commands import Paths, build, check, generate_wiki, resolve, serve  # noqa: E402
from docstool.errors import DocsError  # noqa: E402


def main() -> int:
    parser = argparse.ArgumentParser(description="Documentation tooling for the template.")
    parser.add_argument("command", choices=["resolve", "wiki", "build", "serve", "check"])
    command = parser.parse_args().command
    paths = Paths(Path(__file__).resolve().parents[2])
    try:
        if command == "check":
            errors = check(paths)
            for error in errors:
                print(error, file=sys.stderr)
            if errors:
                return 1
            print("docs check: OK")
            return 0
        {"resolve": resolve, "wiki": generate_wiki, "build": build, "serve": serve}[command](paths)
    except DocsError as error:
        print(error, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

Run: `python -m pytest eng/docs/tests -q`
Expected: tous les tests PASS.

- [ ] **Step 4: Configuration du site**

`mkdocs.yml` :

```yaml
site_name: Template Web CQRS
site_description: Guide et cours du template ASP.NET Core CQRS (.NET 10, Aspire, Entra ID, Keycloak)
site_url: https://floriandrevet.github.io/template-CQRS/
repo_url: https://github.com/FlorianDrevet/template-CQRS
edit_uri: edit/main/docs/site/
docs_dir: build/docs-src
site_dir: site

theme:
  name: material
  language: fr
  features:
    - navigation.sections
    - navigation.indexes
    - navigation.top
    - navigation.footer
    - search.highlight
    - content.code.copy
    - content.action.edit
  palette:
    - media: "(prefers-color-scheme: light)"
      scheme: default
      toggle:
        icon: material/brightness-7
        name: Passer en mode sombre
    - media: "(prefers-color-scheme: dark)"
      scheme: slate
      toggle:
        icon: material/brightness-4
        name: Passer en mode clair

markdown_extensions:
  - admonition
  - pymdownx.details
  - pymdownx.superfences:
      custom_fences:
        - name: mermaid
          class: mermaid
          format: !!python/name:pymdownx.superfences.fence_code_format
  - pymdownx.tabbed:
      alternate_style: true
  - pymdownx.highlight
  - pymdownx.inlinehilite
  - tables
  - toc:
      permalink: true

nav:
  - Accueil: index.md
  - Glossaire: glossaire.md
  - Démarrer:
      - demarrer/index.md
      - Comprendre les templates: demarrer/qu-est-ce-qu-un-template.md
      - Prérequis: demarrer/prerequis.md
      - Créer un projet: demarrer/creer-un-projet.md
      - Premier lancement: demarrer/premier-lancement.md
  - Architecture:
      - architecture/index.md
      - CQRS et Mediator: architecture/cqrs-et-mediator.md
      - Validation: architecture/validation.md
      - Erreurs et Problem Details: architecture/erreurs-et-problem-details.md
  - Fonctionnalités:
      - fonctionnalites/index.md
      - Observabilité: fonctionnalites/observabilite.md
      - Health checks: fonctionnalites/health-checks.md
      - Rate limiting: fonctionnalites/rate-limiting.md
      - Résilience et service discovery: fonctionnalites/resilience-et-service-discovery.md
      - OpenAPI et Scalar: fonctionnalites/openapi-et-scalar.md
      - CORS: fonctionnalites/cors.md
  - Aspire:
      - aspire/index.md
      - AppHost: aspire/apphost.md
      - Émulateurs: aspire/emulateurs.md
      - Paramètres et secrets: aspire/parametres-et-secrets.md
      - Dashboard et CLI: aspire/dashboard-et-cli.md
      - Local et déployé: aspire/local-vs-deploye.md
  - Identité:
      - identite/index.md
      - Entra ID: identite/entra-id.md
      - Keycloak en local: identite/keycloak-local.md
      - Modèles d'autorisation: identite/modeles-d-autorisation.md
      - Configuration locale et déployée: identite/local-vs-deploye.md
  - Tests:
      - tests/index.md
  - Guides:
      - guides/index.md
      - Ajouter une feature: guides/ajouter-une-feature.md
      - Rôles, groupes et personas: guides/ajouter-role-groupe-persona.md
      - Configurer un déploiement: guides/configurer-un-deploiement.md
  - Référence:
      - reference/index.md
      - Options du template: reference/options-du-template.md
      - Configuration: reference/configuration.md
      - Endpoints: reference/endpoints.md
      - Scripts et CI: reference/scripts-et-ci.md
      - Arborescence: reference/arborescence.md
      - Contribuer à la doc: reference/contribuer-a-la-doc.md
```

`docs/.gitattributes` :

```
wiki/** text eol=lf
```

- [ ] **Step 5: Squelette des pages**

Pour chaque fichier listé dans `nav` (46 pages), créer `docs/site/<chemin>` avec exactement :

```markdown
# <Titre de navigation>

!!! info "Page en cours de rédaction"
    Cette page est rédigée dans une prochaine étape.
```

Le `<Titre de navigation>` est la clé de l'entrée (pour un `index.md` de section : le nom de la section, pour `index.md` racine : `Accueil`).

Puis :

Run: `python eng/docs/docs.py wiki && python eng/docs/docs.py check`
Expected: `docs check: OK`.

Run: `python eng/docs/docs.py build`
Expected: Zensical `Build finished`, sans avertissement (mode strict) ; `site/index.html` existe.

- [ ] **Step 6: Commit**

```bash
git add eng/docs mkdocs.yml docs/.gitattributes docs/site docs/wiki
git commit -m "feat(docs): add docs commands, style checks, site configuration and page skeleton"
```

---

### Task 4: CI, README et intégration au template

**Files:**
- Create: `.github/workflows/docs.yml`, `eng/azure-pipelines/docs-ci.yml`, `README.generated.md`
- Modify: `README.md` (réécrit), `.template.config/template.json`, `eng/verify-template.sh`

**Interfaces:**
- Consumes: `python eng/docs/docs.py check|build` (Task 3).
- Produces: workflow `docs` (jobs `check`, `deploy`), pipeline Azure DevOps `eng/azure-pipelines/docs-ci.yml`, README de projet généré.

- [ ] **Step 1: Ajouter les vérifications au script de génération (le « test » de la tâche)**

Dans `eng/verify-template.sh`, ajouter `mkdocs.yml README.generated.md` à la liste de la boucle `for excluded in ...`, puis, juste après cette boucle :

```bash
[ -f README.md ] || fail "the generated project has no README.md."
grep -q "Ci.Generated" README.md || fail "README.md is not the generated project's README."
if grep -q "dotnet new install" README.md; then
  fail "README.md is the template repository README."
fi
```

Run: `bash eng/verify-template.sh AppRoles true true false`
Expected: FAIL — `'mkdocs.yml' leaked into the generated project.`

- [ ] **Step 2: Exclusions et renommage dans `template.json`**

Dans le premier `modifiers` (sans condition), ajouter `"mkdocs.yml"` et `"README.md"` à `exclude`, et ajouter à ce même objet :

```json
          "rename": {
            "README.generated.md": "README.md"
          }
```

- [ ] **Step 3: README du projet généré**

`README.generated.md` :

```markdown
# Web.Template.CQRS

API ASP.NET Core (.NET 10) générée depuis le template Web CQRS : architecture CQRS, Problem Details, authentification Entra ID<!--#if (EnableAspire) -->, orchestration locale Aspire (SQL Server, Azurite, Keycloak)<!--#endif --><!--#if (EnableRateLimiting) -->, limitation de débit<!--#endif -->.

Documentation complète (cours, guides, référence) : https://floriandrevet.github.io/template-CQRS/

<!--#if (EnableAspire) -->
## Lancer en local

Prérequis : SDK .NET 10, Docker, Aspire CLI, certificat de développement approuvé (`dotnet dev-certs https --trust`).

```powershell
dotnet user-secrets set "Parameters:sql-password" "Choose-A-Strong-Local-Password!" --project src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj
dotnet user-secrets set "Parameters:keycloak-admin-password" "Choose-Another-Strong-Password!" --project src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj
aspire start --apphost src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj
```

Obtenir un token pour un persona local puis appeler l'API sur `https://localhost:7246` :

```powershell
./scripts/get-token.ps1 -Persona admin
```

Personas : `admin@local.test` et `user@local.test` (mot de passe `Passw0rd!`, realm local uniquement).
<!--#else -->
## Lancer en local

Configurez `Auth:*` (Entra ID), `ConnectionStrings:projectdb` et `ConnectionStrings:blobs` dans les User Secrets, puis :

```powershell
dotnet run --project src/Web.Template.CQRS.Api/Web.Template.CQRS.Api.csproj
```
<!--#endif -->
<!--#if (EnableTests) -->

## Tests

```powershell
dotnet test --solution Web.Template.CQRS.slnx
```
<!--#endif -->

## Configuration en déploiement

Fournissez par variables d'environnement : `Auth__Provider=Entra`, `Auth__Instance`, `Auth__TenantId`, `Auth__ClientId`<!--#if (UseGroups) -->, `Authorization__Groups__Administrators` (object ID du groupe Entra)<!--#endif -->, `ConnectionStrings__projectdb`, `ConnectionStrings__blobs` et, pour la télémétrie, `APPLICATIONINSIGHTS_CONNECTION_STRING`.
```

- [ ] **Step 4: README du dépôt template**

Remplacer `README.md` par :

```markdown
# Template Web CQRS

Template `dotnet new` pour créer des API ASP.NET Core (.NET 10) : architecture CQRS, authentification Entra ID avec modèle d'autorisation au choix, Problem Details, limitation de débit, tests, et orchestration locale Aspire avec SQL Server, Azurite et un Keycloak qui imite Entra.

**Documentation complète** (cours pour débutants, guides, référence) : https://floriandrevet.github.io/template-CQRS/ — également publiée en wiki de code Azure DevOps depuis `docs/wiki`.

## Démarrage rapide

```powershell
dotnet new install ./
dotnet new templatewebcqrs --name Contoso.Orders
```

Options : `--AuthorizationModel` (`Authenticated`, `AppRoles`, `Groups`, `AppRolesAndGroups`), `--EnableAspire`, `--EnableRateLimiting`, `--EnableTests`. Voir `dotnet new templatewebcqrs --help` et la page « Créer un projet » de la documentation.

## Contribuer

- Template : `bash eng/verify-template.sh <AuthorizationModel> <EnableAspire> <EnableRateLimiting> <EnableTests>` génère, compile et teste une combinaison.
- Documentation : source dans `docs/site`, outillage dans `eng/docs` (`python eng/docs/docs.py check`). Voir la page « Contribuer à la doc ».
```

- [ ] **Step 5: CI GitHub**

`.github/workflows/docs.yml` :

```yaml
name: docs

on:
  push:
    branches: [main]
  pull_request:

permissions:
  contents: read

concurrency:
  group: docs-${{ github.ref }}
  cancel-in-progress: true

jobs:
  check:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v5
      - uses: actions/setup-python@v6
        with:
          python-version: "3.13"
          cache: pip
          cache-dependency-path: eng/docs/requirements.txt
      - run: python -m pip install -r eng/docs/requirements.txt
      - run: python -m pytest eng/docs/tests -q
      - run: python eng/docs/docs.py check

  deploy:
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    needs: check
    runs-on: ubuntu-latest
    permissions:
      contents: read
      pages: write
      id-token: write
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - uses: actions/checkout@v5
      - uses: actions/setup-python@v6
        with:
          python-version: "3.13"
          cache: pip
          cache-dependency-path: eng/docs/requirements.txt
      - run: python -m pip install -r eng/docs/requirements.txt
      - run: python eng/docs/docs.py build
      - uses: actions/configure-pages@v5
      - uses: actions/upload-pages-artifact@v4
        with:
          path: site
      - id: deployment
        uses: actions/deploy-pages@v4
```

- [ ] **Step 6: CI Azure DevOps**

`eng/azure-pipelines/docs-ci.yml` :

```yaml
# Azure DevOps pipeline checking the documentation (same checks as .github/workflows/docs.yml, job "check").
# The Azure DevOps code wiki is published from main:/docs/wiki (see the "Contribuer à la doc" page).

trigger:
  branches:
    include:
      - main

pr:
  branches:
    include:
      - '*'

pool:
  vmImage: ubuntu-latest

steps:
  - checkout: self
  - task: UsePythonVersion@0
    displayName: Use Python 3.13
    inputs:
      versionSpec: '3.13'
  - bash: python -m pip install -r eng/docs/requirements.txt
    displayName: Install documentation tooling
  - bash: python -m pytest eng/docs/tests -q
    displayName: Test documentation tooling
  - bash: python eng/docs/docs.py check
    displayName: Check documentation
```

- [ ] **Step 7: Vérifier**

Run: `python -c "import yaml,sys; [yaml.safe_load(open(f)) for f in sys.argv[1:]]; print('yaml ok')" .github/workflows/docs.yml eng/azure-pipelines/docs-ci.yml`
Expected: `yaml ok`.

Run: `bash eng/verify-template.sh AppRoles true true false`
Expected: `verify-template: OK (...)`.

Run: `bash eng/verify-template.sh Groups false true true`
Expected: `verify-template: OK (...)` ; le `README.md` généré (à inspecter en ajoutant temporairement `cat README.md` si besoin, puis en le retirant) contient la section « Lancer en local » sans Aspire et la variable `Authorization__Groups__Administrators`.

- [ ] **Step 8: Commit**

```bash
git add .github/workflows/docs.yml eng/azure-pipelines/docs-ci.yml README.md README.generated.md .template.config/template.json eng/verify-template.sh
git commit -m "ci(docs): check and deploy the documentation and give generated projects their own README"
```

---

## Tâches de rédaction (Tasks 5 à 11) — règles communes

Chaque tâche de rédaction remplace des pages du squelette. Pour chaque page :

1. Lire les fichiers de code cités avant d'écrire ; ne jamais affirmer un comportement non vérifié dans le code ou la doc officielle.
2. Respecter le fil `## Le concept simplement` → `## Pourquoi c'est important` → `## Comment le template le fait` → `## À essayer` (pages de concept) ; les pages « index » de section et de référence peuvent s'en écarter (présentation, tableaux).
3. Inclure le code avec des directives `--8<--` (jamais de copie manuelle), dans un bloc avec `title="<chemin>"`, en choisissant des ancres uniques (`check` le vérifie). Les inclusions recommandées sont listées ; d'autres sont permises.
4. Au moins un diagramme mermaid par section quand un flux est décrit (séquence d'une requête, dépendances de projets, flux OAuth2…).
5. Relier les pages entre elles par des liens relatifs (`../identite/keycloak-local.md`).
6. Terminer par : `python eng/docs/docs.py wiki && python eng/docs/docs.py check` → `docs check: OK`, puis `python eng/docs/docs.py build` → build strict sans avertissement.
7. Commit : `docs: write the <section> section` avec `docs/site` et `docs/wiki`.

Longueur indicative : 400 à 1 500 mots par page de concept ; les pages « cours » (observabilité, identité/index, Keycloak, tutoriel) peuvent être plus longues.

---

### Task 5: Accueil, glossaire et section « Démarrer »

**Files:**
- Modify: `docs/site/index.md`, `docs/site/glossaire.md`, `docs/site/demarrer/{index,qu-est-ce-qu-un-template,prerequis,creer-un-projet,premier-lancement}.md`, `docs/wiki/**`

**Interfaces:**
- Consumes: outillage (Tasks 1-4). Lit : `.template.config/template.json`, `README.generated.md`, `src/Web.Template.CQRS.AppHost/Program.cs`, `scripts/get-token.ps1`, `scripts/get-token.sh`, `src/Web.Template.CQRS.Api/Api.http`, `src/Web.Template.CQRS.Api/Properties/launchSettings.json`, `src/Web.Template.CQRS.Api/Controllers/*.cs`.

- [ ] **Step 1: Rédiger les pages**

- `index.md` (Accueil) : ce qu'est le template en 3 phrases ; à qui s'adresse la doc ; trois parcours de lecture sous forme de listes de liens : « Je veux créer une API maintenant » (Démarrer → Premier lancement → Ajouter une feature), « Je débute et je veux comprendre » (Comprendre les templates → Architecture → Fonctionnalités → Aspire → Identité), « Je forme une équipe » (ordre de cours conseillé, durée estimée par section, exercices « À essayer ») ; carte du template en mermaid (`graph`) : projets Domain, Application, Infrastructure, Contracts, Api, ServiceDefaults, AppHost et leurs dépendances ; encadré `info` : la doc existe aussi en wiki Azure DevOps.
- `glossaire.md` : tableau terme / définition simple / page où il est expliqué. Au minimum : template, sourceName, symbole de template, CQRS, commande, query, handler, Mediator, pipeline behavior, Problem Details, OpenTelemetry, trace, span, correlation id, traceId, métrique, health check, rate limiting, Aspire, AppHost, ServiceDefaults, émulateur, conteneur, volume, OAuth2, OpenID Connect, JWT, claim, audience, issuer, scope, app role, groupe de sécurité, Entra ID, Keycloak, realm, client, persona, policy, PKCE, Scalar, OpenAPI.
- `demarrer/index.md` : vue d'ensemble de la section et ordre de lecture.
- `demarrer/qu-est-ce-qu-un-template.md` (cours) : ce qu'est `dotnet new` et un template ; `sourceName` (renommage `Web.Template.CQRS` → nom du projet) ; symboles `parameter`/`computed` ; directives `#if` dans le code (et `<!--#if -->`, `//#if`) ; `modifiers` (exclusion/renommage) ; installation/désinstallation (`dotnet new install ./`, `dotnet new uninstall`). Inclusions : `.template.config/template.json` start=`"symbols": {` end=`"sources": [` (symboles), puis start=`"sources": [` (modifiers) ; un extrait de `src/Web.Template.CQRS.Api/Program.cs` montrant un `#if (EnableRateLimiting)` (start=`#if (EnableRateLimiting)` n'est pas unique : utiliser start=`builder.Services.AddInfrastructure(builder.Configuration);` end=`var app = builder.Build();`).
- `demarrer/prerequis.md` : SDK .NET 10 (version de `global.json` incluse : `global.json`), Docker Desktop, Aspire CLI (`dotnet tool install -g Aspire.Cli`), `dotnet dev-certs https --trust` (pourquoi : Keycloak et l'API en HTTPS local ; note Linux sur l'export PEM et `CURL_CA_BUNDLE`), Python seulement pour contribuer à la doc ; onglets PowerShell / Bash pour les commandes ; commande de vérification de chaque prérequis.
- `demarrer/creer-un-projet.md` : installation du template, `dotnet new templatewebcqrs --help` ; tableau complet des options (nom, valeurs, défaut, effet) ; « ce que chaque option ajoute ou retire » (fichiers/projets) ; scénarios clients types avec la commande exacte : API interne simple (`Authenticated`), back-office avec rôles (`AppRoles`), application d'entreprise pilotée par groupes (`Groups`), API sans Aspire pour un environnement contraint (`--EnableAspire false`) ; arborescence générée (renvoi vers « Arborescence »).
- `demarrer/premier-lancement.md` (tutoriel pas à pas) : user-secrets de l'AppHost (inclure `src/Web.Template.CQRS.AppHost/Program.cs` start=`var sqlPassword` end=`.AddDatabase("projectdb");`) ; `aspire start` ; ce qui démarre (diagramme) ; ouvrir le dashboard ; obtenir un token (`get-token`, onglets PowerShell/Bash) ; appeler `GET https://localhost:7246/me` (curl et `Api.http`) et lire la réponse ; `GET /admin/ping` avec admin (200) puis user (403) ; Scalar sur `https://localhost:7246/scalar` (connexion `template-scalar`) ; arrêter (`aspire stop`) ; encadré `warning` sur le port HTTP 5257 qui redirige et perd l'en-tête `Authorization` ; encadré `tip` dépannage (port 8080 occupé, certificat non approuvé, realm non réimporté).

- [ ] **Step 2: Vérifier et commit** (règles communes 6 et 7).

---

### Task 6: Section « Architecture »

**Files:**
- Modify: `docs/site/architecture/{index,cqrs-et-mediator,validation,erreurs-et-problem-details}.md`, `docs/wiki/**`

**Interfaces:**
- Consumes: outillage. Lit : `src/*/DependencyInjection.cs`, `*.csproj`, `src/Web.Template.CQRS.Domain/Common/Models/*.cs`, `src/Web.Template.CQRS.Application/**`, `src/Web.Template.CQRS.Contracts/**`, `src/Web.Template.CQRS.Api/Controllers/*.cs`, `src/Web.Template.CQRS.Api/Errors/ErrorHandling.cs`, `src/Web.Template.CQRS.Api/Program.cs`, `tests/**/ValidationBehaviorTests.cs`, `tests/**/ErrorHandlingTests.cs`.

- [ ] **Step 1: Rédiger les pages**

- `architecture/index.md` (cours) : Clean Architecture expliquée simplement (règle des dépendances, pourquoi le domaine ne dépend de rien) ; rôle de chaque projet (Domain, Application, Infrastructure, Contracts, Api, ServiceDefaults, AppHost) ; diagramme mermaid des références de projets (vérifié dans les `.csproj`) ; où placer un nouveau fichier (tableau « je veux ajouter… → projet / dossier ») ; modèles du domaine `Entity`, `AggregateRoot`, `ValueObject` (inclure `src/Web.Template.CQRS.Domain/Common/Models/ValueObject.cs`) ; `IRepository` / `BaseRepository` ; injection de dépendances par couche (`AddPresentation`, `AddApplication`, `AddInfrastructure` : inclure `src/Web.Template.CQRS.Api/Program.cs` start=`builder.Services.AddEndpointsApiExplorer();` end=`builder.Services.AddInfrastructure(builder.Configuration);`).
- `architecture/cqrs-et-mediator.md` (cours) : CQRS (commande vs query, pourquoi séparer) ; le pattern Mediator ; la bibliothèque Mediator (génération de code à la compilation, pas de réflexion) ; configuration (inclure `src/Web.Template.CQRS.Application/DependencyInjection.cs`) ; **parcours complet de `GET /me`** en diagramme de séquence (client → middleware d'authentification → endpoint → `IMediator.Send` → `ValidationBehavior` → `GetCurrentUserQueryHandler` → `ICurrentUser`/`HttpCurrentUser` → `CurrentUserResult` → `CurrentUserResponse`) avec chaque étape incluse (`CurrentUserController.cs`, `GetCurrentUserQuery.cs`, `GetCurrentUserQueryHandler.cs`, `HttpCurrentUser.cs`, `CurrentUserResponse.cs`) ; pourquoi séparer `Result` (Application) et `Response` (Contracts) ; second exemple `PingAdmin`.
- `architecture/validation.md` : FluentValidation ; pipeline behavior ; inclure `ValidationBehavior.cs` ; exemple de validateur : inclure la classe `SampleCommandValidator` de `tests/Web.Template.CQRS.Application.UnitTests/Common/ValidationBehaviorTests.cs` avec start=`public sealed class SampleCommandValidator` (sans `end`, jusqu'à la fin du fichier) ; ce que reçoit le client (renvoi vers Problem Details).
- `architecture/erreurs-et-problem-details.md` (cours) : RFC 9457 ; pourquoi un format standard ; inclure `ErrorHandling.cs` ; codes `VALIDATION`, `INTERNAL`, `RATE_LIMITED` ; `traceId` dans la réponse (inclure `Program.cs` start=`builder.Services.AddProblemDetails(` end=`});`) et lien vers Observabilité ; exemples JSON de réponses 400 / 500 / 429 ; pourquoi ne jamais exposer le message d'exception (test `UnexpectedException_Returns500WithoutInternalDetails`).

- [ ] **Step 2: Vérifier et commit** (règles communes 6 et 7).

---

### Task 7: Section « Fonctionnalités »

**Files:**
- Modify: `docs/site/fonctionnalites/{index,observabilite,health-checks,rate-limiting,resilience-et-service-discovery,openapi-et-scalar,cors}.md`, `docs/wiki/**`

**Interfaces:**
- Consumes: outillage. Lit : `src/Web.Template.CQRS.ServiceDefaults/Extensions.cs`, `src/Web.Template.CQRS.Api/Observability/RequestLoggingScopeMiddleware.cs`, `src/Web.Template.CQRS.Api/Program.cs`, `src/Web.Template.CQRS.Api/Common/RateLimiting/*.cs`, `src/Web.Template.CQRS.Api/Configuration/OpenApiExtensions.cs`, `src/Web.Template.CQRS.Api/Authentication/ScalarOAuth2.cs`, `src/Web.Template.CQRS.Api/appsettings*.json`, `tests/**/RateLimitingTests.cs`.

- [ ] **Step 1: Rédiger les pages**

- `fonctionnalites/index.md` : tableau des fonctionnalités transverses, où elles sont configurées, option de template qui les active.
- `fonctionnalites/observabilite.md` (**cours complet**) : les trois piliers (logs, traces, métriques) avec une analogie ; logs structurés vs texte ; trace, span, `Activity` en .NET ; **correlation id** : le problème (« quelle ligne de log appartient à quelle requête ? »), la solution, l'identifiant de trace W3C (`traceparent`) propagé entre services, `TraceId` vs `TraceIdentifier` ; OpenTelemetry (standard ouvert, exportateurs OTLP / Azure Monitor) ; comment le template le fait : `ConfigureOpenTelemetry` (inclure `Extensions.cs` start=`public static TBuilder ConfigureOpenTelemetry` end=`        return builder;`… avec des ancres uniques), filtrage de `/health` et `/alive`, export OTLP si `OTEL_EXPORTER_OTLP_ENDPOINT`, Azure Monitor si `APPLICATIONINSIGHTS_CONNECTION_STRING` ; `RequestLoggingScopeMiddleware` (inclure le fichier) : scope de logs `TraceId`/`RequestId`/`Endpoint` et tag `app.endpoint` ; `traceId` renvoyé dans les Problem Details pour relier une erreur côté client à la trace côté serveur (diagramme de séquence : client reçoit une 500 avec traceId → support cherche la trace) ; lire une trace et filtrer les logs par TraceId dans le dashboard Aspire ; en production : Application Insights (requêtes KQL simples d'exemple : `requests | where operation_Id == "<traceId>"`) ; À essayer : provoquer une 401, retrouver la requête dans le dashboard, relier logs et trace.
- `fonctionnalites/health-checks.md` : liveness vs readiness ; `/health` et `/alive` (inclure `Extensions.cs` start=`public static TBuilder AddDefaultHealthChecks` et `public static WebApplication MapDefaultEndpoints` avec des fins uniques) ; exposés seulement en Development/Testing (pourquoi) ; usage par Aspire (`WithHttpHealthCheck("/alive")`) et par un orchestrateur en production ; ajouter un health check (exemple base de données en texte, sans l'ajouter au template).
- `fonctionnalites/rate-limiting.md` (cours) : pourquoi limiter ; algorithmes (fenêtre fixe, glissante, token bucket) ; le choix du template (inclure `RateLimiting.cs` et `RateLimitingPolicies.cs`) ; partition par `oid`/`sub` ou IP ; 429 + `Retry-After` + Problem Details `RATE_LIMITED` ; appliquer une politique à un endpoint (`RequireRateLimiting`) ; tableau des quotas ; limites (instance unique, pas de stockage distribué) ; test `RateLimitingTests`.
- `fonctionnalites/resilience-et-service-discovery.md` : `AddStandardResilienceHandler` (retries, circuit breaker, timeouts) et `AddServiceDiscovery` (inclure `Extensions.cs` start=`public static TBuilder AddServiceDefaults` end=`        return builder;` en vérifiant l'unicité) ; quand ils s'appliquent (HttpClient) ; exemple d'appel d'un autre service.
- `fonctionnalites/openapi-et-scalar.md` : OpenAPI (contrat d'API), document `/openapi/v1.json`, transformers (inclure `OpenApiExtensions.cs`), Scalar et le flux OAuth2 Authorization Code + PKCE (inclure `ScalarOAuth2.cs`, et `Program.cs` start=`if (app.Environment.IsDevelopment())` end=`}).AllowAnonymous();`), seulement en Development.
- `fonctionnalites/cors.md` : ce qu'est CORS (navigateur uniquement) ; configuration `Cors:AllowedOrigins` (inclure `Program.cs` start=`builder.Services.AddCors(` end=`});` en vérifiant que la fin englobe le bloc complet, sinon choisir l'ancre de fin `builder.Services.AddEndpointsApiExplorer();`) ; valeurs locales et en production.

- [ ] **Step 2: Vérifier et commit** (règles communes 6 et 7).

---

### Task 8: Section « Aspire »

**Files:**
- Modify: `docs/site/aspire/{index,apphost,emulateurs,parametres-et-secrets,dashboard-et-cli,local-vs-deploye}.md`, `docs/wiki/**`

**Interfaces:**
- Consumes: outillage. Lit : `src/Web.Template.CQRS.AppHost/**`, `src/Web.Template.CQRS.ServiceDefaults/**`, `Directory.Packages.props`, `src/Web.Template.CQRS.Infrastructure/DependencyInjection.cs`, `src/Web.Template.CQRS.Api/appsettings*.json`, `README.generated.md`.

- [ ] **Step 1: Rédiger les pages**

- `aspire/index.md` (cours) : le problème (lancer une API + base + stockage + identité à la main), ce qu'est Aspire (orchestration locale en C#, dashboard, télémétrie, ServiceDefaults), ce qu'Aspire n'est pas (pas un hébergeur de production) ; AppHost vs ServiceDefaults ; diagramme des ressources du template.
- `aspire/apphost.md` : l'AppHost ligne par ligne (inclure `src/Web.Template.CQRS.AppHost/Program.cs` par blocs avec ancres uniques) : `AddParameter`, `AddSqlServer` + `WithDataVolume` + `ContainerLifetime.Persistent`, `AddDatabase`, `AddAzureStorage().RunAsEmulator().AddBlobs`, `AddKeycloak(port: 8080)` + `WithRealmImport`, `AddProject` + `WithReference` (injection des chaînes de connexion `ConnectionStrings__projectdb` / `ConnectionStrings__blobs` lues par `AddInfrastructure`), `WaitFor`, `WithEnvironment("Auth__…")`, `WithHttpHealthCheck` ; le SDK `Aspire.AppHost.Sdk` (inclure le `.csproj` de l'AppHost).
- `aspire/emulateurs.md` (cours) : émulateur vs service réel (tableau SQL Server conteneur ↔ Azure SQL, Azurite ↔ Azure Blob Storage, Keycloak ↔ Entra ID) ; ce qu'est un conteneur et un volume ; persistance (`ContainerLifetime.Persistent`, `WithDataVolume`) ; réinitialiser les données (noms de volumes `<apphost-en-minuscules>-<hash>-…-data`, commandes `docker volume ls --filter name=keycloak-data`, `docker rm -f`, `docker volume rm`) ; conflits de ports (8080) ; Keycloak en HTTPS avec le certificat de développement.
- `aspire/parametres-et-secrets.md` : `AddParameter(secret: true)`, User Secrets (où ils sont stockés, pourquoi pas dans le dépôt), `UserSecretsId` dans les `.csproj`, commandes `dotnet user-secrets set/list/remove`.
- `aspire/dashboard-et-cli.md` : dashboard (ressources, console, logs structurés, traces, métriques) ; Aspire CLI (`aspire start`, `aspire stop`, `aspire --help`) ; MCP Aspire : présentation (permet à un assistant IA d'interroger ressources, logs et traces) avec renvoi vers la future documentation du harnais agentique.
- `aspire/local-vs-deploye.md` : tableau ressource par ressource (local → déployé, variable de configuration à fournir) ; télémétrie (dashboard → Application Insights) ; identité (Keycloak → Entra, lien vers « Configuration locale et déployée »).

- [ ] **Step 2: Vérifier et commit** (règles communes 6 et 7).

---

### Task 9: Section « Identité »

**Files:**
- Modify: `docs/site/identite/{index,entra-id,keycloak-local,modeles-d-autorisation,local-vs-deploye}.md`, `docs/wiki/**`

**Interfaces:**
- Consumes: outillage. Lit : `src/Web.Template.CQRS.Api/Authentication/**`, `src/Web.Template.CQRS.Api/Authorization/**`, `src/Web.Template.CQRS.Api/Controllers/AdminController.cs`, `src/Web.Template.CQRS.AppHost/Realms/template-realm.json`, `src/Web.Template.CQRS.AppHost/RealmVariants/*.json`, `src/Web.Template.CQRS.Api/appsettings*.json`, `scripts/get-token.*`, `tests/**/AdminEndpointTests.cs`, `tests/**/TestAuthHandler.cs`.

- [ ] **Step 1: Rédiger les pages**

- `identite/index.md` (**cours**) : authentification vs autorisation ; OAuth2 (rôles : client, serveur d'autorisation, API) ; OpenID Connect ; flux Authorization Code + PKCE en diagramme de séquence (Scalar → Keycloak/Entra → API) ; **anatomie d'un JWT** (header, payload, signature ; claims `iss`, `aud`, `exp`, `oid`, `sub`, `name`, `email`, `roles`, `groups`, `scp`) avec un exemple de payload décodé d'un token Keycloak du template ; validation par l'API (signature via JWKS, issuer, audience, expiration) ; scopes vs app roles vs groupes (tableau) ; pourquoi Entra est la cible et Keycloak un émulateur.
- `identite/entra-id.md` : App Registration de l'API (Application ID URI, audience), app roles (valeurs = constantes de `AppRoles.cs`, assignation dans Enterprise applications), claim de groupes (Token configuration → Security groups → Group ID, limite des 200 groupes), App Registration cliente pour Scalar (redirect URI), clés `Auth:*` (inclure `AuthOptions.cs` et `AuthenticationSetup.cs`) et validation au démarrage.
- `identite/keycloak-local.md` (**page de référence Keycloak**) : pourquoi un Keycloak local ; ce qu'est un realm, un client, un utilisateur, un rôle, un groupe ; le realm `template` (inclure des extraits de `template-realm.json` : rôles, groupe avec `objectId`, un persona, le client `template-cli`, les mappers `entra-oid`, `entra-roles`, `entra-groups` — ancres uniques, par exemple start=`"clientId": "template-cli"` end=`"protocolMappers"` puis mappers par leur `"name"` si unique, sinon décrire en prose) ; **comment le realm imite Entra** (tableau claim Entra ↔ mapper Keycloak) ; les trois clients (`template-api` bearer only, `template-scalar` PKCE, `template-cli` password grant local uniquement) ; personas et mot de passe ; obtenir un token (onglets PowerShell/Bash, décoder le token avec jwt.ms en précisant de n'y coller que des tokens locaux) ; console d'administration Keycloak (`https://localhost:8080`, compte admin et mot de passe du paramètre `keycloak-admin-password`) ; créer un utilisateur à la main ; **import au premier démarrage uniquement** et procédure de réinitialisation ; variantes de realm par modèle d'autorisation (`RealmVariants/`) ; HTTPS et certificat de développement ; dépannage (401 « issuer invalide », 401 sur le port 5257, `invalid_grant`).
- `identite/modeles-d-autorisation.md` : les quatre modèles (tableau) ; fallback policy (utilisateur authentifié) ; `RequireAdmin` selon le modèle (inclure `AuthorizationSetup.cs`, `Policies.cs`, `AppRoles.cs`, `GroupOptions.cs`) ; directives `#if` expliquées ; comparaison des groupes en GUID (pourquoi : casse, accolades, espaces) ; protéger un endpoint (inclure `AdminController.cs`) ; comment c'est testé (`TestAuthHandler`, `AdminEndpointTests`).
- `identite/local-vs-deploye.md` : **tableau exhaustif** clé par clé (`Auth:Provider`, `Auth:Authority`, `Auth:Audience`, `Auth:Instance`, `Auth:TenantId`, `Auth:ClientId`, `Auth:OpenApiClientId`, `Auth:OpenApiScope`, `Authorization:Groups:Administrators`, `Cors:AllowedOrigins`) : valeur locale (Keycloak via Aspire / `appsettings.Development.json`), valeur déployée (Entra), où la fournir (variable d'environnement `Auth__…`) ; inclure `appsettings.json` et `appsettings.Development.json` ; ce qui ne change pas (le code, les policies, les claims) ; check-list de passage en production.

- [ ] **Step 2: Vérifier et commit** (règles communes 6 et 7).

---

### Task 10: Tests et guides pratiques (dont le tutoriel « Product » vérifié)

**Files:**
- Modify: `docs/site/tests/index.md`, `docs/site/guides/{index,ajouter-une-feature,ajouter-role-groupe-persona,configurer-un-deploiement}.md`, `docs/wiki/**`

**Interfaces:**
- Consumes: outillage. Lit : `tests/**`, `src/**` (Domain, Application, Infrastructure, Api, Contracts), `src/Web.Template.CQRS.AppHost/Realms/template-realm.json`, `eng/verify-template.sh`.

- [ ] **Step 1: Rédiger `tests/index.md`**

Pyramide des tests ; projets de tests ; xUnit v3 et Microsoft.Testing.Platform (`global.json`, inclure `global.json`) ; tests unitaires d'un handler (inclure `GetCurrentUserQueryHandlerTests.cs`) ; tests d'intégration avec `WebApplicationFactory` : `ApiFactory` (inclure), `TestAuthHandler` et les en-têtes `X-Test-User` / `X-Test-Roles` / `X-Test-Groups` (inclure), exemple `AdminEndpointTests` ; remplacement de SQL Server par EF InMemory et du stockage par un faux ; lancer les tests (`dotnet test --solution …`, filtrer un test) ; où ajouter les tests d'une nouvelle feature.

- [ ] **Step 2: Rédiger et vérifier le tutoriel `guides/ajouter-une-feature.md`**

Tutoriel fil rouge « Product » (gestion d'un catalogue de produits), en étapes numérotées, chaque étape avec le code complet du fichier à créer ou la modification exacte, et une étape « vérifier » :

1. Générer un projet `Contoso.Catalog` (`--AuthorizationModel AppRoles`).
2. Domaine : `ProductId : ValueObject`, `Product : AggregateRoot<ProductId>` (nom, prix, règles d'invariants dans le constructeur).
3. Infrastructure : `ProductConfiguration : IEntityTypeConfiguration<Product>` (conversion de `ProductId`), `DbSet<Product>`, `IProductRepository` (Application) / `ProductRepository : BaseRepository<Product, ProjectDbContext>` (Infrastructure) et son enregistrement.
4. Migration EF : installation de `dotnet-ef`, commande `dotnet ef migrations add InitialCatalog` (projet et startup project exacts ; ajouter ce qui est nécessaire au projet de démarrage si l'outil l'exige), application de la migration en local (méthode choisie et expliquée : `dotnet ef database update` avec la chaîne de connexion lue dans le dashboard Aspire, ou migration au démarrage en Development).
5. Application : `CreateProductCommand` + validateur + handler, `GetProductQuery` + handler, résultats.
6. Contracts : requête/réponse.
7. Api : `ProductsController` (`POST /products` protégé par `Policies.RequireAdmin` + politique `write`, `GET /products/{id}` authentifié + politique `read`), `Program.cs` (`MapProductEndpoints`).
8. Tests : unitaires (validateur, handler) et intégration (201 en admin, 403 en user, 400 Problem Details sur nom vide, 404 inconnu).
9. Essayer en local avec les personas.

**Vérification obligatoire** : appliquer le tutoriel tel qu'écrit à un projet généré dans un dossier temporaire (`dotnet new templatewebcqrs --name Contoso.Catalog --AuthorizationModel AppRoles --output <tmp>`), puis `dotnet build Contoso.Catalog.slnx` et `dotnet test --solution Contoso.Catalog.slnx` doivent réussir, et `dotnet ef migrations add InitialCatalog` doit produire une migration. Corriger la page jusqu'à ce que ce soit vrai. Consigner les sorties dans le rapport. Supprimer le dossier temporaire ensuite. Le code de ce tutoriel est écrit dans la page (il n'existe pas dans le dépôt : exception prévue par la spec).

- [ ] **Step 3: Rédiger les autres guides**

- `guides/index.md` : liste des guides et quand les utiliser.
- `guides/ajouter-role-groupe-persona.md` : ajouter un rôle (constante `AppRoles`, realm local : rôle + attribution au persona, app role Entra, policy, tests) ; ajouter un groupe (propriété `GroupOptions` + validation, configuration locale/déployée, groupe Keycloak avec `objectId`, groupe Entra) ; ajouter un persona (utilisateur du realm avec `firstName`/`lastName`, sinon Keycloak exige une mise à jour du profil et le password grant échoue) ; rappeler la réinitialisation du volume Keycloak pour réimporter le realm ; check-list finale.
- `guides/configurer-un-deploiement.md` : variables d'environnement à fournir (lien vers Référence › Configuration), secrets (Key Vault ou variables protégées), Application Insights, CORS, HTTPS, health checks non exposés hors Development, image Docker (inclure `src/Web.Template.CQRS.Api/Dockerfile`, commande `docker build`), ce que le template ne fournit pas (infrastructure as code).

- [ ] **Step 4: Vérifier et commit** (règles communes 6 et 7).

---

### Task 11: Section « Référence » et page « Contribuer à la doc »

**Files:**
- Modify: `docs/site/reference/{index,options-du-template,configuration,endpoints,scripts-et-ci,arborescence,contribuer-a-la-doc}.md`, `docs/wiki/**`

**Interfaces:**
- Consumes: outillage et toutes les pages précédentes (liens). Lit : `.template.config/template.json`, `src/Web.Template.CQRS.Api/appsettings*.json`, `src/Web.Template.CQRS.Api/Controllers/*.cs`, `src/Web.Template.CQRS.ServiceDefaults/Extensions.cs`, `scripts/*`, `eng/verify-template.sh`, `.github/workflows/*.yml`, `eng/azure-pipelines/*.yml`, `eng/docs/**`, `mkdocs.yml`.

- [ ] **Step 1: Rédiger les pages**

- `reference/index.md` : sommaire de la référence.
- `reference/options-du-template.md` : chaque option (type, valeurs, défaut, symboles calculés, fichiers ajoutés/retirés, effet sur le realm) ; inclure `template.json` (symboles).
- `reference/configuration.md` : toutes les clés de configuration (section, clé, type, défaut, obligatoire, exemple, variable d'environnement équivalente `Section__Clé`) : `Auth:*`, `Authorization:Groups:*`, `Cors:AllowedOrigins`, `ConnectionStrings:projectdb`, `ConnectionStrings:blobs`, `BlobSettings:ContainerName`, `Logging:*`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `APPLICATIONINSIGHTS_CONNECTION_STRING` ; inclure `appsettings.json`.
- `reference/endpoints.md` : tableau méthode / chemin / authentification / policy / rate limit / environnement / réponse (`/me`, `/admin/ping`, `/openapi/v1.json`, `/scalar`, `/health`, `/alive`, fallback 404).
- `reference/scripts-et-ci.md` : `scripts/get-token.ps1` et `.sh` (paramètres, variables, codes de sortie), `eng/verify-template.sh` (arguments, vérifications), workflows GitHub (`template-ci.yml`, `docs.yml`) et pipelines Azure DevOps (`template-ci.yml`, `docs-ci.yml`) : déclencheurs, jobs, création du pipeline à partir d'un YAML existant, branch policy pour les PR Azure Repos ; activer GitHub Pages (Settings › Pages › Source : GitHub Actions).
- `reference/arborescence.md` : arborescence d'un projet généré (toutes options activées) commentée, et ce qui change par option.
- `reference/contribuer-a-la-doc.md` : organisation (`docs/site`, `docs/wiki`, `mkdocs.yml`, `eng/docs`) ; installer l'outillage ; écrire une page (squelette de page de concept) ; syntaxe autorisée avec exemples **dans des blocs de code** (encadrés, onglets, mermaid, blocs titrés, inclusions `--8<--` avec `start`/`end` et leurs règles) ; ce qui est interdit et pourquoi (wiki Azure DevOps) ; commandes `resolve`, `wiki`, `build`, `serve`, `check` ; ajouter une page à la navigation (pas de tiret dans les titres) ; **publier le wiki de code Azure DevOps** : *Publish code as wiki* (dépôt, branche `main`, dossier `/docs/wiki`) ou `az devops wiki create --name "Template Web CQRS" --type codewiki --repository <Repo> --mapped-path /docs/wiki --version main` ; le wiki suit `main` automatiquement ; ne jamais modifier `docs/wiki` à la main ; Zensical et repli Material for MkDocs.

- [ ] **Step 2: Vérifier et commit** (règles communes 6 et 7).

---

### Task 12: Vérification finale

**Files:**
- Modify: uniquement si une vérification échoue.

- [ ] **Step 1: Contrôles**

Run: `python -m pytest eng/docs/tests -q` → tous PASS.
Run: `python eng/docs/docs.py check` → `docs check: OK`.
Run: `python eng/docs/docs.py build` → build strict sans avertissement.
Run: `grep -rl "Page en cours de rédaction" docs/site` → aucun résultat.
Run: `bash eng/verify-template.sh AppRolesAndGroups true true true` et `bash eng/verify-template.sh Authenticated false false false` → `verify-template: OK`.
Run: `dotnet test --solution Web.Template.CQRS.slnx` → tous PASS (aucun code de production modifié par ce sous-projet).

- [ ] **Step 2: Relecture du rendu**

Run: `python eng/docs/docs.py serve` en arrière-plan, ouvrir `http://127.0.0.1:8000`, vérifier : navigation complète, recherche, un encadré, des onglets, un diagramme mermaid, un bloc de code inclus avec son titre. Arrêter le serveur. Ouvrir `docs/wiki/Identité/Keycloak-en-local.md` et vérifier que les encadrés sont des citations, les onglets des libellés en gras, les liens en `/Dossier/Page.md`.

- [ ] **Step 3: Commit éventuel**

```bash
git add -A docs eng mkdocs.yml
git commit -m "docs: final documentation fixes"
```
