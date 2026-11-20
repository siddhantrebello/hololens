from __future__ import annotations

import io
from typing import Any
from unittest.mock import AsyncMock, patch

import pytest
from fastapi.testclient import TestClient

from app.config import Settings, get_settings
from app.main import app, _jpeg_valid
from app.models import ModelStructuredOutput
from app.openai_service import OpenAiAskAiService, OpenAiServiceError


def _minimal_jpeg() -> bytes:
    # Tiny valid-ish JPEG with SOI/EOI markers for validation tests.
    return bytes([0xFF, 0xD8, 0xFF, 0xD9])


def _valid_jpeg_of_size(size: int) -> bytes:
    # Keep SOI/EOI valid while padding to a target size.
    size = max(size, 4)
    return bytes([0xFF, 0xD8]) + (b"\x00" * (size - 4)) + bytes([0xFF, 0xD9])


@pytest.fixture(autouse=True)
def _clear_settings_cache():
    get_settings.cache_clear()
    yield
    get_settings.cache_clear()


@pytest.fixture
def settings() -> Settings:
    return Settings(
        openai_api_key="test-key-not-real",
        openai_model="gpt-5.6-luna",
        ask_ai_openai_timeout_seconds=35,
        ask_ai_max_image_bytes=5 * 1024 * 1024,
        ask_ai_max_question_chars=2000,
    )


@pytest.fixture
def client(settings: Settings):
    get_settings.cache_clear()
    app.dependency_overrides[get_settings] = lambda: settings
    with TestClient(app) as test_client:
        yield test_client
    app.dependency_overrides.clear()
    get_settings.cache_clear()


def test_health(client: TestClient):
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ok"}


def test_jpeg_valid_helper():
    assert _jpeg_valid(_minimal_jpeg()) is True
    assert _jpeg_valid(b"not-a-jpeg") is False
    assert _jpeg_valid(b"") is False


def test_valid_jpeg_and_question(client: TestClient, settings: Settings):
    model_out = ModelStructuredOutput(
        answer="Dust is visible near the fan intake.",
        confidence=0.8,
        visual_observations=["Dust near intake"],
        recommended_action="Power down before cleaning.",
        safety_warning="Disconnect power first.",
        needs_more_information=False,
    )
    with patch("app.main.OpenAiAskAiService.analyze", new=AsyncMock(return_value=model_out)):
        response = client.post(
            "/api/v1/ask-ai",
            data={
                "question": "What is wrong with this fan?",
                "session_id": "s1",
                "timestamp_utc": "2026-07-31T12:00:00Z",
            },
            files={"image": ("capture.jpg", _minimal_jpeg(), "image/jpeg")},
        )
    assert response.status_code == 200
    body = response.json()
    assert body["success"] is True
    assert body["answer"]
    assert "request_id" in body


def test_missing_image(client: TestClient):
    response = client.post(
        "/api/v1/ask-ai",
        data={"question": "hello", "session_id": "s1"},
    )
    assert response.status_code == 422


def test_empty_question(client: TestClient):
    response = client.post(
        "/api/v1/ask-ai",
        data={"question": "   ", "session_id": "s1"},
        files={"image": ("capture.jpg", _minimal_jpeg(), "image/jpeg")},
    )
    assert response.status_code == 400


def test_wrong_media_type(client: TestClient):
    response = client.post(
        "/api/v1/ask-ai",
        data={"question": "hello", "session_id": "s1"},
        files={"image": ("capture.png", _minimal_jpeg(), "image/png")},
    )
    assert response.status_code == 415
    assert response.json()["success"] is False


def test_oversized_image(settings: Settings):
    settings.ask_ai_max_image_bytes = 8
    get_settings.cache_clear()
    app.dependency_overrides[get_settings] = lambda: settings
    try:
        local = TestClient(app)
        response = local.post(
            "/api/v1/ask-ai",
            data={"question": "hello", "session_id": "s1"},
            files={"image": ("capture.jpg", _valid_jpeg_of_size(32), "image/jpeg")},
        )
    finally:
        app.dependency_overrides.clear()
    assert response.status_code == 413
    assert response.json()["success"] is False


