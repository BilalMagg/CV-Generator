"""
BIME tools — call the .NET backend API to query/act on the user's data.
All tools are LangChain @tool-decorated async functions.
"""
from langchain_core.tools import tool
import httpx
from shared.config import settings


def _get_backend_client(user_id: str) -> httpx.AsyncClient:
    return httpx.AsyncClient(
        base_url=settings.BACKEND_BASE_URL,
        timeout=20.0,
        headers={"X-User-Id": user_id, "Content-Type": "application/json"},
    )


@tool
async def get_user_profile(user_id: str) -> str:
    """Get the current user's profile (name, email, completeness)."""
    async with _get_backend_client(user_id) as client:
        resp = await client.get(f"/api/users/{user_id}")
        if resp.status_code != 200:
            return "Could not retrieve user profile."
        data = resp.json().get("data", {})
        name = f"{data.get('firstName', '')} {data.get('lastName', '')}".strip() or data.get("username", "Unknown")
        return f"User: {name}\nEmail: {data.get('email', 'N/A')}\nProfile complete: {data.get('isProfileComplete', False)}"


@tool
async def list_applications(user_id: str, status: str = "", limit: int = 20) -> str:
    """List the user's job applications. Optionally filter by status (SAVED, APPLIED, SCREENING, INTERVIEW, OFFER, ACCEPTED, REJECTED, WITHDRAWN)."""
    async with _get_backend_client(user_id) as client:
        params = {"limit": limit}
        if status:
            params["status"] = status
        resp = await client.get("/api/applications", params=params)
        if resp.status_code != 200:
            return "Could not retrieve applications."
        body = resp.json().get("data", {})
        items = body.get("items", body) if isinstance(body, dict) else body
        if not items:
            return "No applications found."
        lines = []
        for a in items[:limit]:
            lines.append(f"- [{a.get('id', 'N/A')}] {a.get('positionTitle', a.get('position', 'N/A'))} at {a.get('companyName', 'N/A')} [{a.get('status', '?')}]")
        return "\n".join(lines)


@tool
async def get_application_detail(user_id: str, application_id: str) -> str:
    """Get full details of a specific application by its ID."""
    async with _get_backend_client(user_id) as client:
        resp = await client.get(f"/api/applications/{application_id}")
        if resp.status_code != 200:
            return "Application not found."
        a = resp.json().get("data", {})
        parts = [
            f"Position: {a.get('positionTitle', a.get('position', 'N/A'))}",
            f"Company: {a.get('companyName', 'N/A')}",
            f"Status: {a.get('status', 'N/A')}",
            f"Applied: {a.get('appliedAt', 'Not yet')}",
            f"Notes: {a.get('notes', '')}",
        ]
        return "\n".join(parts)


@tool
async def list_companies(user_id: str, search: str = "") -> str:
    """List companies the user has interacted with. Optionally search by name."""
    async with _get_backend_client(user_id) as client:
        params = {}
        if search:
            params["search"] = search
        resp = await client.get("/api/companies", params=params)
        if resp.status_code != 200:
            return "Could not retrieve companies."
        body = resp.json().get("data", {})
        items = body.get("items", body) if isinstance(body, dict) else body
        if not items:
            return "No companies found."
        lines = [f"- {c.get('name', 'N/A')} ({c.get('applicationsCount', 0)} applications)" for c in items[:20]]
        return "\n".join(lines)


@tool
async def list_contacts(user_id: str, search: str = "") -> str:
    """List the user's contacts. Optionally search by name or company."""
    async with _get_backend_client(user_id) as client:
        params = {}
        if search:
            params["search"] = search
        resp = await client.get("/api/contacts", params=params)
        if resp.status_code != 200:
            return "Could not retrieve contacts."
        body = resp.json().get("data", {})
        items = body.get("items", body) if isinstance(body, dict) else body
        if not items:
            return "No contacts found."
        lines = [f"- {c.get('fullName', c.get('name', 'N/A'))} ({c.get('companyName', c.get('company', 'N/A'))})" for c in items[:20]]
        return "\n".join(lines)


@tool
async def create_application(user_id: str, company_name: str, position: str, notes: str = "") -> str:
    """Create a new application entry (SAVED status). Use when the user wants to track a new opportunity."""
    async with _get_backend_client(user_id) as client:
        resp = await client.post("/api/applications", json={
            "companyName": company_name, "position": position,
            "notes": notes, "status": "SAVED",
        })
        if resp.status_code in (200, 201):
            data = resp.json().get("data", {})
            return f"Application created: {position} at {company_name} (ID: {data.get('id', '?')})"
        return f"Failed to create application: {resp.text}"


