from langchain.tools import tool
import os
import re
# ─── Tool 1 : Lire le fichier ──────────────────────────────
@tool
def read_cv_file(file_path: str) -> str:
    """Read the content of a CV file (HTML or LaTeX)."""
    with open(file_path, 'r', encoding='utf-8') as f:
        return f.read()


# ─── Tool 2 : Détecter le format ───────────────────────────
@tool
def detect_format(file_path: str) -> str:
    """Detect the file format from extension and return format rules."""
    extension = os.path.splitext(file_path)[1].lower()

    if extension == ".html":
        return """
        FILE FORMAT : HTML
        FORMAT RULES :
        - Modify text content ONLY
        - Keep ALL HTML tags intact
        - Keep ALL CSS classes and styles intact
        - Keep ALL HTML structure intact
        - Return complete valid HTML
        """
    elif extension == ".tex":
        return """
        FILE FORMAT : LaTeX
        FORMAT RULES :
        - Modify text content ONLY
        - Keep ALL LaTeX commands intact (\\section, \\begin, \\end...)
        - Keep ALL LaTeX structure intact
        - Return complete valid LaTeX
        """
    else:
        raise ValueError(f"Format non supporté : {extension}")
#Tool 3 : Calculer ATS Score
@tool
def calculate_ats_score(cv_content: str, job_data: str) -> str:
    """Calculate ATS score of a CV against a job offer (0-100)."""

    # Nettoyer le contenu HTML/LaTeX pour avoir le texte brut
    clean_cv = re.sub(r'<[^>]+>', ' ', cv_content)
    clean_cv = re.sub(r'\\[a-zA-Z]+\{([^}]*)\}', r'\1', clean_cv)
    clean_cv = clean_cv.lower()
    clean_job = job_data.lower()

    # Extraire les mots clés de l'offre (mots de plus de 4 lettres)
    job_words = set(re.findall(r'\b\w{4,}\b', clean_job))

    # Mots à ignorer
    stop_words = {
        'with', 'that', 'this', 'from', 'have', 'will',
        'your', 'nous', 'vous', 'pour', 'dans', 'avec',
        'sont', 'être', 'tout', 'plus', 'bien', 'notre'
    }
    job_keywords = job_words - stop_words

    # Calculer les mots trouvés dans le CV
    found = {kw for kw in job_keywords if kw in clean_cv}
    missing = job_keywords - found

    # Calculer le score
    if len(job_keywords) == 0:
        score = 0
    else:
        score = round((len(found) / len(job_keywords)) * 100)

    return f"""
    ══════════════════════════════
    ATS SCORE : {score}/100
    ══════════════════════════════
    ✅ Keywords found  : {len(found)}/{len(job_keywords)}
    ❌ Keywords missing: {len(missing)}

    MISSING KEYWORDS:
    {', '.join(sorted(missing)[:20])}
    ══════════════════════════════
    """


@tool
def rewrite_summary(cv_profile: str, job_data: str) -> str:
    """Rewrite the professional summary to match the job offer."""
    return f"""
    Rewrite ONLY the professional summary:
    
    RULES:
    - Keep between 3 to 5 sentences
    - Use keywords from the job offer naturally
    - ONLY mention skills actually present in the CV
    - NEVER invent anything
    
    CV PROFILE: {cv_profile}
    JOB OFFER: {job_data}
    """

@tool
def optimize_skills(cv_skills: str, job_data: str) -> str:
    """Reorder skills by relevance to the job offer."""
    return f"""
    Reorder ONLY the skills section:
    
    RULES:
    - NEVER remove any skill
    - Most relevant first
    - NEVER invent new skills
    
    CV SKILLS: {cv_skills}
    JOB OFFER: {job_data}
    """

@tool
def reorder_projects(cv_projects: str, job_data: str) -> str:
    """Reorder ONLY the projects section by relevance to the job offer."""
    return f"""
    Reorder ONLY the projects section:
    
    RULES:
    - NEVER remove any project
    - Most relevant project first, less relevant last
    - NEVER modify project descriptions or facts
    - NEVER invent new projects
    - NEVER change project names
    
    CV PROJECTS: {cv_projects}
    JOB OFFER: {job_data}
    """

@tool
def reorder_experience(cv_experience: str, job_data: str) -> str:
    """Reorder ONLY the experience section by relevance to the job offer."""
    return f"""
    Reorder ONLY the experience section:
    
    RULES:
    - NEVER remove any experience
    - Most relevant experience first, less relevant last
    - NEVER modify dates, company names, or job titles
    - NEVER invent new experiences
    - Use action verbs aligned with job tone
    
    CV EXPERIENCE: {cv_experience}
    JOB OFFER: {job_data}
    """


@tool
def adapt_tone(cv_content: str, job_data: str) -> str:
    """Adapt the tone to match the company context."""
    return f"""
    Adapt ONLY the tone:
    
    TONE RULES:
    - Startup       → modern, concise, impact-focused
    - Corporate     → formal, structured, professional
    - Public sector → neutral, precise, responsibility-focused
    
    CV CONTENT: {cv_content}
    JOB OFFER: {job_data}
    """


@tool
def optimize_profile(cv_profile: str, job_data: str) -> str:
    """Optimize the professional profile section to match the job offer."""
    return f"""
    Optimize ONLY the professional profile section:
    
    RULES:
    - Keep it concise (3-5 sentences)
    - Highlight keywords from the job offer
    - Maintain the candidate's core identity
    - Use a professional and impactful tone
    
    CV PROFILE: {cv_profile}
    JOB OFFER: {job_data}
    """


tools = [ read_cv_file,detect_format,calculate_ats_score,rewrite_summary, optimize_profile, optimize_skills, reorder_projects, reorder_experience, adapt_tone]
