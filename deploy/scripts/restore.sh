#!/usr/bin/env bash
#
# Phục hồi từ bản sao lưu.
#
#   ./deploy/scripts/restore.sh backups/db-20260818-030000.dump
#
# Lệnh này GHI ĐÈ dữ liệu hiện tại. Có bước xác nhận vì không thể hoàn tác.

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

DUMP_FILE="${1:-}"

if [ -z "$DUMP_FILE" ] || [ ! -f "$DUMP_FILE" ]; then
    echo "Cach dung: $0 <duong-dan-file-dump>" >&2
    echo "" >&2
    echo "Cac ban co san:" >&2
    ls -lh backups/db-*.dump 2>/dev/null | awk '{print "  " $9 "  " $5 "  " $6 " " $7 " " $8}' >&2 || echo "  (khong co)" >&2
    exit 1
fi

# shellcheck disable=SC1091
[ -f .env ] && set -a && . ./.env && set +a

POSTGRES_USER="${POSTGRES_USER:-efit}"
POSTGRES_DB="${POSTGRES_DB:-englishforit}"
DB_CONTAINER="${DB_CONTAINER:-englishforit-db-1}"

echo "CANH BAO: se GHI DE toan bo du lieu trong database '$POSTGRES_DB'."
echo "Nguon:    $DUMP_FILE"
echo ""
read -r -p "Go dung chu 'PHUC HOI' de tiep tuc: " confirm

if [ "$confirm" != "PHUC HOI" ]; then
    echo "Da huy."
    exit 1
fi

# Dừng API trước: phục hồi trong khi ứng dụng còn ghi sẽ tạo dữ liệu lai giữa hai thời điểm.
echo "Dung API..."
docker compose stop api

echo "Dang phuc hoi..."
# --clean --if-exists: xoá đối tượng cũ trước khi tạo lại, không lỗi khi đối tượng chưa có.
# --single-transaction: hỏng giữa chừng thì quay lui hết, không để DB ở trạng thái nửa vời.
docker exec -i "$DB_CONTAINER" pg_restore \
    --username="$POSTGRES_USER" \
    --dbname="$POSTGRES_DB" \
    --clean \
    --if-exists \
    --single-transaction \
    --no-owner \
    < "$DUMP_FILE"

echo "Khoi dong lai API..."
docker compose start api

echo ""
echo "Xong. Kiem tra lai:"
echo "  curl -fsS http://localhost/health/ready"
