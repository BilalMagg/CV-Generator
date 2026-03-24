"""
Dynamic Workflow Orchestrator.
Exectutes a sequence of agents based on a JSON definition from the backend.
"""
import logging
from uuid import UUID
from typing import Any, Dict

from app.core import backend_client
from app.models.workflow_model import WorkflowResponse, WorkflowNode
from app.workflows.context import WorkflowContext

# Agent Imports
from app.agents.job_extractor.agent import extract_job_requirements
from app.agents.job_extractor.schemas import ExtractorInput
from app.agents.search_agent.agent import match_candidate_data
from app.agents.search_agent.schemas import SearchInput
from app.agents.template_agent.agent import render_template
from app.agents.template_agent.schemas import TemplateInput
from app.agents.cv_optimizer.agent import optimize_cv
from app.agents.cv_optimizer.schemas import OptimizerInput
from app.agents.contact_agent.agent import deliver_cv
from app.agents.contact_agent.schemas import ContactInput

# Shared Models for construction
from app.models.cv_model import CVDraft

logger = logging.getLogger(__name__)

async def run_dynamic_workflow(workflow_id: UUID, user_id: UUID, initial_data: Dict[str, Any]) -> WorkflowContext:
    """
    Fetches the workflow definition and executes it node by node.
    """
    # 1. Fetch Workflow Definition
    workflow: WorkflowResponse = await backend_client.get_workflow(workflow_id)
    
    # 2. Initialise Context
    context = WorkflowContext(
        user_id=user_id,
        job_description=initial_data.get("job_description")
    )
    context.log(f"Starting workflow: {workflow.name}")

    # 3. Always fetch base user data at start (standard requirement)
    # Alternatively, this could be a 'fetch_user' node.
    context.user_profile = await backend_client.get_user(user_id)
    context.experiences = await backend_client.get_user_experiences(user_id)
    context.projects = await backend_client.get_user_projects(user_id)
    context.skills = await backend_client.get_user_skills(user_id)
    context.log("User data fetched from backend")

    # 4. Execute Nodes
    for node in workflow.nodes:
        context.log(f"Executing node: {node.type}")
        
        try:
            if node.type == "extractor":
                # INPUT: job_description from context
                if not context.job_description:
                    raise ValueError("Job description missing for extractor node")
                
                inp = ExtractorInput(job_description=context.job_description)
                out = await extract_job_requirements(inp)
                
                # OUTPUT: Update requirements in context
                context.job_requirements = out
            
            elif node.type == "search":
                # INPUT: user_id + job_requirements
                if not context.job_requirements:
                    raise ValueError("Job requirements missing for search node")
                
                inp = SearchInput(user_id=context.user_id, job_requirements=context.job_requirements)
                out = await match_candidate_data(inp)
                
                # OUTPUT: Update match data
                context.matched_experiences = out.matched_experiences
                context.matched_projects = out.matched_projects
                context.matched_skills = out.matched_skills
                context.gap_skills = out.gap_skills
            
            elif node.type == "template":
                # INPUT: CVDraft (assembled from context)
                if not context.job_requirements:
                    raise ValueError("Job requirements (target role) missing for template node")
                
                draft = CVDraft(
                    target_role=context.job_requirements.job_role,
                    summary="Dynamic summary", # TODO: enhance
                    matched_skills=context.matched_skills,
                    matched_experiences=context.matched_experiences,
                    matched_projects=context.matched_projects,
                    gap_skills=context.gap_skills
                )
                
                inp = TemplateInput(
                    cv_draft=draft,
                    template_id=node.config.get("template_id", "default"),
                    target_role=context.job_requirements.job_role
                )
                out = await render_template(inp)
                
                # OUTPUT: Rendered sections
                context.rendered_sections = out.sections

            elif node.type == "optimizer":
                # INPUT: sections + requirements
                if not context.rendered_sections or not context.job_requirements:
                    raise ValueError("Missing state for optimizer node")
                
                inp = OptimizerInput(
                    job_id=str(workflow_id), 
                    rendered_sections=context.rendered_sections,
                    job_requirements=context.job_requirements
                )
                out = await optimize_cv(inp)
                
                # OUTPUT: Optimized CV
                context.optimized_cv = out.optimized_cv

            elif node.type == "contact":
                # INPUT: optimized_cv + email
                if not context.optimized_cv:
                    raise ValueError("No optimized CV found for contact node")
                
                inp = ContactInput(
                    optimized_cv=context.optimized_cv,
                    recipient_email=context.user_profile.email
                )
                await deliver_cv(inp)
            
            else:
                context.log(f"Warning: Unknown node type '{node.type}'")

        except Exception as e:
            context.log(f"Error in node {node.type}: {str(e)}")
            logger.exception(f"Node execution failed: {node.type}")
            # Depending on policy, we might break or continue
            break

    context.log("Workflow execution finished")
    return context
