#!/usr/bin/env python3
"""Sinh file audio cho mọi đoạn tiếng Anh có nút bấm nghe.

Chạy BÊN TRONG ảnh piper, không chạy trên máy chủ: ảnh đó đã có sẵn onnxruntime và
espeak-ng, còn máy chủ thì không và cũng không nên cài thêm.

Đầu vào là media/tts/manifest.jsonl do API ghi ra sau mỗi lần khởi động. Mỗi dòng có
"hash" và "text"; file kết quả đặt tên đúng bằng hash, nên API tìm lại được mà hai bên
không cần thoả thuận gì thêm.

Nạp model một lần rồi đọc cả loạt. Gọi CLI cho từng câu thì mỗi lần lại nạp model mất
gần hai giây, trong khi đọc xong một câu chỉ mất khoảng 0,15 giây — chênh nhau mười lần
trên cùng một khối lượng.

Bỏ qua đoạn đã có file. Nhờ vậy chạy lại sau mỗi lần thêm bài chỉ sinh phần mới, và
chạy nhầm hai lần cũng không tốn gì.
"""

import json
import os
import sys
import time
import wave

TTS_DIR = os.environ.get("TTS_DIR", "/media/tts")
MODEL_PATH = os.environ.get("PIPER_MODEL", "/voices/en_US-lessac-medium.onnx")

# Giọng của lượt chạy này. Manifest chứa đoạn của nhiều giọng, mỗi lượt chỉ nạp một model
# nên phải bỏ qua đoạn không thuộc giọng đang chạy — script gọi lại một lần cho mỗi giọng.
VOICE = os.environ.get("PIPER_VOICE", "en_US-lessac-medium")

# Trần số file sinh trong một lượt. Đặt >0 khi muốn chạy thử một mẻ nhỏ trước.
LIMIT = int(os.environ.get("TTS_LIMIT", "0"))

MANIFEST = os.path.join(TTS_DIR, "manifest.jsonl")


def load_manifest():
    if not os.path.exists(MANIFEST):
        sys.exit(
            f"Khong thay {MANIFEST}. API ghi file nay sau khi seed noi dung, "
            "nen hay khoi dong API it nhat mot lan truoc."
        )

    entries = []
    with open(MANIFEST, encoding="utf-8") as handle:
        for line_number, line in enumerate(handle, start=1):
            line = line.strip()
            if not line:
                continue
            try:
                item = json.loads(line)
            except json.JSONDecodeError as error:
                sys.exit(f"Dong {line_number} trong manifest khong phai JSON hop le: {error}")

            # Manifest cũ không có trường voice: coi như giọng chính để file đã sinh
            # trước khi có nhiều giọng vẫn khớp.
            if item.get("hash") and item.get("text") and item.get("voice", VOICE) == VOICE:
                entries.append((item["hash"], item["text"]))

    return entries


def main():
    entries = load_manifest()
    os.makedirs(TTS_DIR, exist_ok=True)

    missing = [
        (h, t) for h, t in entries
        if not os.path.exists(os.path.join(TTS_DIR, h + ".wav"))
    ]

    print(f"Manifest co {len(entries)} doan, thieu {len(missing)} file.", flush=True)

    if not missing:
        print("Khong co gi de sinh.", flush=True)
        return

    if LIMIT > 0:
        missing = missing[:LIMIT]
        print(f"Gioi han luot nay: {len(missing)} file.", flush=True)

    # Nạp model sau khi đã biết chắc có việc để làm: nạp mất gần hai giây và tốn vài
    # trăm MB, không đáng bỏ ra chỉ để phát hiện không thiếu file nào.
    from piper import PiperVoice

    started = time.monotonic()
    voice = PiperVoice.load(MODEL_PATH)
    print(f"Nap model xong sau {time.monotonic() - started:.1f}s.", flush=True)

    done = 0
    failed = 0
    started = time.monotonic()

    for index, (digest, text) in enumerate(missing, start=1):
        final = os.path.join(TTS_DIR, digest + ".wav")

        # Ghi ra file tạm rồi mới đổi tên. API doc thu muc nay bat cu luc nao, va mot
        # file WAV moi ghi duoc nua chung se phat ra tieng rẹt rồi im.
        temporary = final + ".tmp"

        try:
            with wave.open(temporary, "wb") as target:
                voice.synthesize_wav(text, target)

            os.replace(temporary, final)
            done += 1
        except Exception as error:  # noqa: BLE001 - một câu hỏng không được dừng cả mẻ
            failed += 1
            print(f"  LOI o {digest}: {error}", flush=True)
            if os.path.exists(temporary):
                os.remove(temporary)

        if index % 25 == 0 or index == len(missing):
            elapsed = time.monotonic() - started
            rate = index / elapsed if elapsed > 0 else 0
            remaining = (len(missing) - index) / rate if rate > 0 else 0
            print(
                f"  {index}/{len(missing)} — {rate:.1f} doan/giay, "
                f"con khoang {remaining / 60:.1f} phut",
                flush=True,
            )

    total_bytes = sum(
        os.path.getsize(os.path.join(TTS_DIR, name))
        for name in os.listdir(TTS_DIR)
        if name.endswith(".wav")
    )

    print(
        f"Xong: sinh {done} file, loi {failed}. "
        f"Thu muc audio hien {total_bytes / 1_048_576:.1f} MB.",
        flush=True,
    )

    # Đoạn mồ côi: file còn đó nhưng câu tương ứng đã bị sửa hoặc xoá khỏi giáo trình.
    # Chỉ báo số lượng, KHÔNG tự xoá — một manifest ghi thiếu sẽ biến bước dọn dẹp
    # thành bước xoá sạch.
    known = {h for h, _ in entries}
    orphans = [
        name for name in os.listdir(TTS_DIR)
        if name.endswith(".wav") and name[:-4] not in known
    ]

    if orphans:
        print(f"Co {len(orphans)} file khong con doan nao dung toi (khong tu xoa).", flush=True)

    if failed:
        sys.exit(1)


if __name__ == "__main__":
    main()