@tool
async def update_application_status(user_id: str, application_id: str, new_status: str) -> str:
    """Update an application's status. Valid statuses: SAVED, APPLIED, SCREENING, INTERVIEW, OFFER, ACCEPTED, REJECTED, WITHDRAWN."""
    async with _get_backend_client(user_id) as client:
        resp = await client.patch(f"/api/applications/{application_id}", json={"status": new_status})
        if resp.status_code == 200:
            return f"Application updated to {new_status}."
        return f"Failed to update: {resp.text}"


@tool
async def get_application_stats(user_id: str) -> str:
    """Get summary statistics of all applications (counts by status)."""
    async with _get_backend_client(user_id) as client:
        resp = await client.get("/api/applications")
        if resp.status_code != 200:
            return "Could not retrieve stats."
        body = resp.json().get("data", {})
        items = body.get("items", body) if isinstance(body, dict) else body
        counts: dict[str, int] = {}
        if isinstance(items, list):
            for a in items:
                s = a.get("status", "UNKNOWN")
                counts[s] = counts.get(s, 0) + 1
        lines = [f"{k}: {v}" for k, v in sorted(counts.items())]
        total = sum(counts.values())
        return f"Total: {total}\n" + "\n".join(lines)


@tool
async def list_schedules(user_id: str) -> str:
    """List the user's email schedules."""
    async with _get_backend_client(user_id) as client:
        resp = await client.get("/api/email-schedules")
        if resp.status_code != 200:
            return "Could not retrieve schedules."
        body = resp.json().get("data", {})
        items = body.get("items", body) if isinstance(body, dict) else body
        if not items or not isinstance(items, list):
            return "No schedules found."
        lines = [f"- {s.get('name', 'N/A')} (cadence: {s.get('cadence', '?')}, active: {s.get('isActive', False)})" for s in items[:10]]
        return "\n".join(lines)


@tool
async def get_entity_stats(user_id: str) -> str:
    """Get counts of all entities: applications, companies, contacts, and schedules. Use this when the user asks 'how many X I have' or for a general overview."""
    async with _get_backend_client(user_id) as client:
        parts = []

        r = await client.get("/api/applications", params={"limit": 1})
        if r.status_code == 200:
            total = r.json().get("data", {}).get("total", 0)
            parts.append(f"Applications: {total}")

        r = await client.get("/api/companies", params={"limit": 1})
        if r.status_code == 200:
            total = r.json().get("data", {}).get("total", 0)
            parts.append(f"Companies: {total}")

        r = await client.get("/api/contacts", params={"limit": 1})
        if r.status_code == 200:
            total = r.json().get("data", {}).get("total", 0)
            parts.append(f"Contacts: {total}")

        r = await client.get("/api/email-schedules", params={"limit": 1})
        if r.status_code == 200:
            data = r.json().get("data", {})
            items = data.get("items", data) if isinstance(data, dict) else data
            total = len(items) if isinstance(items, list) else 0
            parts.append(f"Schedules: {total}")

        return "\n".join(parts) if parts else "Could not retrieve stats."


# ── Profile management tools ──────────────────────────────────────────

@tool
async def list_experiences(user_id: str) -> str:
    """List the user's work experiences."""
    async with _get_backend_client(user_id) as client:
        resp = await client.get("/api/experiences", params={"userId": user_id})
        if resp.status_code != 200:
            return "Could not retrieve experiences."
        items = resp.json().get("data", [])
        if not items:
            return "No experiences found."
        lines = []
        for e in items:
            end = e.get("endDate") or "Present"
            lines.append(f"- [{e.get('id', 'N/A')}] {e.get('title', 'N/A')} at {e.get('company', 'N/A')} ({e.get('startDate', '?')} → {end})")
        return "\n".join(lines)


@tool
async def create_experience(user_id: str, title: str, company: str, description: str = "", start_date: str = "", end_date: str = "", status: str = "COMPLETED") -> str:
    """Add a new work experience to the user's profile. status: COMPLETED or IN_PROGRESS."""
    async with _get_backend_client(user_id) as client:
        payload = {
            "title": title,
            "company": company,
            "description": description,
            "startDate": start_date or "2025-01-01T00:00:00Z",
            "endDate": end_date if end_date else None,
            "status": status,
            "userId": user_id,
        }
        resp = await client.post("/api/experiences", json=payload)
        if resp.status_code in (200, 201):
            data = resp.json().get("data", {})
            return f"Experience added: {title} at {company} (ID: {data.get('id', 'N/A')})"
        return f"Failed to add experience: {resp.text}"


