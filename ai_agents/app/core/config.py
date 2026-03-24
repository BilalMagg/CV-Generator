from pydantic_settings import BaseSettings, SettingsConfigDict

class Settings(BaseSettings):
    PROJECT_NAME: str = "AI CV Generation API"
    VERSION: str = "1.0.0"

    # Backend ASP.NET API
    BACKEND_BASE_URL: str = "http://localhost:5000"

    # API Keys for LLMs
    OPENAI_API_KEY: str | None = None
    GROQ_API_KEY: str | None = None

    # Email Delivery Config
    SMTP_SERVER: str | None = None
    SMTP_PORT: int = 587
    SMTP_USERNAME: str | None = None
    SMTP_PASSWORD: str | None = None

    # Database / Vector DB
    DATABASE_URL: str | None = None

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")

settings = Settings()
