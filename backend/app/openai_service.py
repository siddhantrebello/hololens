from __future__ import annotations

import base64
import json
from typing import Any, Optional

from openai import APIConnectionError, APIStatusError, APITimeoutError, AsyncOpenAI, RateLimitError

from .config import Settings
from .models import ModelStructuredOutput
from .prompts import DEVELOPER_PROMPT


STRUCTURED_SCHEMA: dict[str, Any] = {
    "type": "object",
    "additionalProperties": False,
    "properties": {
        "answer": {"type": "string"},
        "confidence": {"type": "number", "minimum": 0, "maximum": 1},
        "visual_observations": {
            "type": "array",
            "items": {"type": "string"},
        },
        "recommended_action": {"type": ["string", "null"]},
        "safety_warning": {"type": ["string", "null"]},
        "needs_more_information": {"type": "boolean"},
    },
    "required": [
        "answer",
        "confidence",
        "visual_observations",
        "recommended_action",
        "safety_warning",
        "needs_more_information",
    ],
}


class OpenAiServiceError(Exception):
    def __init__(self, code: str, message: str, status_code: int = 502):
        super().__init__(message)
        self.code = code
        self.message = message
        self.status_code = status_code


class OpenAiAskAiService:
    def __init__(self, settings: Settings, client: Optional[AsyncOpenAI] = None):
        self._settings = settings
        self._client = client

    def _get_client(self) -> AsyncOpenAI:
        if self._client is not None:
            return self._client
        if not self._settings.openai_api_key:
            raise OpenAiServiceError(
                "UNAVAILABLE",
                "The AI service is temporarily unavailable.",
                status_code=503,
            )
        # Do not log the key or client configuration.
        return AsyncOpenAI(
            api_key=self._settings.openai_api_key,
            timeout=self._settings.ask_ai_openai_timeout_seconds,
        )

    async def analyze(self, question: str, jpeg_bytes: bytes) -> ModelStructuredOutput:
        client = self._get_client()
        image_b64 = base64.b64encode(jpeg_bytes).decode("ascii")
        data_url = f"data:image/jpeg;base64,{image_b64}"

        try:
            response = await client.responses.create(
                model=self._settings.openai_model,
                input=[
                    {
                        "role": "developer",
                        "content": [{"type": "input_text", "text": DEVELOPER_PROMPT}],
                    },
                    {
                        "role": "user",
                        "content": [
                            {"type": "input_text", "text": question},
                            {"type": "input_image", "image_url": data_url},
                        ],
                    },
                ],
                text={
                    "format": {
                        "type": "json_schema",
                        "name": "norscope_ask_ai",
                        "strict": True,
                        "schema": STRUCTURED_SCHEMA,
                    }
                },
            )
        except APITimeoutError as exc:
            raise OpenAiServiceError(
                "TIMEOUT",
                "The AI response took too long. Please try again.",
                status_code=504,
            ) from exc
        except RateLimitError as exc:
            raise OpenAiServiceError(
                "RATE_LIMITED",
                "The AI service is busy. Please try again shortly.",
                status_code=429,
            ) from exc
        except APIConnectionError as exc:
            raise OpenAiServiceError(
                "UNAVAILABLE",
                "The AI service is temporarily unavailable.",
                status_code=503,
            ) from exc
        except APIStatusError as exc:
            if exc.status_code in (401, 403):
                raise OpenAiServiceError(
                    "UNAVAILABLE",
                    "The AI service is temporarily unavailable.",
                    status_code=503,
                ) from exc
            if exc.status_code == 429:
                raise OpenAiServiceError(
                    "RATE_LIMITED",
                    "The AI service is busy. Please try again shortly.",
                    status_code=429,
                ) from exc
            raise OpenAiServiceError(
                "MODEL_UNAVAILABLE",
                "The AI service is temporarily unavailable.",
                status_code=502,
            ) from exc
        except Exception as exc:
            raise OpenAiServiceError(
                "MODEL_UNAVAILABLE",
                "The AI service is temporarily unavailable.",
                status_code=502,
            ) from exc

        raw_text = getattr(response, "output_text", None)
        if not raw_text:
            raw_text = _extract_output_text(response)
        if not raw_text:
            raise OpenAiServiceError(
                "INVALID_MODEL_OUTPUT",
                "The AI service is temporarily unavailable.",
                status_code=502,
            )

        try:
            payload = json.loads(raw_text)
            return ModelStructuredOutput.model_validate(payload)
        except Exception as exc:
            raise OpenAiServiceError(
                "INVALID_MODEL_OUTPUT",
                "The AI service is temporarily unavailable.",
                status_code=502,
            ) from exc


def _extract_output_text(response: Any) -> str:
    try:
        chunks: list[str] = []
        for item in getattr(response, "output", None) or []:
            for content in getattr(item, "content", None) or []:
                text = getattr(content, "text", None)
                if text:
                    chunks.append(text)
        return "".join(chunks).strip()
    except Exception:
        return ""
