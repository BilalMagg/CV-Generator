"""
Template Agent tools.
"""
from cvtools import get_template_object, TEMPLATES_BUCKET


def get_template(template_id: str) -> dict:
    """
    Fetch template JSON from MINIO cv-templates bucket.

    Returns:
        dict with keys: id, type, latex_code, html_code
    """
    return get_template_object(template_id, TEMPLATES_BUCKET)


def get_template_type(template_id: str) -> str:
    """
    Get template format type (latex or html).

    Args:
        template_id: The template ID to look up.

    Returns:
        "latex" or "html"
    """
    template = get_template(template_id)
    return template.get("type", "latex") #defualt is latex


def get_template_code(template_id: str) -> tuple[str, str]:
    """
    Get template code and format type.

    Args:
        template_id: The template ID to look up.

    Returns:
        Tuple of (template_code, format_type)
    """
    template = get_template(template_id)
    template_type = template.get("type", "latex")

    if template_type == "latex":
        return template.get("latex_code", ""), "latex"
    elif template_type == "html":
        return template.get("html_code", ""), "html"
    else:
        return template.get("latex_code", ""), "latex"