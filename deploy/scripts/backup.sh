#!/usr/bin/env bash
#
# Sao lưu cơ sở dữ liệu và thư mục media.
#
# Chạy tay:   ./deploy/scripts/backup.sh
# Chạy định kỳ, thêm vào crontab của root:
#   0 3 * * * cd /srv/englishforit && ./deploy/scripts/backup.sh >> /var/log/efit-backup.log 2>&1
#
# Bản sao lưu chưa từng phục hồi thử thì coi như chưa có. Diễn tập một lần trước khi
# mở cho học viên — xem restore.sh.

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

BACKUP_DIR="${BACKUP_DIR:-$ROOT_DIR/backups}"
KEEP_DAYS="${KEEP_DAYS:-7}"
STAMP="$(date -u +%Y%m%d-%H%M%S)"

# shellcheck disable=SC1091
[ -f .env ] && set -a && . ./.env && set +a

POSTGRES_USER="${POSTGRES_USER:-efit}"
POSTGRES_DB="${POSTGRES_DB:-englishforit}"
DB_CONTAINER="${DB_CONTAINER:-englishforit-db-1}"

mkdir -p "$BACKUP_DIR"

echo "[$(date -u +%FT%TZ)] Bat dau sao luu"

# --- Cơ sở dữ liệu ---
# pg_dump định dạng custom (-Fc): nén sẵn và cho phép phục hồi từng bảng.
DB_FILE="$BACKUP_DIR/db-$STAMP.dump"
docker exec "$DB_CONTAINER" pg_dump \
    --username="$POSTGRES_USER" \
    --dbname="$POSTGRES_DB" \
    --format=custom \
    --compress=6 \
    > "$DB_FILE"

DB_SIZE=$(du -h "$DB_FILE" | cut -f1)
echo "  DB   -> $(basename "$DB_FILE") ($DB_SIZE)"

# --- Media ---
# Audio sinh lại được từ nội dung, nhưng sinh lại tốn vài phút CPU nên vẫn sao lưu.
if [ -d "$ROOT_DIR/media" ] && [ -n "$(ls -A "$ROOT_DIR/media" 2>/dev/null)" ]; then
    MEDIA_FILE="$BACKUP_DIR/media-$STAMP.tar.gz"

    # Thư mục này SỐNG trong lúc sao lưu: học viên thu âm bất cứ lúc nào, và mẻ sinh giọng
    # cũng ghi vào đây. Gặp file đổi giữa chừng thì tar trả mã 1 — đó là cảnh báo, không
    # phải hỏng — nhưng `set -e` ở đầu script coi nó là lỗi và giết cả lần triển khai.
    # Đúng một lần như vậy đã làm hỏng một bản nâng cấp hoàn toàn bình thường.
    #
    # Mã 2 trở lên mới là lỗi thật (không ghi được, hết đĩa) và vẫn phải dừng.
    #
    # Bỏ qua file .tmp: chúng là bản ghi dở của bước sinh giọng, sao lưu chúng vừa vô nghĩa
    # vừa chính là thứ hay biến mất giữa chừng.
    set +e
    tar --exclude='*.tmp' \
        --warning=no-file-changed \
        --warning=no-file-removed \
        -czf "$MEDIA_FILE" -C "$ROOT_DIR" media
    TAR_STATUS=$?
    set -e

    if [ "$TAR_STATUS" -gt 1 ]; then
        echo "  LOI: khong dong goi duoc media (tar tra ma $TAR_STATUS)" >&2
        exit "$TAR_STATUS"
    fi

    if [ "$TAR_STATUS" -eq 1 ]; then
        echo "  MEDIA: co file doi trong luc dong goi, ban sao luu van dung duoc"
    fi

    echo "  MEDIA -> $(basename "$MEDIA_FILE") ($(du -h "$MEDIA_FILE" | cut -f1))"
fi

# --- Kiểm tra bản dump đọc được ---
# Dump hỏng mà không ai biết là kịch bản tệ nhất: phát hiện lúc cần phục hồi thì đã muộn.
if ! docker exec -i "$DB_CONTAINER" pg_restore --list < "$DB_FILE" > /dev/null 2>&1; then
    echo "  LOI: ban dump khong doc duoc, da giu lai de dieu tra" >&2
    exit 1
fi
echo "  Kiem tra dump: OK"

# --- Dọn bản cũ ---
find "$BACKUP_DIR" -name 'db-*.dump' -mtime "+$KEEP_DAYS" -delete
find "$BACKUP_DIR" -name 'media-*.tar.gz' -mtime "+$KEEP_DAYS" -delete

echo "[$(date -u +%FT%TZ)] Xong. Giu lai $KEEP_DAYS ngay gan nhat."
