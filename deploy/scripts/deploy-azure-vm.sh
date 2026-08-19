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
#
# Lần cài đầu thì chưa có gì để sao lưu. Kiểm ở đây chứ KHÔNG cho backup.sh tự bỏ qua khi
# thiếu container: nếu container biến mất giữa hai lần nâng cấp thì đó là sự cố thật và phải
# dừng lại, không được lặng lẽ nâng cấp đè lên.
echo "[1/5] Sao luu truoc khi nang cap..."

if docker ps -a --format '{{.Names}}' | grep -qx "${DB_CONTAINER:-englishforit-db-1}"; then
    ./deploy/scripts/backup.sh
else
    echo "     Chua co ban cai nao tren may nay, bo qua sao luu."
fi

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
#
# Phải truyền IMAGE_TAG lần nữa dù bước 3 đã ghi nó vào .env.
#
# Đầu script có `set -a && . ./.env`, nên IMAGE_TAG CŨ đang nằm trong biến môi trường, mà biến
# môi trường thì thắng file .env khi compose phân giải ${IMAGE_TAG}. Thiếu dòng này, compose
# dựng lại đúng bản cũ, kiểm tra sẵn sàng ở bước 5 vẫn xanh vì dịch vụ vẫn chạy, và script báo
# "nang cap xong" trong khi không có gì được nâng cấp.
echo "[4/5] Khoi dong lai dich vu..."
IMAGE_TAG="$NEW_TAG" $COMPOSE up -d

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

# Cùng lý do như bước 4: biến môi trường thắng .env, mà biến đang giữ tag CŨ. Ở đây nó tình cờ
# đúng thứ ta muốn, nhưng vẫn ghi rõ ra để lần sau đọc không phải đoán.
IMAGE_TAG="$CURRENT_TAG" $COMPOSE up -d
echo "Da quay lui. Xem log: $COMPOSE logs api --tail 100" >&2
exit 1
