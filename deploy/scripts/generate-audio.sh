#!/usr/bin/env bash
#
# Sinh giọng đọc cho phần Nghe.
#
# Chạy sau mỗi lần triển khai có thêm hoặc sửa bài. An toàn khi chạy lại: đoạn nào đã có
# file thì bỏ qua, nên lượt thứ hai gần như không tốn gì.
#
#   ./deploy/scripts/generate-audio.sh
#   TTS_LIMIT=20 ./deploy/scripts/generate-audio.sh    # chạy thử một mẻ nhỏ
#
# KHÔNG chạy trong container API. Nạp model Piper tốn vài trăm MB và đọc cả nghìn câu mất
# vài phút; nhét việc đó vào tiến trình phục vụ web là chặn đường đăng nhập của học viên
# mỗi lần khởi động lại.

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

MEDIA_DIR="${MEDIA_DIR:-$ROOT_DIR/media}"
TTS_DIR="$MEDIA_DIR/tts"

# Giọng để NGOÀI media/: backup.sh đóng gói cả thư mục media, và một model 61 MB đi kèm
# mọi bản sao lưu là phí chỗ cho thứ tải lại được bất cứ lúc nào.
VOICE_DIR="${VOICE_DIR:-$ROOT_DIR/tts-voices}"
VOICE="${PIPER_VOICE:-en_US-lessac-medium}"
IMAGE="${PIPER_IMAGE:-lscr.io/linuxserver/piper:latest}"

# UID của user 'app' trong ảnh .NET. File sinh ra phải để API đọc được.
APP_UID="${APP_UID:-1654}"

log() { printf '%s %s\n' "$(date '+%H:%M:%S')" "$*"; }

# Chỉ cho một mẻ chạy tại một thời điểm.
#
# Deploy tự gọi script này, và người vận hành cũng có thể chạy tay cùng lúc. Hai tiến trình
# cùng thấy một đoạn còn thiếu sẽ cùng ghi vào một file tạm, và file thắng cuộc là file bị
# ghi đè giữa chừng — nghe ra tiếng rẹt rồi im.
#
# Khoá để ở /tmp chứ không ở media/: thư mục đó thuộc user của container nên script chạy
# bằng tài khoản vận hành không tạo file trong đó được.
LOCK_FILE="/tmp/englishforit-generate-audio.lock"
exec 9>"$LOCK_FILE"

if ! flock -n 9; then
    echo "Da co mot luot sinh giong dang chay. Bo qua luot nay."
    exit 0
fi

if [ ! -f "$TTS_DIR/manifest.jsonl" ]; then
    echo "Khong thay $TTS_DIR/manifest.jsonl" >&2
    echo "API ghi file nay sau khi seed noi dung. Khoi dong API mot lan roi chay lai." >&2
    exit 1
fi

mkdir -p "$VOICE_DIR"

# Tải giọng nếu chưa có. Chỉ tải một lần, lần sau dùng lại file trên đĩa.
#
# Tên giọng theo dạng <ngonngu>-<ten>-<chatluong>, còn đường dẫn trên kho lại tách thành
# bốn cấp, nên phải bóc từng phần chứ không ghép thẳng được.
voice_locale="${VOICE%%-*}"                 # en_US
voice_rest="${VOICE#*-}"                    # lessac-medium
voice_name="${voice_rest%-*}"               # lessac
voice_quality="${voice_rest##*-}"           # medium

BASE_URL="https://huggingface.co/rhasspy/piper-voices/resolve/main"
BASE_URL="$BASE_URL/${voice_locale%%_*}/$voice_locale/$voice_name/$voice_quality"

for suffix in .onnx .onnx.json; do
    target="$VOICE_DIR/$VOICE$suffix"
    if [ ! -s "$target" ]; then
        log "Tai giong $VOICE$suffix ..."
        curl -fsSL -o "$target" "$BASE_URL/$VOICE$suffix?download=true"
    fi
done

log "Bat dau sinh giong. Thu muc: $TTS_DIR"

docker run --rm \
    --user "$APP_UID:$APP_UID" \
    -e HOME=/tmp \
    -e TTS_DIR=/media/tts \
    -e PIPER_MODEL="/voices/$VOICE.onnx" \
    -e TTS_LIMIT="${TTS_LIMIT:-0}" \
    -v "$MEDIA_DIR:/media" \
    -v "$VOICE_DIR:/voices:ro" \
    -v "$ROOT_DIR/deploy/scripts/generate-audio.py:/generate.py:ro" \
    --entrypoint python3 \
    "$IMAGE" /generate.py

log "Hoan tat."
