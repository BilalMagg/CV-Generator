import os
import re
import atexit
import psycopg

from langchain_postgres import PostgresChatMessageHistory
from langchain_core.runnables.history import RunnableWithMessageHistory
from langgraph.prebuilt import create_react_agent
from .tool import tools
from .prompt import prompt
from .llm import llm
from .db import get_session_history




agent = create_react_agent(llm, tools, prompt=prompt)


agent_with_memory=RunnableWithMessageHistory(
    agent,
    get_session_history,
    input_messages_key="input",
    history_messages_key="chat_history"

)


def optimize_CV(file_path: str, job_data: str, candidate_name: str, session_id: str, user_focus: str = None):
    os.makedirs("output", exist_ok=True)

    # Récupérer l'extension du fichier
    extension = os.path.splitext(file_path)[1].lower()
    if extension not in [".html", ".tex"]:
        raise ValueError(f"Format non supporté : {extension}")
  

    focus_text = f"\n    USER FOCUS/SPECIFIC INSTRUCTIONS: {user_focus}" if user_focus else ""

    user_prompt = f"""
    CV FILE PATH : {file_path}
    JOB OFFER    : {job_data}{focus_text}

    Please proceed with the full optimization by following the workflow defined in your system instructions
    (Analysis, ATS Scoring, Optimization, and Final Scoring).
    """

    config = {"configurable": {"session_id": session_id}}
    result = agent_with_memory.invoke({"input": user_prompt}, config=config)
    output = result["messages"][-1].content

    # Parsing des scores ATS (depuis le format défini dans prompt.py)
    before_match = re.search(r"ATS SCORE BEFORE\s*:\s*(\d+)", output)
    after_match = re.search(r"ATS SCORE AFTER\s*:\s*(\d+)", output)
    improvement_match = re.search(r"IMPROVEMENT\s*:\s*\+?(-?\d+)", output)

    # Extraction du CV optimisé (entre les balises [OPTIMIZED CV] et [END CV])
    cv_match = re.search(r"\[OPTIMIZED CV\](.*)\[END CV\]", output, re.DOTALL)
    
    if cv_match:
        clean_output = cv_match.group(1).strip()
    else:
        # Fallback si les balises sont absentes
        clean_output = output.strip()

    # Supprimer les blocs de code Markdown (ex: ```latex ... ``` ou ``` ... ```)
    if "```" in clean_output:
        # Recherche d'un bloc de code avec ou sans nom de langage
        code_block_match = re.search(r"```(?:\w+)?\n?(.*?)\n?```", clean_output, re.DOTALL)
        if code_block_match:
            clean_output = code_block_match.group(1).strip()
        else:
            # Nettoyage manuel si le regex échoue (cas rares)
            clean_output = re.sub(r"```(?:\w+)?", "", clean_output).replace("```", "").strip()

    clean_output = re.sub(r'\*\*(.*?)\*\*', r'\1', clean_output)

    # Sauvegarde du fichier
    final_path = f"output/cv_{candidate_name}{extension}"
    with open(final_path, "w", encoding="utf-8") as f:
        f.write(clean_output)

    # Préparation des données de sortie
    score_before = int(before_match.group(1)) if before_match else 0
    score_after = int(after_match.group(1)) if after_match else 0
    improvement = int(improvement_match.group(1)) if improvement_match else (score_after - score_before)

    return {
        "ats_score_before": score_before,
        "ats_score_after": score_after,
        "improvement": improvement,
        "file_path": final_path
    }