@tool
async def list_projects(user_id: str) -> str:
    """List the user's projects."""
    async with _get_backend_client(user_id) as client:
        resp = await client.get("/api/projects", params={"userId": user_id})
        if resp.status_code != 200:
            return "Could not retrieve projects."
        items = resp.json().get("data", [])
        if not items:
            return "No projects found."
        lines = []
        for p in items:
            end = p.get("endDate") or "Ongoing"
            lines.append(f"- [{p.get('id', 'N/A')}] {p.get('title', 'N/A')} ({p.get('startDate', '?')} → {end}) | {p.get('status', '?')}")
        return "\n".join(lines)


@tool
async def create_project(user_id: str, title: str, description: str = "", role: str = "", start_date: str = "", end_date: str = "", status: str = "COMPLETED", repo_url: str = "", demo_url: str = "") -> str:
    """Add a new project to the user's profile."""
    async with _get_backend_client(user_id) as client:
        payload = {
            "title": title,
            "description": description,
            "role": role,
            "startDate": start_date or "2025-01-01T00:00:00Z",
            "endDate": end_date if end_date else None,
            "repositoryUrl": repo_url or None,
            "demoUrl": demo_url or None,
            "status": status,
            "userId": user_id,
        }
        resp = await client.post("/api/projects", json=payload)
        if resp.status_code in (200, 201):
            data = resp.json().get("data", {})
            return f"Project added: {title} (ID: {data.get('id', 'N/A')})"
        return f"Failed to add project: {resp.text}"


@tool
async def list_skills(user_id: str) -> str:
    """List the user's skills."""
    async with _get_backend_client(user_id) as client:
        resp = await client.get("/api/skills", params={"userId": user_id})
        if resp.status_code != 200:
            return "Could not retrieve skills."
        items = resp.json().get("data", [])
        if not items:
            return "No skills found."
        lines = []
        for s in items:
            lvl = s.get("level") or "N/A"
            yrs = s.get("yearsOfExperience")
            yrs_str = f", {yrs}y exp" if yrs else ""
            lines.append(f"- [{s.get('id', 'N/A')}] {s.get('name', 'N/A')} (level: {lvl}{yrs_str})")
        return "\n".join(lines)


@tool
async def create_skill(user_id: str, name: str, level: str = "", years_of_experience: int = 0, category: str = "") -> str:
    """Add a new skill to the user's profile. Level examples: Beginner, Intermediate, Advanced, Expert."""
    async with _get_backend_client(user_id) as client:
        payload = {
            "name": name,
            "level": level or None,
            "yearsOfExperience": years_of_experience or None,
            "userId": user_id,
            "category": category or None,
        }
        resp = await client.post("/api/skills", json=payload)
        if resp.status_code in (200, 201):
            data = resp.json().get("data", {})
            return f"Skill added: {name} (ID: {data.get('id', 'N/A')})"
        return f"Failed to add skill: {resp.text}"


@tool
async def update_skill(user_id: str, skill_id: str, name: str = "", level: str = "", years_of_experience: int = 0, category: str = "") -> str:
    """Update an existing skill. Only pass fields you want to change — others keep their current value."""
    async with _get_backend_client(user_id) as client:
        cur = await client.get(f"/api/skills/{skill_id}")
        if cur.status_code != 200:
            return f"Skill {skill_id} not found."
        old = cur.json().get("data", {})
        payload = {
            "name": name or old.get("name", ""),
            "level": level or old.get("level"),
            "yearsOfExperience": years_of_experience or old.get("yearsOfExperience"),
            "category": category or old.get("category"),
        }
        resp = await client.put(f"/api/skills/{skill_id}", json=payload)
        if resp.status_code == 200:
            return "Skill updated."
        return f"Failed to update skill: {resp.text}"


@tool
async def delete_skill(user_id: str, skill_id: str) -> str:
    """Delete a skill by its ID."""
    async with _get_backend_client(user_id) as client:
        resp = await client.delete(f"/api/skills/{skill_id}")
        if resp.status_code in (200, 204):
            return "Skill deleted."
        return f"Failed to delete skill: {resp.text}"


