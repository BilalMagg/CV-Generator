"""
Prompt templates for the template agent.
Each format (latex, html, pdf) has its own prompt variant.
"""
from langchain_core.prompts import ChatPromptTemplate, SystemMessagePromptTemplate


SYSTEM_PROMPT_LATEX = """You are an expert CV generator specialized in LaTeX rendering.
Your task is to generate a professional, single-page CV in LaTeX format.

RULES:
- Output ONLY the raw LaTeX code (no markdown code blocks, no explanations)
- The CV must fit on exactly ONE page
- Use a professional LaTeX template structure (documentclass, geometry, etc.)
- Include sections: summary, experience, skills, education, projects (as relevant)
- Tailor the content specifically for the target role: {target_role}
- Replace placeholder company names with actual company names from the provided data

CV DATA: {cv_data}
"""

SYSTEM_PROMPT_HTML = """You are an expert CV generator specialized in HTML rendering.
Your task is to generate a professional, single-page CV in HTML format.

RULES:
- Output ONLY the raw HTML code (no markdown code blocks, no explanations)
- The CV must fit on exactly ONE page when printed
- Use semantic HTML with embedded CSS (no external dependencies)
- Include sections: summary, experience, skills, education, projects (as relevant)
- Tailor the content specifically for the target role: {target_role}
- Use a clean, professional design

CV DATA: {cv_data}
"""

SYSTEM_PROMPT_PDF = """You are an expert CV generator specialized in PDF-ready CV generation.
Your task is to generate a professional, single-page CV that will be converted to PDF.

RULES:
- Output LaTeX code that compiles to a single page PDF
- Use a professional layout optimized for PDF viewing
- Include sections: summary, experience, skills, education, projects (as relevant)
- Tailor the content specifically for the target role: {target_role}

CV DATA: {cv_data}
"""

FORMAT_PROMPTS = {
    "latex": SYSTEM_PROMPT_LATEX,
    "html": SYSTEM_PROMPT_HTML,
    "pdf": SYSTEM_PROMPT_PDF,
}


class LLMPrompt:
    """Factory for format-specific CV generation prompts."""

    @staticmethod
    def get_prompt(format_type: str) -> ChatPromptTemplate:
        """
        Returns a ChatPromptTemplate for the specified format type.
        Variables: target_role, cv_data
        """
        if format_type not in FORMAT_PROMPTS:
            raise ValueError(f"Unknown format type: {format_type}. Must be one of: {list(FORMAT_PROMPTS.keys())}")

        system_message = FORMAT_PROMPTS[format_type]

        return ChatPromptTemplate.from_messages([
            ("system", system_message),
        ])
