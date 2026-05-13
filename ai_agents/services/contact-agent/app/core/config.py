from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    SERVICE_NAME: str = "contact-agent"
    VERSION: str = "1.0.0"
    CONTACT_AGENT_URL: str = "http://localhost:8005"

    BACKEND_BASE_URL: str = "http://localhost:5000"
    
    # --- Database Settings (Uncomment when needed) ---
    # DATABASE_URL: str = "postgresql://user:pass@localhost:5432/contact_agent_db"
    # DB_POOL_SIZE: int = 5
    # DB_MAX_OVERFLOW: int = 10
    
    SMTP_SERVER: str | None = None
    SMTP_PORT: int = 587
    SMTP_USERNAME: str | None = Field(None, validation_alias="EMAIL_USER")
    SMTP_PASSWORD: str | None = Field(None, validation_alias="EMAIL_PASS")

    # --- AI Model Settings ---
    # Switch to "mistral-small-latest" when Mistral capacity is available
    AGENT_MODEL: str = "llama-3.1-8b-instant"        # Groq — agent orchestrator
    TOOL_MODEL: str = "llama-3.1-8b-instant"          # Groq — fast tool generation
    LLM_TEMPERATURE: float = 0.0

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")


settings = Settings()