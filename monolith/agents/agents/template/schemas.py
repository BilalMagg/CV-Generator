from pydantic import BaseModel
from typing import Optional, List


class Contact(BaseModel):
    name: str
    email: str
    relationship: str
    is_primary: bool = False


class LetterRequest(BaseModel):
    user_id: str
    job_title: str
    company_name: str
    job_description: str
    hiring_manager_name: Optional[str] = None
    hiring_manager_email: Optional[str] = None
    letter_type: str = "cover_letter"
    language: str = "English"
    tone: str = "professional"


class LetterResponse(BaseModel):
    letter: str
    letter_type: str
    language: str
    tone: str
    job_title: str
    company_name: str
    file_path: Optional[str] = None
    file_size: Optional[int] = None


class PdfRequest(BaseModel):
    user_id: str
    workflow_id: Optional[str] = None
    cv_data: str
    # "tex"    -> cv_data is already-filled LaTeX source
    # "json"   -> cv_data is a JSON-serialized CvSections object
    cv_data_format: str = "tex"
    template_name: str = "default"
    # Optional template content (overrides the bundled file) — used by the
    # in-app authoring flow so placeholder sections survive into the PDF.
    template_content: Optional[str] = None
    file_name: str = "cv.pdf"
    language: str = "English"
    tone: str = "professional"
    photo_key: Optional[str] = None
    provider: Optional[str] = None
    model: Optional[str] = None


class PdfResponse(BaseModel):
    file_path: str
    file_size: int
    message: str


# ---------------------------------------------------------------------------
# Structured CV content model (the "sections" an LLM builds from raw input)
# ---------------------------------------------------------------------------

class CvHeader(BaseModel):
    name: str = ""
    tagline: str = ""
    location: str = ""
    phone: str = ""
    email: str = ""
    github: str = ""
    linkedin: str = ""


class CvExperience(BaseModel):
    role: str = ""
    company: str = ""
    dates: str = ""
    description: str = ""
    bullets: List[str] = []


class CvEducation(BaseModel):
    title: str = ""
    institution: str = ""
    dates: str = ""


class CvProject(BaseModel):
    name: str = ""
    dates: str = ""
    bullets: List[str] = []


class CvSkillGroup(BaseModel):
    title: str = ""
    items: List[str] = []


class CvHackathon(BaseModel):
    name: str = ""
    event: str = ""
    description: str = ""


class CvLanguage(BaseModel):
    name: str = ""
    level: str = ""


class CvExtracurricular(BaseModel):
    title: str = ""
    role: str = ""
    dates: str = ""
    bullets: List[str] = []


class CvSections(BaseModel):
    header: CvHeader = CvHeader()
    about: str = ""
    experiences: List[CvExperience] = []
    educations: List[CvEducation] = []
    projects: List[CvProject] = []
    skillGroups: List[CvSkillGroup] = []
    hackathons: List[CvHackathon] = []
    languages: List[CvLanguage] = []
    softSkills: List[str] = []
    extracurricular: List[CvExtracurricular] = []
    interests: List[str] = []


class TemplateRenderRequest(BaseModel):
    # user_id is optional: the backend TemplateInput carries it inside cv_draft.
    user_id: str = ""
    workflow_id: Optional[str] = None
    cv_draft: Optional[dict] = None
    template_name: str = "default"
    # Alias: the backend TemplateInput serializes the selected template as
    # template_id, so prefer it over template_name when present.
    template_id: Optional[str] = None
    # When present, the raw template LaTeX source (e.g. a user template edited
    # in-app) overrides the bundled template of the same name. Enables the
    # %=== SECTION PLACEHOLDER === authoring format to drive generation.
    template_content: Optional[str] = None
    target_role: str = ""
    language: str = "English"
    tone: str = "professional"
    provider: Optional[str] = None
    model: Optional[str] = None


class TemplateRenderResponse(BaseModel):
    cv_code: str
    template_id: str
    sections: CvSections
