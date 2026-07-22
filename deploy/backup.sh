#!/bin/sh
# Nightly backup of the SQLite DB + uploads. Run as a Coolify Scheduled Task on the
# `api` container (or host cron with `docker exec`). Keeps 14 days.
#
# Coolify Scheduled Task command:  sh /app/deploy/backup.sh   (mount or bake this file)
# Host cron alternative:           docker exec <api-container> sh -c "$(cat deploy/backup.sh)"

set -eu

BACKUP_DIR=/data/backups
STAMP=$(date +%Y%m%d-%H%M)

mkdir -p "$BACKUP_DIR"

# Consistent SQLite snapshot (works while the app is running)
sqlite3 /data/yemenibreeze.db ".backup '$BACKUP_DIR/db-$STAMP.db'" 2>/dev/null \
  || cp /data/yemenibreeze.db "$BACKUP_DIR/db-$STAMP.db"

tar -czf "$BACKUP_DIR/uploads-$STAMP.tar.gz" -C /app/wwwroot uploads 2>/dev/null || true

# Rotate: keep 14 days
find "$BACKUP_DIR" -name 'db-*.db' -mtime +14 -delete
find "$BACKUP_DIR" -name 'uploads-*.tar.gz' -mtime +14 -delete

echo "Backup complete: $BACKUP_DIR (db-$STAMP.db)"
