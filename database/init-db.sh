#!/bin/bash
# Prepares the SafeLearn Kids database inside docker compose (service "db-init"); safe to run on every start.
#  1. creates the database and its schema the first time only (database/01-schema.sql),
#  2. adds missing reference data (database/02-reference-data.sql),
#  3. creates or updates the app's own SQL login "safelearn_app" (db_owner of this database only; the app adds
#     later columns at startup, so it needs ALTER rights).
set -euo pipefail

# The defaults are the compose setup; the overrides exist only to try the script against a local SQL Server.
SQLCMD=${SQLCMD:-/opt/mssql-tools18/bin/sqlcmd}
SQL_SERVER=${SQL_SERVER:-db}
DB=${DB_NAME:-hoctapchotre}
if [ "${SQL_WINDOWS_AUTH:-0}" = "1" ]; then AUTH=(-E); else : "${MSSQL_SA_PASSWORD:?MSSQL_SA_PASSWORD is not set}"; AUTH=(-U sa -P "$MSSQL_SA_PASSWORD"); fi
run() { "$SQLCMD" -S "$SQL_SERVER" "${AUTH[@]}" -C -b -I -f 65001 "$@"; }

: "${APP_DB_PASSWORD:?APP_DB_PASSWORD is not set}"
case "$APP_DB_PASSWORD" in *"'"*|*";"*) echo "APP_DB_PASSWORD must not contain ' or ;" >&2; exit 1;; esac

for _ in $(seq 1 60); do
    run -Q "SELECT 1" -o /dev/null 2>/dev/null && break
    echo "waiting for SQL Server..."; sleep 2
done

run -Q "IF DB_ID(N'$DB') IS NULL CREATE DATABASE [$DB];"
has_schema=$(run -d "$DB" -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN OBJECT_ID(N'dbo.Users') IS NULL THEN 0 ELSE 1 END;")
if [ "$has_schema" = "0" ]; then
    echo "Creating the schema..."
    run -d "$DB" -i "${SQL_DIR:-/database}/01-schema.sql"
else
    echo "Schema already present, skipping 01-schema.sql."
fi
run -d "$DB" -i "${SQL_DIR:-/database}/02-reference-data.sql"

run -Q "IF SUSER_ID(N'safelearn_app') IS NULL CREATE LOGIN [safelearn_app] WITH PASSWORD = N'$APP_DB_PASSWORD', CHECK_POLICY = ON;
        ELSE ALTER LOGIN [safelearn_app] WITH PASSWORD = N'$APP_DB_PASSWORD';"
run -d "$DB" -Q "IF USER_ID(N'safelearn_app') IS NULL CREATE USER [safelearn_app] FOR LOGIN [safelearn_app];
                 IF IS_ROLEMEMBER(N'db_owner', N'safelearn_app') = 0 ALTER ROLE db_owner ADD MEMBER [safelearn_app];"
echo "Database ready."
