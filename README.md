# English for IT

Nền tảng học tiếng Anh giao tiếp cho **kỹ sư IT, Cloud và AI người Việt** — trọng tâm là đúng những việc họ làm mỗi ngày:
standup, báo sự cố, gọi vendor, review kiến trúc cloud, đề xuất AI use case, viết incident report.

Giải thích hoàn toàn bằng tiếng Việt. Bắt đầu được từ số 0.

---

## Chạy trong ba lệnh

Cần: Docker Desktop hoặc Docker Engine + Compose v2, 8 GB RAM, ~6 GB đĩa trống.

```bash
make env      # tạo .env, tự sinh POSTGRES_PASSWORD và APP_MASTER_KEY
make up       # build và khởi động toàn bộ stack
make ps       # xem trạng thái
```

Mở **http://localhost:9090**

| Thành phần | Địa chỉ |
|---|---|
| Giao diện | http://localhost:9090 |
| API | http://localhost:8080 |
| OpenAPI | http://localhost:8080/openapi/v1.json |
| Postgres | localhost:55432 |

Không có `make`? Chạy thẳng:

```bash
cp .env.example .env && docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build
```

(nhớ điền `POSTGRES_PASSWORD` và `APP_MASTER_KEY` trong `.env` trước)

---

## Stack

| Tầng | Công nghệ |
|---|---|
| Frontend | React 19 · TypeScript · Vite 8 · **Tailwind CSS v4.3** · React Router 7 · TanStack Query 5 |
| Backend | **ASP.NET Core 10** · EF Core 10 · minimal API theo module |
| Database | PostgreSQL 16 |
| Reverse proxy | Caddy 2 |
| Đóng gói | Docker multi-stage · Docker Compose |
| Đích triển khai | Ubuntu VM trên Azure |

Không có `tailwind.config.js` và không có `postcss.config.js` — Tailwind v4 khai báo theme ngay trong CSS bằng `@theme`,
cài qua plugin Vite chính thức.

---

## Kiến trúc một phút

```
Trình duyệt
    │  :443
    ▼
  Caddy ──────────────┬──── /api/*, /health/*  ──►  api (ASP.NET Core 10)
  (TLS, CSP, nén)     │                                  │
                      └──── còn lại ──► web (tệp tĩnh)   ├─ asyncpg ─► postgres:16
                                                          └─ HTTP ────► speech (Part 3)
```

Bốn quyết định định hình mọi thứ:

1. **Web và API cùng một gốc.** Cookie phiên `SameSite=Lax` chỉ hoạt động khi cùng site. Tách `api.domain.com` sẽ phải nới sang `SameSite=None` và mất một lớp phòng CSRF.
2. **Phiên lưu ở server, không dùng JWT.** Thu hồi tức thì quan trọng hơn stateless. Cookie giữ token thô, DB chỉ giữ SHA-256.
3. **Nội dung học là YAML trong repo, không phải bảng CMS.** `content/**/*.yaml` là nguồn sự thật; seeder upsert theo mã bài nên nạp lại **không bao giờ xoá tiến độ học viên**.
4. **Chấm phát âm chạy tại chỗ.** Giọng học viên không gửi ra dịch vụ bên thứ ba (Part 3).

Chi tiết: [`ARCHITECTURE.md`](ARCHITECTURE.md)

---

## Cấu trúc

```
apps/
  api/              ASP.NET Core 10 — Program, Middleware, Modules
  web/              React + TS + Tailwind v4
src/
  Domain/           Entity, enum, quy tắc miền — không phụ thuộc gì
  Application/      Abstraction, use case, service nghiệp vụ
  Infrastructure/   EF Core, DbContext, cấu hình, bảo mật, migration
  Worker/           Job định kỳ (Part 3)
tests/
  UnitTests/            Không cần Docker
  IntegrationTests/     Testcontainers + Postgres thật
  ContentValidationTests/ Cổng chất lượng nội dung
content/            YAML bài học, đề xếp lớp, roleplay, truyện
deploy/             Caddyfile, script vận hành
docs/               Giáo trình, nguồn nội dung
```

Luật phụ thuộc: `apps` → `Application` → `Domain`. `Infrastructure` cài đặt abstraction của `Application`.
`Application` **không** tham chiếu `Infrastructure`.

---

## Nội dung học

Thiết kế giáo trình: [`docs/curriculum.md`](docs/curriculum.md) · Nguồn tham chiếu: [`docs/content-sources.md`](docs/content-sources.md)

