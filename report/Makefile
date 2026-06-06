LATEX_IMAGE = texlive/texlive:latest
REPORT_DIR = $(CURDIR)

.PHONY: all build build-miktex clean install-miktex

all: build

# === Compilation avec Docker (Windows, Linux, macOS) ===
build:
	docker run --rm -v "$(REPORT_DIR):/workspace" $(LATEX_IMAGE) sh -c "\
		cd /workspace && \
		pdflatex -interaction=nonstopmode main && \
		makeglossaries main && \
		pdflatex -interaction=nonstopmode main && \
		bibtex main && \
		pdflatex -interaction=nonstopmode main && \
		pdflatex -interaction=nonstopmode main"
	@echo ""
	@echo "=== PDF généré : main.pdf ($(shell ls -lh main.pdf | awk '{print $$5}')) ==="

# === Compilation avec MiKTeX local ===
install-miktex:
	choco install miktex -y
	@echo "Redémarrez le terminal, puis faites 'make build-miktex'"

build-miktex:
	pdflatex -interaction=nonstopmode main
	makeglossaries main
	pdflatex -interaction=nonstopmode main
	bibtex main
	pdflatex -interaction=nonstopmode main
	pdflatex -interaction=nonstopmode main
	@echo "=== PDF généré : main.pdf ==="

# === Nettoyage (sauf le PDF) ===
clean:
	rm -f *.aux *.log *.out *.toc *.lof *.lot *.bbl *.blg
	rm -f *.bcf *.run.xml *.synctex.gz *.fls *.fdb_latexmk
	rm -f *.glo *.gls *.glg *.ist *.acn *.acr *.alg
	find . -name '*.aux' -delete
	find . -name '*.log' -delete
	@echo "=== Nettoyage terminé ==="

# === Nettoyage complet (y compris le PDF) ===
distclean: clean
	rm -f main.pdf
	@echo "=== PDF supprimé ==="
