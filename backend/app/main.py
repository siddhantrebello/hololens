from __future__ import annotations

import uuid
from typing import Optional

from fastapi import Depends, FastAPI, File, Form, HTTPException, UploadFile
from fastapi.responses import JSONResponse

from .config import Settings, get_settings
from .models import AskAiErrorBody, AskAiFailureResponse, AskAiSuccessResponse
from .openai_service import OpenAiAskAiService, OpenAiServiceError

app = FastAPI(title="NORSCOPE Ask AI Backend", version="0.1.0")


def _jpeg_valid(data: bytes) -> bool:
    return (
        len(data) >= 4
        and data[0] == 0xFF
        and data[1] == 0xD8
        and data[-2] == 0xFF
        and data[-1] == 0xD9
    )


def _failure(code: str, message: str, status_code: int, request_id: str) -> JSONResponse:
    body = AskAiFailureResponse(
        success=False,
        error=AskAiErrorBody(code=code, message=message),
        request_id=request_id,
    )
    return JSONResponse(status_code=status_code, content=body.model_dump())


@app.get("/health")
async def health() -> dict:
    return {"status": "ok"}


@app.post("/api/v1/ask-ai")
async def ask_ai(
    question: str = Form(...),
    image: UploadFile = File(...),
    session_id: str = Form(...),
    timestamp_utc: Optional[str] = Form(None),
    task_context: Optional[str] = Form(None),
    settings: Settings = Depends(get_settings),
):
    # Keep multipart metadata accepted; do not persist or log content.
    _ = (session_id, timestamp_utc, task_context)
    request_id = str(uuid.uuid4())

    question_clean = (question or "").strip()
    if not question_clean:
        raise HTTPException(status_code=400, detail="question is required")
    if len(question_clean) > settings.ask_ai_max_question_chars:
        raise HTTPException(status_code=400, detail="question exceeds maximum length")

    content_type = (image.content_type or "").split(";")[0].strip().lower()
    if content_type not in ("image/jpeg", "image/jpg"):
        return _failure(
            "UNSUPPORTED_MEDIA_TYPE",
            "The captured image could not be analyzed. Please capture it again.",
            415,
            request_id,
        )

    jpeg_bytes = await image.read()
    # Do not persist image bytes to disk.
    if not jpeg_bytes:
        return _failure(
            "MISSING_IMAGE",
            "The captured image could not be analyzed. Please capture it again.",
            400,
            request_id,
        )
    if len(jpeg_bytes) > settings.ask_ai_max_image_bytes:
        return _failure(
            "IMAGE_TOO_LARGE",
            "The captured image could not be analyzed. Please capture it again.",
            413,
            request_id,
        )
    if not _jpeg_valid(jpeg_bytes):
        return _failure(
            "INVALID_IMAGE",
            "The captured image could not be analyzed. Please capture it again.",
            400,
            request_id,
        )

    service = OpenAiAskAiService(settings)
    try:
        model_out = await service.analyze(question_clean, jpeg_bytes)
    except OpenAiServiceError as exc:
        return _failure(exc.code, exc.message, exc.status_code, request_id)

    success = AskAiSuccessResponse(
        success=True,
        answer=model_out.answer,
        confidence=model_out.confidence,
        visual_observations=model_out.visual_observations,
        recommended_action=model_out.recommended_action,
        safety_warning=model_out.safety_warning,
        needs_more_information=model_out.needs_more_information,
        request_id=request_id,
    )
    return JSONResponse(status_code=200, content=success.model_dump())
