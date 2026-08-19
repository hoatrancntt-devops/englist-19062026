#!/usr/bin/env bash
#
# Nâng cấp phiên bản đang chạy trên một máy chủ đơn.
#
#   ./deploy/scripts/deploy-azure-vm.sh v0.3.0
#
# Mặc định nhắm Azure VM. Máy khác thì đổi hai biến dưới đây, KHÔNG chép script ra bản thứ hai:
# phần sao lưu, chờ sẵn sàng và quay lui phải giống nhau ở mọi nơi, mà hai bản song song thì
# chỉ một bản được sửa khi có lỗi.
#
#   COMPOSE_OVERLAY=docker-compose.vps.yml \
#   HEALTH_URL=http://127.0.0.1:8080/health/ready \
#     ./deploy/scripts/deploy-azure-vm.sh v0.3.0
#
# Nguyên tắc: image gắn tag cụ thể, không dùng 'latest'. Có tag cụ thể thì mới
# quay lui được, và mới biết chắc VM đang chạy đúng bản nào.

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

NEW_TAG="${1:-}"

if [ -z "$NEW_TAG" ]; then
    echo "Cach dung: $0 <image-tag>" >&2
    echo "Vi du:     $0 v0.3.0" >&2
    exit 1
fi

if [ "$NEW_TAG" = "latest" ]; then
    echo "Khong dung tag 'latest' tren production: khong quay lui duoc." >&2
    exit 1
fi

# shellcheck disable=SC1091
set -a && . ./.env && set +a

CURRENT_TAG="${IMAGE_TAG:-<chua-dat>}"

COMPOSE_OVERLAY="${COMPOSE_OVERLAY:-docker-compose.azure-vm.yml}"
HEALTH_URL="${HEALTH_URL:-http://localhost/health/ready}"
COMPOSE="docker compose -f docker-compose.yml -f $COMPOSE_OVERLAY"

echo "Dang chay:   $CURRENT_TAG"
echo "Se nang len: $NEW_TAG"
echo ""

# 1. Sao lưu trước khi đụng vào bất cứ thứ gì.
echo "[1/5] Sao luu truoc khi nang cap..."
./deploy/scripts/backup.sh

# 2. Kéo image mới về TRƯỚC khi dừng dịch vụ, để thời gian gián đoạn chỉ là thời gian
#    khởi động lại chứ không gồm cả thời gian tải image.
echo "[2/5] Keo image $NEW_TAG..."

# Kéo cả worker. Bỏ sót nó thì `up -d` ở bước 4 mới đi tải, tức là tải image DIỄN RA trong
# lúc dịch vụ đã dừng — đúng thứ mà việc kéo trước đang cố tránh.
IMAGE_TAG="$NEW_TAG" $COMPOSE pull api worker web

# 3. Ghi tag mới vào .env để lần khởi động lại sau vẫn đúng phiên bản.
echo "[3/5] Cap nhat .env..."
sed -i.bak "s/^IMAGE_TAG=.*/IMAGE_TAG=$NEW_TAG/" .env

# 4. Khởi động lại. Migration tự chạy lúc API khởi động.
echo "[4/5] Khoi dong lai dich vu..."
$COMPOSE up -d

# 5. Chờ tới khi thật sự sẵn sàng; thất bại thì quay lui.
echo "[5/5] Doi dich vu san sang..."
for _ in $(seq 1 40); do
    if curl -fsS "$HEALTH_URL" > /dev/null 2>&1; then
        echo ""
        echo "Nang cap xong: $CURRENT_TAG -> $NEW_TAG"
        rm -f .env.bak
        exit 0
    fi
    sleep 5
done

echo "" >&2
echo "KHONG SAN SANG sau 200 giay. Dang quay lui ve $CURRENT_TAG..." >&2
mv .env.bak .env
$COMPOSE up -d
echo "Da quay lui. Xem log: $COMPOSE logs api --tail 100" >&2
exit 1
