-- Local-dev convenience: a classic "postgres"/"postgres" superuser for admin tools.
-- The cluster's bootstrap superuser is "medicalassistant" (POSTGRES_USER), so the
-- "postgres" role would not otherwise exist. LOCAL DEV ONLY — do not use in prod.
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'postgres') THEN
        CREATE ROLE postgres LOGIN SUPERUSER PASSWORD 'postgres';
    ELSE
        ALTER ROLE postgres WITH LOGIN SUPERUSER PASSWORD 'postgres';
    END IF;
END
$$;