def test_invalid_jpeg_bytes(client: TestClient):
    response = client.post(
        "/api/v1/ask-ai",
        data={"question": "hello", "session_id": "s1"},
        files={"image": ("capture.jpg", b"abcdef", "image/jpeg")},
    )
    assert response.status_code == 400
    assert response.json()["error"]["code"] == "INVALID_IMAGE"


@pytest.mark.asyncio
async def test_upstream_timeout(settings: Settings):
    service = OpenAiAskAiService(settings, client=AsyncMock())
    service._client.responses.create = AsyncMock(
        side_effect=OpenAiServiceError("TIMEOUT", "timeout", 504)
    )
    # Directly raise mapped error path
    with pytest.raises(OpenAiServiceError) as exc:
        raise OpenAiServiceError("TIMEOUT", "The AI response took too long. Please try again.", 504)
    assert exc.value.status_code == 504


def test_rate_limit_mapping(client: TestClient):
    with patch(
        "app.main.OpenAiAskAiService.analyze",
        new=AsyncMock(
            side_effect=OpenAiServiceError(
                "RATE_LIMITED",
                "The AI service is busy. Please try again shortly.",
                429,
            )
        ),
    ):
        response = client.post(
            "/api/v1/ask-ai",
            data={"question": "hello", "session_id": "s1"},
            files={"image": ("capture.jpg", _minimal_jpeg(), "image/jpeg")},
        )
    assert response.status_code == 429
    assert response.json()["success"] is False


def test_auth_failure_mapping(client: TestClient):
    with patch(
        "app.main.OpenAiAskAiService.analyze",
        new=AsyncMock(
            side_effect=OpenAiServiceError(
                "UNAVAILABLE",
                "The AI service is temporarily unavailable.",
                503,
            )
        ),
    ):
        response = client.post(
            "/api/v1/ask-ai",
            data={"question": "hello", "session_id": "s1"},
            files={"image": ("capture.jpg", _minimal_jpeg(), "image/jpeg")},
        )
    assert response.status_code == 503


def test_malformed_structured_output(client: TestClient):
    with patch(
        "app.main.OpenAiAskAiService.analyze",
        new=AsyncMock(
            side_effect=OpenAiServiceError(
                "INVALID_MODEL_OUTPUT",
                "The AI service is temporarily unavailable.",
                502,
            )
        ),
    ):
        response = client.post(
            "/api/v1/ask-ai",
            data={"question": "hello", "session_id": "s1"},
            files={"image": ("capture.jpg", _minimal_jpeg(), "image/jpeg")},
        )
    assert response.status_code == 502


def test_no_file_persistence(tmp_path, client: TestClient):
    before = set(p.name for p in tmp_path.iterdir()) if tmp_path.exists() else set()
    with patch(
        "app.main.OpenAiAskAiService.analyze",
        new=AsyncMock(
            return_value=ModelStructuredOutput(
                answer="ok",
                confidence=0.5,
                visual_observations=[],
                recommended_action=None,
                safety_warning=None,
                needs_more_information=False,
            )
        ),
    ):
        client.post(
            "/api/v1/ask-ai",
            data={"question": "hello", "session_id": "s1"},
            files={"image": ("capture.jpg", _minimal_jpeg(), "image/jpeg")},
        )
    after = set(p.name for p in tmp_path.iterdir()) if tmp_path.exists() else set()
    assert before == after


def test_no_secret_leakage(client: TestClient, settings: Settings):
    with patch(
        "app.main.OpenAiAskAiService.analyze",
        new=AsyncMock(
            side_effect=OpenAiServiceError("UNAVAILABLE", "The AI service is temporarily unavailable.", 503)
        ),
    ):
        response = client.post(
            "/api/v1/ask-ai",
            data={"question": "hello", "session_id": "s1"},
            files={"image": ("capture.jpg", _minimal_jpeg(), "image/jpeg")},
        )
    text = response.text
    assert settings.openai_api_key not in text
    assert "OPENAI_API_KEY" not in text
    assert "sk-" not in text
