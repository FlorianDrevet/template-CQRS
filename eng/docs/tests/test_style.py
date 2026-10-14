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


def test_single_quoted_include_is_valid() -> None:
    text = "```csharp\n--8<-- \"src/A.cs\" start='\"x\"' before='y'\n```\n"

    assert check_page(text, "p.md") == []
