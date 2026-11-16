#!/bin/sh
set -eu
psql -X -v ON_ERROR_STOP=1 -U postgres -d ss -f /fixture/ss-schema-noseed.sql
psql -X -v ON_ERROR_STOP=1 -U postgres -d ss -f /fixture/ss-synthetic-generator.sql
psql -X -v ON_ERROR_STOP=1 -U postgres -d ss -c 'CREATE ROLE ss_migration_reader LOGIN; GRANT CONNECT ON DATABASE ss TO ss_migration_reader; GRANT USAGE ON SCHEMA public, synthetic TO ss_migration_reader; GRANT SELECT ON ALL TABLES IN SCHEMA public, synthetic TO ss_migration_reader; ALTER ROLE ss_migration_reader SET default_transaction_read_only = on;'
psql -X -v ON_ERROR_STOP=1 -U postgres -d ss -c "COMMENT ON DATABASE ss IS 'SS SYNTHETIC';"
touch /var/lib/postgresql/data/ss-fixture-ready
