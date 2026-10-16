#!/bin/bash

set -Eeuo pipefail
export MSYS_NO_PATHCONV=1

SCRIPT_HOME="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_HOME"
export COMPOSE_PROJECT_NAME="${COMPOSE_PROJECT_NAME-unified-scheduling}"

if docker compose version >/dev/null 2>&1; then
  COMPOSE=(docker compose)
elif command -v docker-compose >/dev/null 2>&1; then
  COMPOSE=(docker-compose)
else
  echo "Docker Compose is required but was not found." >&2
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is required but was not found." >&2
  exit 1
fi

OLD_DB_CONTAINER_ID="$("${COMPOSE[@]}" ps -q db)"
if [ -z "$OLD_DB_CONTAINER_ID" ] || [ "$(docker inspect --format '{{.State.Running}}' "$OLD_DB_CONTAINER_ID")" != "true" ]; then
  echo "The existing PostgreSQL 16 'db' container must be running before this script is started." >&2
  exit 1
fi

SERVER_VERSION_NUM="$(
  "${COMPOSE[@]}" exec -T db sh -ec '
    test -n "$POSTGRES_USER"
    test -n "$POSTGRES_DB"
    psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --tuples-only --no-align --command "SHOW server_version_num"
  ' | tr -d '\r'
)"

if [[ "$SERVER_VERSION_NUM" != 16* ]]; then
  echo "The running 'db' container is PostgreSQL $SERVER_VERSION_NUM; PostgreSQL 16 was expected." >&2
  exit 1
fi

COMPOSE_PROJECT="$(docker inspect --format '{{ index .Config.Labels "com.docker.compose.project" }}' "$OLD_DB_CONTAINER_ID")"
TARGET_VOLUME="$(
  docker volume ls --quiet \
    --filter "label=com.docker.compose.project=$COMPOSE_PROJECT" \
    --filter "label=com.docker.compose.volume=postgres-data-18" |
    head -n 1
)"

if [ -n "$TARGET_VOLUME" ]; then
  echo "The PostgreSQL 18 volume '$TARGET_VOLUME' already exists; refusing to overwrite it." >&2
  echo "Resolve or remove that volume before starting a new upgrade." >&2
  exit 1
fi

BACKUP_DIRECTORY="postgres-backups"
BACKUP_NAME="postgres-16-$(date -u +%Y%m%dT%H%M%SZ).dump"
BACKUP_PATH="$BACKUP_DIRECTORY/$BACKUP_NAME"
CONTAINER_BACKUP_PATH="/tmp/$BACKUP_NAME"

mkdir -p "$BACKUP_DIRECTORY"
chmod 700 "$BACKUP_DIRECTORY" 2>/dev/null || true

cleanup() {
  docker exec "$OLD_DB_CONTAINER_ID" rm -f "$CONTAINER_BACKUP_PATH" >/dev/null 2>&1 || true
  if [ -n "${NEW_DB_CONTAINER_ID:-}" ]; then
    docker exec "$NEW_DB_CONTAINER_ID" rm -f "$CONTAINER_BACKUP_PATH" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

echo "Stopping application services to prevent database writes ..."
"${COMPOSE[@]}" stop api web

echo "Backing up PostgreSQL 16 to $BACKUP_PATH ..."
"${COMPOSE[@]}" exec -T db sh -ec '
  pg_dump \
    --username "$POSTGRES_USER" \
    --dbname "$POSTGRES_DB" \
    --format custom \
    --compress 6 \
    --file "$1"
' sh "$CONTAINER_BACKUP_PATH"

echo "Validating the backup archive ..."
"${COMPOSE[@]}" exec -T db pg_restore --list "$CONTAINER_BACKUP_PATH" >/dev/null
docker cp "$OLD_DB_CONTAINER_ID:$CONTAINER_BACKUP_PATH" "$BACKUP_PATH"

if [ ! -s "$BACKUP_PATH" ]; then
  echo "The backup was not copied successfully; the PostgreSQL 16 container has not been removed." >&2
  exit 1
fi

echo "Pulling the PostgreSQL 18 image ..."
"${COMPOSE[@]}" pull db

echo "Removing the PostgreSQL 16 container while retaining its data volume ..."
"${COMPOSE[@]}" rm --stop --force db

echo "Starting PostgreSQL 18 with its new data volume ..."
"${COMPOSE[@]}" up --detach db
NEW_DB_CONTAINER_ID="$("${COMPOSE[@]}" ps -q db)"

if [ -z "$NEW_DB_CONTAINER_ID" ]; then
  echo "PostgreSQL 18 did not create a container. The backup is available at $BACKUP_PATH." >&2
  exit 1
fi

echo "Waiting for PostgreSQL 18 to become healthy ..."
DB_HEALTH=""
for _ in $(seq 1 60); do
  DB_HEALTH="$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$NEW_DB_CONTAINER_ID")"
  case "$DB_HEALTH" in
    healthy)
      break
      ;;
    unhealthy|exited|dead)
      echo "PostgreSQL 18 entered state '$DB_HEALTH'. The backup is available at $BACKUP_PATH." >&2
      exit 1
      ;;
  esac
  sleep 2
done

if [ "$DB_HEALTH" != "healthy" ]; then
  echo "PostgreSQL 18 did not become healthy in time. The backup is available at $BACKUP_PATH." >&2
  exit 1
fi

echo "Restoring the application database into PostgreSQL 18 ..."
docker cp "$BACKUP_PATH" "$NEW_DB_CONTAINER_ID:$CONTAINER_BACKUP_PATH"
"${COMPOSE[@]}" exec -T --user postgres db sh -ec '
  pg_restore \
    --username "$POSTGRES_USER" \
    --dbname "$POSTGRES_DB" \
    --clean \
    --if-exists \
    --no-owner \
    --no-privileges \
    "$1"
  vacuumdb --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --analyze-in-stages
' sh "$CONTAINER_BACKUP_PATH"

echo "Starting application services ..."
"${COMPOSE[@]}" up --detach api web

echo "PostgreSQL 16 to 18 upgrade completed successfully."
echo "Backup retained at: $SCRIPT_HOME/$BACKUP_PATH"
echo "The original PostgreSQL 16 Docker volume was retained for rollback."
