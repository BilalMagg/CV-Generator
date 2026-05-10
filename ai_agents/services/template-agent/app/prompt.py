"""
Prompt templates for the template agent.
Each format (latex, html) has its own prompt variant.
"""
from langchain_core.prompts import ChatPromptTemplate, SystemMessagePromptTemplate


SYSTEM_PROMPT_LATEX = """You are an expert CV generator specialized in LaTeX rendering.
Your task is to generate a professional, single-page CV in LaTeX format using the provided template structure.

TEMPLATE:
{template_code}

CV DATA:
{cv_data}

RULES:
- Output ONLY the raw LaTeX code (no markdown code blocks, no explanations)
- The CV must fit on exactly ONE page
- Use the provided template structure as the base
- Include sections: summary, experience, skills, education, projects (as relevant)
- Tailor the content specifically for the target role: {target_role}
- Replace placeholder company names with actual company names from the provided data
- Use pdflatex-compatible packages only (avoid fontspec, use pdftex-compatible alternatives)
- Default font: Times or Computer Modern (built-in LaTeX fonts)

IMPORTANT: At the end of your response, include a JSON block with the sections breakdown in this format:
```json
{{"sections": {{"summary": "...", "experience": "...", "skills": "..."}}}}
```"""

SYSTEM_PROMPT_HTML = """You are an expert CV generator specialized in HTML rendering.
Your task is to generate a professional, single-page CV in HTML format using the provided template structure.

TEMPLATE:
{template_code}

CV DATA:
{cv_data}

RULES:
- Output ONLY the raw HTML code (no markdown code blocks, no explanations)
- The CV must fit on exactly ONE page when printed
- Use the provided template structure as the base
- Use semantic HTML with embedded CSS (no external dependencies)
- Include sections: summary, experience, skills, education, projects (as relevant)
- Tailor the content specifically for the target role: {target_role}
- Use a clean, professional design

IMPORTANT: At the end of your response, include a JSON block with the sections breakdown in this format:
```json
{{"sections": {{"summary": "...", "experience": "...", "skills": "..."}}}}
```"""

FORMAT_PROMPTS = {
    "latex": SYSTEM_PROMPT_LATEX,
    "html": SYSTEM_PROMPT_HTML,
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
