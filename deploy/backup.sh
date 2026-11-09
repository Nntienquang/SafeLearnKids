#!/bin/bash
# Backs up the database, uploaded files and sign-in keys into ./backups and keeps 14 days.
# Run from the project folder on the server:  ./deploy/backup.sh
# Daily at 02:00:  crontab -e  then add  0 2 * * * cd /opt/safelearnkids && ./deploy/backup.sh >> backups/backup.log 2>&1
set -euo pipefail
cd "$(dirname "$0")/.."
MSSQL_SA_PASSWORD=$(grep -E '^MSSQL_SA_PASSWORD=' .env | cut -d= -f2-)
stamp=$(date +%F-%H%M)
project=$(docker compose ps --format '{{.Project}}' | head -n1)
mkdir -p backups

echo "[$stamp] database"
docker compose exec -T db /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b \
    -Q "BACKUP DATABASE [hoctapchotre] TO DISK = N'/var/opt/mssql/backup-$stamp.bak' WITH INIT, CHECKSUM"
docker compose cp "db:/var/opt/mssql/backup-$stamp.bak" "backups/hoctapchotre-$stamp.bak"
docker compose exec -T db rm -f "/var/opt/mssql/backup-$stamp.bak"

echo "[$stamp] uploads and keys"
docker run --rm -v "${project}_uploads:/data:ro" -v "$PWD/backups:/out" alpine tar czf "/out/uploads-$stamp.tgz" -C /data .
docker run --rm -v "${project}_appdata:/data:ro" -v "$PWD/backups:/out" alpine tar czf "/out/appdata-$stamp.tgz" -C /data .

find backups -type f \( -name '*.bak' -o -name '*.tgz' \) -mtime +14 -delete
echo "[$stamp] done"
