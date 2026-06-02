from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    SERVICE_NAME: str = "job-crawler"
    VERSION: str = "1.0.0"
    JOB_CRAWLER_URL: str = "http://localhost:8006"

    # Kafka
    KAFKA_BOOTSTRAP_SERVERS: str = "kafka:9092"
    CONSUME_TOPIC: str = "trigger-live-crawl"
    PRODUCE_TOPIC: str = "raw-job-urls"
    SUMMARY_TOPIC: str = "crawl-summary"
    KAFKA_GROUP_ID: str = "job-crawler-group"

    # Scraping limits
    MAX_RESULTS_PER_SITE: int = 10  # 10 per site × 2 sites = 20 total max

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")


settings = Settings()
