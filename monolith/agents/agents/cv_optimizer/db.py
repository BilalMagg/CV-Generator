import psycopg
import json
from datetime import datetime, timezone
from uuid import uuid4, UUID
from typing import Optional, List
from contextlib import contextmanager
from shared.config import settings

DB_URL = settings.DATABASE_URL or "postgresql://postgres:bime@localhost:5432/cv_monolith"


def adapt_uuid(uuid_obj: UUID) -> str:
    return str(uuid_obj)


def adapt_datetime(dt: datetime) -> str:
    return dt.isoformat()


@contextmanager
def get_db_connection():
    conn = psycopg.connect(DB_URL, autocommit=True)
    try:
        yield conn
    finally:
        conn.close()


def create_tables():
    with get_db_connection() as conn:
        with conn.cursor() as cur:
            cur.execute("""
                CREATE TABLE IF NOT EXISTS conversations (
                    id TEXT PRIMARY KEY,
                    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                    title TEXT,
                    thread_id TEXT,
                    user_id TEXT
                );
            """)
            cur.execute("""
                CREATE TABLE IF NOT EXISTS messages (
                    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                    msg_index INTEGER,
                    type TEXT,
                    content TEXT,
                    conversation_id TEXT,
                    user_id TEXT,
                    token_count INTEGER DEFAULT 0,
                    runnable_id TEXT,
                    FOREIGN KEY (conversation_id) REFERENCES conversations(id)
                );
            """)


def save_message(
    conversation_id: str, role: str, content: str, user_id: str,
    token_count: int = 0, msg_index: int = 0,
) -> dict:
    with get_db_connection() as conn:
        with conn.cursor() as cur:
            msg_id = uuid4()
            cur.execute(
                """INSERT INTO messages (id, type, content, conversation_id, user_id, token_count, msg_index, created_at, updated_at)
                   VALUES (%s, %s, %s, %s, %s, %s, %s, NOW(), NOW())""",
                (str(msg_id), role, content, conversation_id, user_id, token_count, msg_index),
            )
            cur.execute("UPDATE conversations SET updated_at = NOW() WHERE id = %s", (conversation_id,))
    return {"id": str(msg_id), "role": role, "content": content}


def get_conversation_messages(conversation_id: str) -> List[dict]:
    with get_db_connection() as conn:
        with conn.cursor() as cur:
            cur.execute(
                "SELECT id, type, content, msg_index FROM messages WHERE conversation_id = %s ORDER BY msg_index",
                (conversation_id,),
            )
            rows = cur.fetchall()
            return [
                {"id": row[0], "role": row[1], "content": row[2], "msg_index": row[3]}
                for row in rows
            ]


def list_conversations(user_id: str) -> List[dict]:
    with get_db_connection() as conn:
        with conn.cursor() as cur:
            cur.execute(
                "SELECT id, title, created_at, updated_at FROM conversations WHERE user_id = %s ORDER BY updated_at DESC",
                (user_id,),
            )
            rows = cur.fetchall()
            return [
                {"id": row[0], "title": row[1], "created_at": row[2], "updated_at": row[3]}
                for row in rows
            ]


def create_conversation(user_id: str, title: str = "CV Review") -> str:
    conv_id = str(uuid4())
    with get_db_connection() as conn:
        with conn.cursor() as cur:
            cur.execute(
                "INSERT INTO conversations (id, title, user_id, created_at, updated_at) VALUES (%s, %s, %s, NOW(), NOW())",
                (conv_id, title, user_id),
            )
    return conv_id
