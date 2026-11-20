from typing import List, Optional

from pydantic import BaseModel, Field


class AskAiErrorBody(BaseModel):
    code: str
    message: str


class AskAiSuccessResponse(BaseModel):
    success: bool = True
    answer: str
    confidence: float = Field(ge=0.0, le=1.0)
    visual_observations: List[str] = Field(default_factory=list)
    recommended_action: Optional[str] = None
    safety_warning: Optional[str] = None
    needs_more_information: bool = False
    request_id: str


class AskAiFailureResponse(BaseModel):
    success: bool = False
    error: AskAiErrorBody
    request_id: str


class ModelStructuredOutput(BaseModel):
    """Schema enforced on the model before wrapping with success/request_id."""

    answer: str
    confidence: float = Field(ge=0.0, le=1.0)
    visual_observations: List[str] = Field(default_factory=list)
    recommended_action: Optional[str] = None
    safety_warning: Optional[str] = None
    needs_more_information: bool = False
