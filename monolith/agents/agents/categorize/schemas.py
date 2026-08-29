from pydantic import BaseModel
from typing import List, Optional


class CandidateNode(BaseModel):
    id: str
    name: str
    keywords: List[str] = []


class CategorizeRequest(BaseModel):
    scope: str
    text: str
    candidates: List[CandidateNode] = []
    provider: Optional[str] = None
    model: Optional[str] = None


class CategorizeResponse(BaseModel):
    node_ids: List[str] = []
