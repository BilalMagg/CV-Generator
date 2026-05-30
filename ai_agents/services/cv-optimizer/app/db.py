# app/database.py

import psycopg
from langchain_postgres import PostgresChatMessageHistory
from dotenv import load_dotenv
import os

load_dotenv()

TABLE_NAME = "chat_history_table"
DB_URL = os.getenv("DATABASE_URL")

_conn: psycopg.Connection | None = None


def _get_conn() -> psycopg.Connection:
    global _conn
    if _conn is None:
        _conn = psycopg.connect(DB_URL)
        PostgresChatMessageHistory.create_tables(_conn, TABLE_NAME)
    return _conn


def get_session_history(session_id: str):
    conn = _get_conn()
    history = PostgresChatMessageHistory(
        TABLE_NAME,
        session_id,
        sync_connection=conn,
    )

    if len(history.messages) > 2:
        last_two = history.messages[-2:]
        history.clear()
        for msg in last_two:
            history.add_message(msg)

    return history