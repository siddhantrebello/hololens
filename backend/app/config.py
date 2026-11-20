from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    openai_api_key: str = ""
    openai_model: str = "gpt-5.6-luna"
    ask_ai_openai_timeout_seconds: float = 35.0
    ask_ai_max_image_bytes: int = 5 * 1024 * 1024
    ask_ai_max_question_chars: int = 2000


@lru_cache
def get_settings() -> Settings:
    return Settings()
