from app.models.cv_model import OptimizedCV

def deliver_cv(cv: OptimizedCV, email: str) -> bool:
    """
    Agent 5: Sends the generated CV to the recipient.
    """
    # TODO: Implement Email / SMTP sending logic here, attach generated PDF
    print(f"Delivering optimized CV to {email}...")
    return True
