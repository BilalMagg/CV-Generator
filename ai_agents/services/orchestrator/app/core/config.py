from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    SERVICE_NAME: str = "orchestrator"
    VERSION: str = "1.0.0"
    ORCHESTRATOR_URL: str = "http://localhost:8000"

    JOB_EXTRACTOR_URL: str = "http://localhost:8001"
    SEARCH_AGENT_URL: str = "http://localhost:8002"
    TEMPLATE_AGENT_URL: str = "http://localhost:8003"
    CV_OPTIMIZER_URL: str = "http://localhost:8004"
    CONTACT_AGENT_URL: str = "http://localhost:8005"

    BACKEND_BASE_URL: str = "http://localhost:5000"
    GROQ_API_KEY: str | None = None

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")


settings = Settings()