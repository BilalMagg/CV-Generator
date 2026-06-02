from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    SERVICE_NAME: str = "common-tools"
    VERSION: str = "1.0.0"
    COMMON_TOOLS_URL: str = "http://localhost:8006"

    BACKEND_BASE_URL: str = "http://localhost:5000"

    GROQ_API_KEY: str | None = None
    OPENAI_API_KEY: str | None = None
    GOOGLE_API_KEY: str | None = None
    MISTRAL_API_KEY: str | None = None

    model_config = SettingsConfigDict(extra="ignore")


settings = Settings()