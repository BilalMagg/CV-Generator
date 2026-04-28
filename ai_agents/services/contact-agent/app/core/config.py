from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    SERVICE_NAME: str = "contact-agent"
    VERSION: str = "1.0.0"
    CONTACT_AGENT_URL: str = "http://localhost:8005"

    BACKEND_BASE_URL: str = "http://localhost:5000"
    SMTP_SERVER: str | None = None
    SMTP_PORT: int = 587
    SMTP_USERNAME: str | None = None
    SMTP_PASSWORD: str | None = None

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")


settings = Settings()