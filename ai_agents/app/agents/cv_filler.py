from app.models.cv_model import CVDraft

def fill_cv_template(draft: CVDraft, template_id: str) -> CVDraft:
    """
    Agent 3: Fills a CV template using the data provided by Agent 2.
    """
    # TODO: Implement templating logic here (e.g., Jinja2 or LaTeX generation)
    print(f"Filling template '{template_id}'...")
    return draft  # Returning the draft updated with template markup for now
