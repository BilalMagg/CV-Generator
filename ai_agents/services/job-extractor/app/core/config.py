from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    SERVICE_NAME: str = "job-extractor"
    VERSION: str = "1.0.0"
    JOB_EXTRACTOR_URL: str = "http://localhost:8001"

    BACKEND_BASE_URL: str = "http://localhost:5000"
    GROQ_API_KEY: str | None = None

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")


settings = Settings()