import requests
from dotenv import load_dotenv
from langchain.agents import create_agent
from langchain.tools import tool
from langchain_openai import ChatOpenAI
from groq import Groq
import os

load_dotenv()

# client = Groq(api_key=os.getenv("GROQ_API_KEY"))

client = ChatOpenAI(
    model="openai/gpt-oss-120b",
    base_url="https://api.groq.com/openai/v1",
    api_key=os.getenv("GROQ_API_KEY"),
)

@tool("get_person", description="Return a random person with all its personal informations", return_direct=False)
def get_person():
    response = requests.get("https://randomuser.me/api/")
    return response.json()

@tool("get_weather", description="Return weather information for a given city", return_direct=False)
def get_weather(city: str):
    response = requests.get(f"https://wttr.in/${city}?format=j1")
    return response.json()["current_condition"]

# person = get_person()
# response = client.chat.completions.create(
#     model="openai/gpt-oss-120b",
#     messages=[{
#         "role": "user",
#         "content": f"Give a brief description fro this person ${person}"
#     }]
# )
# print(response.choices[0].message.content)

agent = create_agent(
    model=client,
    tools=[get_person, get_weather],
    system_prompt="You are an assistant that can fetch a random person and describe them."
)

response = agent.invoke({
    "messages": [
        {"role": "user", "content": "Get a random person and describe them briefly"}
    ]
})

print(response["messages"][-1].content)

response = agent.invoke({
    "messages": [
        {"role": "user", "content": "Can you check the weather in tangier"}
    ]
})

print(response["messages"][-1].content)