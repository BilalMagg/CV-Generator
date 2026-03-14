# AI-Powered CV Generation Workflow

This document describes the **agentic workflow** for generating and optimizing CVs automatically. The workflow is implemented in Python using FastAPI and is designed to be modular, scalable, and integrable with a .NET backend.

---

## Overview

The workflow is composed of **5 main agents**, each performing a specific task. The agents operate in sequence where the output of each agent is passed as the input to the next agent. The workflow also relies on **shared core tools** such as PDF reader, text summarizer, and prompt builder.

### Workflow Steps

1. **Job Requirement Extraction**
2. **Data Matching with RAG**
3. **CV Template Filling**
4. **CV Optimization**
5. **CV Delivery via Email**

The workflow is orchestrated by the `cv_generation.py` module and exposed via a single FastAPI endpoint.

---

## Agents Description

### 1. Job Requirement Extraction Agent

- **Purpose**: Extract relevant job requirements and keywords from the job description.
- **Inputs**: 
  - Raw job description (text)
  - Optional metadata (company, role, location)
- **Outputs**:
  - Structured job requirements (skills, qualifications, keywords)
  - Normalized job role and category
- **Tools**:
  - NLP libraries (SpaCy, NLTK, or transformers)
  - Text summarizer
- **Responsibilities**:
  - Identify technical skills, soft skills, and experience levels
  - Normalize synonyms and common abbreviations
  - Prepare data for matching in the next agent

---

### 2. RAG (Retrieval-Augmented Generation) / Data Matching Agent

- **Purpose**: Match extracted job requirements with the user's profile, experience, skills, and previous CV data.
- **Inputs**:
  - Structured job requirements from Agent 1
  - User profile, experience, projects, skills from database
  - Optional external knowledge base (best practices for CVs, ATS rules)
- **Outputs**:
  - Candidate CV sections: recommended experiences, skills, projects for the job
  - Suggestions for missing skills or experience to highlight
- **Tools**:
  - Vector database (FAISS, PGVector, Chroma)
  - Embedding models (OpenAI embeddings, HuggingFace)
  - Prompt builder for LLM generation
- **Responsibilities**:
  - Find best-matching profile information
  - Generate suggestions for missing CV content
  - Provide structured data for template filling

---

### 3. CV Template Filling Agent

- **Purpose**: Fill a CV template using the data provided by Agent 2.
- **Inputs**:
  - Candidate CV sections from Agent 2
  - Selected CV template
- **Outputs**:
  - Fully populated CV content (JSON or internal model)
  - Placeholders filled with text, dates, and achievements
- **Tools**:
  - Template engine (Jinja2, LaTeX generation)
  - PDF generation tools (PDFKit, ReportLab)
- **Responsibilities**:
  - Map structured CV data to template placeholders
  - Ensure proper formatting and section order
  - Prepare the CV for optimization and final output

---

### 4. CV Optimization Agent

- **Purpose**: Validate and optimize the CV before final delivery.
- **Inputs**:
  - Filled CV from Agent 3
  - Rules and constraints (length, formatting, ATS compliance)
- **Outputs**:
  - Optimized CV in final structure
  - Warnings or corrections if some constraints are not met
- **Tools**:
  - Validation rules (JSON schema, regex)
  - Optional AI suggestions for improvements
- **Responsibilities**:
  - Check section lengths, formatting, and consistency
  - Optimize keyword placement for ATS compatibility
  - Return final CV ready for delivery

---

### 5. CV Delivery / Email Sending Agent

- **Purpose**: Send the generated CV to the recipient (email address) or save it to storage.
- **Inputs**:
  - Optimized CV from Agent 4
  - Recipient email address
- **Outputs**:
  - Sent email confirmation / delivery status
  - Optional storage URL for PDF
- **Tools**:
  - Gmail API, SMTP, or other email services
  - PDF attachment handling
- **Responsibilities**:
  - Attach CV as PDF
  - Send email and track delivery success
  - Log sent CVs in the system

---

## Shared Core Tools

All agents share the following utilities:

- **PDF Reader**: Parse existing CVs for analysis
- **Text Summarizer**: Summarize long text content (job description or CV section)
- **Prompt Builder**: Create prompts for LLMs for RAG or optimization tasks
- **Configuration Loader**: Manage API keys, credentials, and environment variables

---

## Workflow Orchestration

The workflow is orchestrated in `cv_generation.py`:

1. Agent 1 extracts job requirements
2. Agent 2 matches data using RAG
3. Agent 3 fills the CV template
4. Agent 4 optimizes the CV
5. Agent 5 delivers the CV via email

The workflow can be called via a single FastAPI endpoint:

```http
POST /agents/generate-cv
```

- **Input**: User ID, job description, template choice
- **Output**: CV PDF, delivery status, optimization notes

## Scalability & Future Extensions

- New agents can be added to the workflow easily
- Shared tools are reusable across agents
- Supports multiple users and multiple workflow requests concurrently
- Can integrate additional AI features like:
- Automated cover letter generation
- Multi-language CV generation
- Integration with external job portals for automatic applications