import logging

from langchain_core.output_parsers import PydanticOutputParser
from langchain_core.prompts import ChatPromptTemplate

from cvtools.core.llm import get_llm
from app.prompt import SYSTEM_PROMPT
from app.schemas import ExtractorInput, ExtractorOutput
from app.tools import fetch_job_offer, fetch_url_content, load_file_content

logger = logging.getLogger(__name__)


async def normalize_input(input_data: ExtractorInput) -> str:
    if input_data.text:
        return input_data.text
    if input_data.file_content and input_data.file_name:
        return await load_file_content(input_data.file_content, input_data.file_name)
    if input_data.url:
        return await fetch_url_content(input_data.url)
    if input_data.job_offer_id:
        return await fetch_job_offer(input_data.job_offer_id)
    raise ValueError("No input provided")


async def extract_job_requirements(input_data: ExtractorInput) -> ExtractorOutput:
    text = await normalize_input(input_data)

    llm = get_llm(model="llama-3.1-8b-instant", temperature=0.0)
    parser = PydanticOutputParser(pydantic_object=ExtractorOutput)

    prompt = ChatPromptTemplate.from_messages([
        ("system", SYSTEM_PROMPT),
        ("human", "Extract information from this job posting:\n\n{text}\n\n{format_instructions}"),
    ])

    chain = prompt | llm | parser

    try:
        result = await chain.ainvoke({
            "text": text,
            "format_instructions": parser.get_format_instructions(),
        })
        return result
    except Exception as e:
        logger.error(f"LLM extraction failed: {e}")
        raise