@tool
async def update_experience(user_id: str, experience_id: str, title: str = "", company: str = "", description: str = "", start_date: str = "", end_date: str = "", status: str = "") -> str:
    """Update an existing experience. Only pass fields you want to change — others keep their current value."""
    async with _get_backend_client(user_id) as client:
        cur = await client.get(f"/api/experiences/{experience_id}")
        if cur.status_code != 200:
            return f"Experience {experience_id} not found."
        old = cur.json().get("data", {})
        payload = {
            "title": title or old.get("title", ""),
            "company": company or old.get("company"),
            "description": description or old.get("description"),
            "startDate": start_date or old.get("startDate", ""),
            "endDate": end_date if end_date != "" else old.get("endDate"),
            "status": status or old.get("status", ""),
        }
        resp = await client.put(f"/api/experiences/{experience_id}", json=payload)
        if resp.status_code == 200:
            return "Experience updated."
        return f"Failed to update experience: {resp.text}"


@tool
async def delete_experience(user_id: str, experience_id: str) -> str:
    """Delete an experience by its ID."""
    async with _get_backend_client(user_id) as client:
        resp = await client.delete(f"/api/experiences/{experience_id}")
        if resp.status_code in (200, 204):
            return "Experience deleted."
        return f"Failed to delete experience: {resp.text}"


@tool
async def update_project(user_id: str, project_id: str, title: str = "", description: str = "", role: str = "", start_date: str = "", end_date: str = "", status: str = "", repo_url: str = "", demo_url: str = "") -> str:
    """Update an existing project. Only pass fields you want to change — others keep their current value."""
    async with _get_backend_client(user_id) as client:
        cur = await client.get(f"/api/projects/{project_id}")
        if cur.status_code != 200:
            return f"Project {project_id} not found."
        old = cur.json().get("data", {})
        payload = {
            "title": title or old.get("title", ""),
            "description": description or old.get("description"),
            "role": role or old.get("role"),
            "startDate": start_date or old.get("startDate", ""),
            "endDate": end_date if end_date != "" else old.get("endDate"),
            "status": status or old.get("status", ""),
            "repositoryUrl": repo_url if repo_url != "" else old.get("repositoryUrl"),
            "demoUrl": demo_url if demo_url != "" else old.get("demoUrl"),
        }
        resp = await client.put(f"/api/projects/{project_id}", json=payload)
        if resp.status_code == 200:
            return "Project updated."
        return f"Failed to update project: {resp.text}"


@tool
async def delete_project(user_id: str, project_id: str) -> str:
    """Delete a project by its ID."""
    async with _get_backend_client(user_id) as client:
        resp = await client.delete(f"/api/projects/{project_id}")
        if resp.status_code in (200, 204):
            return "Project deleted."
        return f"Failed to delete project: {resp.text}"


# ── Search tool ─────────────────────────────────────────────────────

VALID_SOURCE_TYPES = [
    "User", "CVProfile", "Experience", "Project", "Skill",
    "Education", "Certification", "Language", "Interest",
    "Hackathon", "AcademicActivity", "SocialLink",
    "Company", "Contact", "Application"
]


@tool
async def search_profile(user_id: str, query: str, entity_type: str | list[str], limit: int = 10) -> str:
    """Search the user's profile using natural language. entity_type MUST be provided — either a single type or a list of types.
    Valid entity_type values: Experience, Project, Skill, Education, Certification, Language, Interest, Hackathon, AcademicActivity, SocialLink, Company, Contact, Application, CVProfile, User.
    Use this when the user asks about something specific in their profile (e.g., 'python projects', 'devops experience', 'what certifications do I have').
    For broad questions, pass multiple entity types as a list (e.g., entity_type=["Experience", "Project", "Skill"]).
    """
    if isinstance(entity_type, str):
        entity_type = [entity_type]

    invalid = [t for t in entity_type if t not in VALID_SOURCE_TYPES]
    if invalid:
        return f"Invalid entity types: {', '.join(invalid)}. Valid types: {', '.join(VALID_SOURCE_TYPES)}"

    async with _get_backend_client(user_id) as client:
        payload = {"query": query, "sourceTypes": entity_type, "limit": limit}
        resp = await client.post("/api/search", json=payload)
        if resp.status_code != 200:
            return f"Search failed: {resp.text}"
        data = resp.json().get("data", {})
        items = data if isinstance(data, list) else data.get("items", []) if isinstance(data, dict) else []
        if not items:
            return f"No results found for '{query}' in {', '.join(entity_type)}."
        lines = []
        for r in items[:limit]:
            score = r.get("score", 0)
            lines.append(f"- [{r.get('sourceType', '?')}] {r.get('content', 'N/A')[:200]} (score: {score:.2f})")
        return f"Found {len(items)} results:\n" + "\n".join(lines)
