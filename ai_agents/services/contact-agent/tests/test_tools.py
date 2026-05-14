import pytest
import json
from app.tools import extract_cv_text


def test_extract_cv_text_normal_case():
    sections_json = json.dumps([
        {"section_type": "contact", "content": "John Doe - john@email.com", "order": 2},
        {"section_type": "summary", "content": "Experienced developer", "order": 1},
    ])

    result = extract_cv_text.invoke({"sections_json": sections_json})

    assert "=== SUMMARY ===" in result
    assert "=== CONTACT ===" in result
    assert result.index("=== SUMMARY ===") < result.index("=== CONTACT ===")


def test_extract_cv_text_empty_list():
    result = extract_cv_text.invoke({"sections_json": "[]"})
    assert result == "Error: sections_json is empty. No CV content to extract."


def test_extract_cv_text_invalid_json():
    result = extract_cv_text.invoke({"sections_json": "not valid json"})
    assert "Error: Could not parse sections_json" in result


def test_extract_cv_text_section_without_content():
    sections_json = json.dumps([
        {"section_type": "skills", "content": "", "order": 1},
    ])
    result = extract_cv_text.invoke({"sections_json": sections_json})
    assert result == ""
