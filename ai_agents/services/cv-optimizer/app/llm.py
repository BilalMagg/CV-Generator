from langchain_mistralai import ChatMistralAI
import os
from dotenv import load_dotenv


load_dotenv()

_llm: ChatMistralAI | None = None


def _get_llm() -> ChatMistralAI:
    global _llm
    if _llm is None:
        _llm = ChatMistralAI(
            model="mistral-small-latest",
            api_key=os.getenv("MISTRAL_API_KEY"),
            temperature=0.3,
        )
    return _llm