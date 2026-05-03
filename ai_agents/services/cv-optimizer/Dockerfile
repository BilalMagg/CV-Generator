FROM python:3.11-slim

WORKDIR /app

# On copie les requirements du service spécifiquement
COPY requirements.txt .

# On copie les outils communs (nécessaires pour l'installation -e)
#COPY services/common-tools ./services/common-tools

RUN pip install --no-cache-dir -r requirements.txt
#RUN pip install --no-cache-dir -e ./services/common-tools

# On copie le code source du service
COPY app/ ./app/

# Créer le dossier output
RUN mkdir -p /app/output && chmod 777 /app/output

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8004"]

