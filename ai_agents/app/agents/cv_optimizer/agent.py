"""
CV Optimizer agent — logic to polish CV for ATS.
"""
from app.agents.cv_optimizer.schemas import OptimizerInput, OptimizerOutput
from app.models.cv_model import OptimizedCV

async def optimize_cv(input_data: OptimizerInput) -> OptimizerOutput:
    """
    TODO: Implement LLM optimization logic.
    For now, returns a stub.
    """
    optimized = OptimizedCV(
        job_id=input_data.job_id,
        final_sections=input_data.rendered_sections,
        ats_score_estimate=85,
        optimization_notes=["Keywords matched well", "Added missing action verbs"]
    )
    return OptimizerOutput(
        optimized_cv=optimized,
        suggestions=["Add more quantifiable results"]
    )
