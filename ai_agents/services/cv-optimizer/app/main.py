import shutil
import os
from fastapi import FastAPI, UploadFile, Form
from fastapi.responses import FileResponse
from app import optimize_CV, OptimizerInput, OptimizerOutput





app = FastAPI()

@app.get("/api/v1/health")
async def health_check():
    return {"status": "healthy", "service": "cv-optimizer"}

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