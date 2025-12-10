-- ======================================================================
-- Migration Name : 002_add_users.sql
-- Description    : Create 'auth.users' table for authentication providers
-- Author         : 나여 빠스
-- Created At     : 2025-11-12
-- Depends On     : 001_init.sql
-- ======================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS auth.users (
    user_id TEXT PRIMARY KEY,
    display_name TEXT NOT NULL,
    provider TEXT NOT NULL,
    created_at TIMESTAMPTZ DEFAULT now(),
    updated_at TIMESTAMPTZ DEFAULT now()
);

COMMENT ON TABLE auth.users IS '사용자 계정 정보 (게스트/구글 공통)';
COMMENT ON COLUMN auth.users.user_id IS 'Firebase or 자체 생성된 유저 식별자';
COMMENT ON COLUMN auth.users.provider IS '로그인 제공자 (guest, google 등)';

COMMIT;
