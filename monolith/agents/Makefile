.PHONY: venv install run clean

PY     := py
VENV   := .venv
BIN    := $(VENV)/Scripts

venv:
	$(PY) -m venv $(VENV)
	$(BIN)/pip install -e .

install:
	$(BIN)/pip install -e .

run:
	$(BIN)/uvicorn main:app --port 8000 --reload

clean:
	rm -rf $(VENV)
