-- Runs once, on first cluster initialization (empty data volume).
-- The app database (MedicalAssistantDb) is created by POSTGRES_DB; this adds the
-- second database on the same server, with the same owner/credentials.
-- The Clinical Knowledge service's EF migrations create the pgvector extension
-- and the ai_med schema on startup; creating it here up front is belt-and-suspenders.
CREATE DATABASE ai_med;
\connect ai_med
CREATE EXTENSION IF NOT EXISTS vector;
