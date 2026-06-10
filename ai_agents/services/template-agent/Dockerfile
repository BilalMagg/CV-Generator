FROM python:3.11-slim

WORKDIR /app

# Native OS dependencies for PDF generation:
#   - WeasyPrint (HTML→PDF) needs the Pango/Cairo/GdkPixbuf/GObject shared libs.
#   - pdflatex (LaTeX→PDF) is provided by TeX Live.
# Without these the /render endpoint 500s at the PDF step even though the LLM
# call succeeds (libgobject-2.0-0 not found / 'pdflatex' not found).
RUN apt-get update && apt-get install -y --no-install-recommends \
        libglib2.0-0 \
        libpango-1.0-0 \
        libpangocairo-1.0-0 \
        libpangoft2-1.0-0 \
        libgdk-pixbuf-2.0-0 \
        libcairo2 \
        libffi-dev \
        shared-mime-info \
        fonts-dejavu \
        texlive-latex-base \
        texlive-latex-recommended \
        texlive-latex-extra \
        texlive-fonts-recommended \
    && rm -rf /var/lib/apt/lists/*


COPY services/common-tools services/common-tools
RUN pip install --no-cache-dir -e "./services/common-tools[all]"

COPY services/template-agent/requirements.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

COPY services/common-tools/cvtools/models/ ./app/models/

COPY services/template-agent/app/ ./app/

# Bundle the default CV templates so the service can seed them into MinIO on
# startup (see app/main.py lifespan). Lands at /app/templates.
COPY services/template-agent/templates/ ./templates/

CMD uvicorn app.main:app --host 0.0.0.0 --port ${PORT:-8003}
