import shutil
import os
import re
import tempfile
# pyrefly: ignore [missing-import]
from fastapi import FastAPI, UploadFile, Form
from fastapi.responses import FileResponse, JSONResponse
from app import optimize_CV, OptimizerInput, OptimizerOutput
from app.tool import calculate_ats_score

app = FastAPI()

DRAFT_CV_HTML = """<!DOCTYPE html><html><body>
<h1>{candidate_name}</h1>
<h2>Professional Summary</h2>
<p>Experienced professional seeking new opportunities.</p>
<h2>Experience</h2>
<p>Details to be optimized based on job requirements.</p>
<h2>Skills</h2>
<ul>{skills}</ul>
<h2>Education</h2>
<p>Relevant qualifications.</p>
</body></html>"""

@app.get("/api/v1/health")
async def health_check():
    return {"status": "healthy", "service": "cv-optimizer"}

@app.post("/score")
async def score_endpoint(
    file: UploadFile,
    job_data: str = Form(...)
):
    # Lire le contenu du fichier (HTML ou LaTeX)
    contents = await file.read()
    cv_content = contents.decode("utf-8")

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

    # Appeler le tool pour avoir le rapport textuel
    report = calculate_ats_score.func(cv_content, job_data)

    return {
        "ats_score": score,
        "keywords_found": sorted(list(found)),
        "keywords_missing": sorted(list(missing)),
        "keywords_found_count": len(found),
        "keywords_missing_count": len(missing),
        "total_keywords_count": len(job_keywords),
        "report": report.strip()
    }

@app.post("/api/v1/optimize")
async def optimize_json_endpoint(input_data: OptimizerInput):
    # Create a minimal draft CV from candidate name
    skills_html = "\n".join(f"<li>{s}</li>" for s in ["Communication", "Problem Solving", "Teamwork"])
    draft = DRAFT_CV_HTML.format(candidate_name=input_data.candidate_name, skills=skills_html)

    with tempfile.NamedTemporaryFile(mode="w", suffix=".html", delete=False) as f:
        f.write(draft)
        temp_path = f.name

    try:
        result_dict = optimize_CV(
            temp_path,
            input_data.job_data,
            input_data.candidate_name,
            input_data.session_id,
            input_data.user_focus
        )
    finally:
        if os.path.exists(temp_path):
            os.unlink(temp_path)

    output = OptimizerOutput(**result_dict)
    return JSONResponse(
        content=output.model_dump(),
        headers={
            "X-ATS-Score-Before": str(output.ats_score_before),
            "X-ATS-Score-After": str(output.ats_score_after),
            "X-Improvement": str(output.improvement)
        }
    )

@app.post("/optimize")
async def optimize_endpoint(
    file: UploadFile,
    job_data: str = Form(...),
    candidate_name: str = Form(),
    session_id: str = Form(),
    user_focus: str = Form(None)
):
    # Sauvegarder le fichier uploadé temporairement
    temp_path = f"temp_{file.filename}"
    with open(temp_path, "wb") as f:
        shutil.copyfileobj(file.file, f)

    # Utilisation correcte du schéma OptimizerInput
    input_data = OptimizerInput(
        job_data=job_data,
        candidate_name=candidate_name,
        session_id=session_id,
        user_focus=user_focus
    )

    # Appel de l'agent
    result_dict = optimize_CV(
        temp_path,
        input_data.job_data,
        input_data.candidate_name,
        input_data.session_id,
        input_data.user_focus
    )

    # Création de l'objet de sortie
    output = OptimizerOutput(**result_dict)
    filename = os.path.basename(output.file_path)

    # On peut retourner soit le fichier, soit le JSON. 
    # Pour l'instant, on retourne le JSON qui contient les scores et le chemin du fichier.
    return FileResponse(
        path=output.file_path,
        filename=filename,
        media_type="application/octet-stream",
        # Optionnel : On peut cacher les scores dans les headers si besoin
        headers={
            "X-ATS-Score-Before": str(output.ats_score_before),
            "X-ATS-Score-After": str(output.ats_score_after),
            "X-Improvement": str(output.improvement)
        })