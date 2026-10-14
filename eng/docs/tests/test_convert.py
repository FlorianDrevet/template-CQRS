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
