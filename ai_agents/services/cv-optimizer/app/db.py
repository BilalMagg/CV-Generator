# app/database.py

import atexit
import psycopg
from langchain_postgres import PostgresChatMessageHistory
from dotenv import load_dotenv
import os

load_dotenv()

DB_URL = os.getenv("DATABASE_URL")
TABLE_NAME = "chat_history_table"

conn = psycopg.connect(DB_URL)
PostgresChatMessageHistory.create_tables(conn, TABLE_NAME)
atexit.register(conn.close)

def get_session_history(session_id: str):
    history= PostgresChatMessageHistory(
        TABLE_NAME,
        session_id,
        sync_connection=conn
    )

    if len(history.messages) > 2:
        last_two = history.messages[-2:]
        history.clear()
        for msg in last_two:
           history.add_message(msg)
        
    return history