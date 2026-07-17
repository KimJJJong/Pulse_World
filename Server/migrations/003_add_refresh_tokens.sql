-- ======================================================================
-- Migration Name : 003_add_refresh_tokens.sql
-- Description    : Create 'auth.user_refresh_tokens' for JWT RefreshToken rotation
-- Author         : 해당 헤더 필요한감?
-- Created At     : 2025-11-12
-- Depends On     : 002_add_users.sql
-- ======================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS auth.user_refresh_tokens (
    id BIGSERIAL PRIMARY KEY,
    user_id TEXT NOT NULL REFERENCES auth.users(user_id) ON DELETE CASCADE,
    token_hash BYTEA NOT NULL,
    salt BYTEA NOT NULL,
    family_id UUID NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    revoked_at TIMESTAMPTZ,
    revoked_reason TEXT,
    ip inet,
    user_agent TEXT,
    created_at TIMESTAMPTZ DEFAULT now()
);

COMMENT ON TABLE auth.user_refresh_tokens IS 'Refresh Token 로테이션 관리 테이블';
COMMENT ON COLUMN auth.user_refresh_tokens.family_id IS 'Refresh Token Family (로그인 세션 그룹)';
COMMENT ON COLUMN auth.user_refresh_tokens.revoked_reason IS '폐기 사유 (rotated, manual 등)';

COMMIT;