Tóm tắt: **hai trục** — bốn bậc L0→L4 (bám CEFR) và ba tầng ngữ cảnh (đời sống → văn phòng → chuyên môn).
**Năm chế độ học**: đủ bốn kỹ năng, hoặc chỉ Nghe / chỉ Nói / chỉ Đọc / chỉ Viết.
Thứ tự ưu tiên kỹ năng toàn hệ thống: **nghe → nói → đọc → viết**.

Cấu trúc tài liệu nghề bám chuẩn ngành có thật, không tự nghĩ ra:
postmortem theo 13 mục của Google SRE, thuật ngữ vận hành theo ITIL 4, sự cố bảo mật theo NIST SP 800-61,
kiến trúc cloud theo sáu trụ cột AWS Well-Architected, rủi ro AI theo NIST AI RMF.

---

## Lệnh hay dùng

```bash
make help          # liệt kê tất cả
make up            # chạy stack dev
make logs          # xem log
make test          # chạy test .NET
make typecheck     # kiểm kiểu TypeScript
make psql          # mở psql
make migration m="them bang x"   # tạo migration mới
make backup        # sao lưu DB và media
make down          # dừng, GIỮ dữ liệu
make reset         # dừng và XOÁ SẠCH dữ liệu (chỉ dùng trên máy dev)
```

Muốn hot reload frontend: `make web-dev` (Vite ở cổng 5173, proxy `/api` sang cổng 8080).

### Sinh giọng đọc cho phần Nghe

```bash
./deploy/scripts/generate-audio.sh
```

Chạy sau mỗi lần triển khai có thêm hoặc sửa bài. API ghi sẵn danh sách đoạn cần đọc vào
`media/tts/manifest.jsonl` lúc khởi động; script nạp model Piper một lần rồi đọc cả loạt,
đặt tên file theo hash của chính đoạn văn bản.

Chạy lại bao nhiêu lần cũng được — đoạn nào đã có file thì bỏ qua. Đoạn chưa sinh thì
`/api/v1/media/tts` trả 404 và giao diện tự lùi về giọng của trình duyệt, nên bài mới thêm
vẫn nghe được ngay trong lúc chờ mẻ sinh kế tiếp.

---

## Bảo mật

- **Mật khẩu**: Argon2id, tham số đóng gói trong chuỗi hash nên nâng cấp sau này không làm hỏng hash cũ
- **Phiên**: cookie `HttpOnly` + `SameSite=Lax` + `Secure`, token lưu dạng SHA-256 trong DB
- **CSRF**: double-submit — cookie CSRF đọc được bằng JS, phải gửi lại qua header `X-CSRF-Token`
- **Bí mật trong DB**: AES-256-GCM, khoá dẫn xuất từ `APP_MASTER_KEY` bằng HKDF
- **Rate limit**: theo user khi đã đăng nhập, theo IP khi chưa; riêng nhóm xác thực siết chặt hơn
- **Đăng nhập sai**: email lạ và sai mật khẩu trả **cùng một thông báo**, và cùng tốn thời gian như nhau
- **Đăng ký**: email đã tồn tại trả về **y hệt** đăng ký thành công — trang đăng ký không thành công cụ dò tài khoản
- Container chạy non-root, có giới hạn bộ nhớ, có healthcheck

> **Đừng đổi `APP_MASTER_KEY` sau khi đã chạy.** Mọi bí mật trong DB mã hoá bằng khoá dẫn xuất từ nó.
> Đổi = mất sạch, và app sẽ **im lặng** coi như chưa cấu hình chứ không báo lỗi ồn ào.

---

## Triển khai Azure VM

Xem [`AZURE_VM_DEPLOYMENT.md`](AZURE_VM_DEPLOYMENT.md).

Bắt buộc có **HTTPS**: micro của trình duyệt không chạy trên HTTP thuần (trừ localhost) —
không có HTTPS thì toàn bộ phần luyện nói chết.

---

## Trạng thái hiện tại

Đây là **Part 1**: nền móng đã chạy thật và đã kiểm chứng end-to-end trên Docker.

Đã có: monorepo, 43 bảng + migration, đăng ký/đăng nhập/phiên/CSRF/rate limit, khung frontend + dashboard,
Docker + Caddy + Compose (dev và Azure VM), script sao lưu/phục hồi, 31 unit test.

Chưa có: audio sinh sẵn, sửa nội dung trực tiếp trong admin, chấm phát âm ở mức âm vị.

CI: `.github/workflows/ci.yml` — build, cổng nội dung, 267 test (đơn vị + tích hợp trên Postgres thật), lint và build web, và build cả ba ảnh Docker.

Danh sách đầy đủ và trung thực: [`IMPLEMENTATION_GAPS.md`](IMPLEMENTATION_GAPS.md)
