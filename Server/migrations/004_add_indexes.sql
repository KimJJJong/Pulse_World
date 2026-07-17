-- ======================================================================
-- Migration Name : 004_add_indexes.sql
-- Description    : Add useful indexes to improve query performance
-- Author         : 
-- Created At     : 2025-11-12
-- Depends On     : 003_add_refresh_tokens.sql
-- ======================================================================

BEGIN;

-- user_id 기반 토큰 조회 성능 향상
CREATE INDEX IF NOT EXISTS idx_user_refresh_tokens_userid
    ON auth.user_refresh_tokens (user_id);

-- family_id 기반 폐기 처리 속도 향상 : 필 없을듯 한데 현재 스키가 땜시 유지
CREATE INDEX IF NOT EXISTS idx_user_refresh_tokens_family
    ON auth.user_refresh_tokens (family_id);

-- 만료일 기준 정리 쿼리용 인덱스
CREATE INDEX IF NOT EXISTS idx_user_refresh_tokens_expires
    ON auth.user_refresh_tokens (expires_at);

COMMIT;
