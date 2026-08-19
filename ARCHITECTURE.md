# Kiến trúc

Ngày: 2026-08-18 · Trạng thái: Part 1 (nền móng đã chạy và đã kiểm chứng)

Tài liệu này giải thích **vì sao** hệ thống có hình dạng như hiện tại. Cái gì đã có, cái gì chưa, xem [`IMPLEMENTATION_GAPS.md`](IMPLEMENTATION_GAPS.md).

---

## 1. Tổng thể

```
                    Internet
                       │ :443
              ┌────────▼────────┐
              │  Caddy (proxy)  │  TLS, HSTS, CSP, nén
              └───┬─────────┬───┘
       /api/*     │         │      còn lại
     /health/*    │         │
                  ▼         ▼
        ┌──────────────┐  ┌──────────────┐
        │ api :8080    │  │ web :8081    │
        │ ASP.NET 10   │  │ Caddy + tệp  │
        └──┬────────┬──┘  └──────────────┘
           │        │
           │        └── HTTP ──► speech (Part 3, chỉ trong mạng nội bộ)
           ▼
     ┌──────────────┐
     │ postgres:16  │  volume pgdata
     └──────────────┘
```

Chỉ `proxy` mở cổng ra ngoài. `db`, `api`, `web` nằm trong mạng bridge nội bộ và không thể truy cập trực tiếp từ Internet.

---

## 2. Bốn quyết định nền

### 2.1 Web và API cùng một gốc

Cookie phiên đặt `SameSite=Lax`. Cookie `Lax` chỉ được gửi kèm khi trang gọi **cùng site** với cookie.
Tách API sang `api.domain.com` sẽ buộc phải nới sang `SameSite=None`, và mất hẳn lớp phòng CSRF mà trình duyệt cho không.

Cái giá phải trả: Caddy phải định tuyến theo đường dẫn, và không thể scale web/api độc lập bằng DNS. Với quy mô hiện tại, đổi lấy một lớp bảo mật là xứng đáng.

### 2.2 Phiên lưu server, không dùng JWT

JWT stateless nhưng **không thu hồi được**. Ở đây thu hồi tức thì quan trọng hơn: đổi mật khẩu phải làm mọi thiết bị khác đăng xuất ngay.

Cách làm:
- Cookie giữ token thô (256 bit ngẫu nhiên).
- DB chỉ giữ SHA-256 của token → lộ bản dump DB không đủ để mạo danh ai.
- `User.SecurityStamp` là mốc thời gian; mọi phiên tạo **trước** mốc đó bị coi là vô hiệu. Đổi mật khẩu chỉ cần đẩy mốc lên, không phải đi xoá từng dòng phiên.
- `LastSeenAt` chỉ cập nhật tối đa 5 phút một lần, để mỗi request không sinh một lệnh ghi.

Dùng SHA-256 trần cho token chứ không dùng Argon2 là có chủ đích: token do máy sinh, 256 bit entropy, không có gì để dò từ điển. Băm chậm ở đây chỉ làm mỗi request chậm thêm mà không tăng an toàn.

### 2.3 Nội dung học là YAML trong repo

`content/**/*.yaml` là nguồn sự thật, không phải bảng CMS. Lý do:

- Nội dung review được bằng pull request, diff đọc được bằng mắt.
- Cổng chất lượng (`ContentValidationTests`) chạy trong CI, chặn publish trước khi chạm DB.
- Seeder **upsert theo `Lesson.Code`**, nên nạp lại không tạo bản trùng và **không bao giờ xoá tiến độ học viên**. Đây là ràng buộc kiến trúc, có unique index trên `code` bảo đảm.

Admin CMS (Part 3) đọc, diff và publish YAML — **không** CRUD thẳng vào DB. Nếu cho CRUD thẳng, YAML và DB sẽ lệch nhau trong vòng một tuần.

### 2.4 Chấm phát âm chạy tại chỗ

Giọng học viên không rời máy chủ. Dịch vụ `speech` (Part 3) là container Python chạy faster-whisper + piper, **không expose ra ngoài**, chỉ `api` gọi được qua mạng nội bộ.

Vì sao Python: faster-whisper và piper là thư viện Python. Viết lại bằng .NET nghĩa là bọc `whisper.cpp` qua P/Invoke và mất phần g2p/chấm âm vị. Ràng buộc của dự án là "không dùng Python làm backend chính" — và .NET vẫn là backend chính; Python chỉ là một thành phần nội bộ chuyên biệt.

---

## 3. Ranh giới module

```
apps/api  ──►  src/Application  ──►  src/Domain
                     ▲
                     │ cài đặt abstraction
              src/Infrastructure
```

| Dự án | Chứa gì | Được tham chiếu ai |
|---|---|---|
| `Domain` | Entity, enum, quy tắc miền thuần | không ai |
| `Application` | Abstraction (`IPasswordHasher`, `IAuthDbContext`…), service nghiệp vụ | `Domain` |
| `Infrastructure` | EF Core, DbContext, cấu hình, mã hoá, migration | `Application`, `Domain` |
| `apps/api` | Endpoint, middleware, DI | cả ba |
| `src/Worker` | Job định kỳ | `Application`, `Infrastructure` |

**`Application` không tham chiếu `Infrastructure`.** Nó khai báo cái nó cần (`IAuthDbContext` chỉ lộ đúng 8 `DbSet` mà module xác thực dùng) và `AppDbContext` cài đặt interface đó. Kết quả: đổi cách lưu trữ không phải sửa nghiệp vụ, và test nghiệp vụ không cần dựng DB thật.

---

## 4. Luồng xác thực

```
1. POST /api/v1/auth/login  { email, password }
        │
        ├─ Không tìm thấy email → vẫn băm một lần với hash giả
        │                          (hai nhánh tốn thời gian tương đương)
        ├─ Sai mật khẩu        → FailedLoginCount++, đủ 8 lần thì khoá 15 phút
        └─ Đúng                → tạo Session
                                  ├─ token thô        → cookie efit_session (HttpOnly)
                                  ├─ SHA-256(token)   → DB
                                  └─ CsrfSecret       → cookie efit_csrf (đọc được bằng JS)

2. Mọi request tiếp theo
        SessionAuthenticationMiddleware
          → tra TokenHash → kiểm còn hạn, chưa thu hồi
          → kiểm Session.CreatedAt >= User.SecurityStamp
          → dựng ClaimsPrincipal (kèm vai trò)
          → đặt HttpContext.Items["session_csrf"]

3. Request POST/PUT/PATCH/DELETE
        CsrfProtectionMiddleware
          → so header X-CSRF-Token với session_csrf (so sánh thời gian hằng định)
          → lệch hoặc thiếu → 403
```

**Thứ tự middleware bắt buộc**: dựng danh tính **trước**, kiểm CSRF **sau** — vì kiểm CSRF cần bí mật của phiên. Đảo thứ tự thì mọi request đều lọt.

Thông báo lỗi: email lạ, sai mật khẩu, và đang bị khoá đều trả **cùng một câu**. Trang đăng ký cũng trả về **y hệt** khi email đã tồn tại — nếu không, trang đăng ký thành công cụ dò xem ai đã có tài khoản.

### Hợp đồng frontend ↔ backend

| Việc | Cơ chế |
|---|---|
| Gửi cookie | `fetch(..., { credentials: 'include' })` trong `api-client.ts` |
| CSRF | Client đọc cookie `efit_csrf`, đặt vào header `X-CSRF-Token` cho mọi method thay đổi dữ liệu |
| Lỗi | Server trả `{ error, message }`; client bọc thành `ApiError` với `status`, `code`, `correlationId` |
| Truy vết | Server đặt `X-Correlation-Id` trên mọi phản hồi; client hiện mã này trên màn hình lỗi |
| Chưa đăng nhập | `GET /auth/me` trả 401 → hook trả `null` (không ném lỗi), router đẩy về `/login` |

---

## 5. Dữ liệu

43 bảng, chia bốn nhóm: **Identity** (7) · **Content** (13) · **Progress** (14) · **Ops** (9).

Quy ước áp cho toàn bộ:

- **Khoá chính GUID v7** — sortable theo thời gian, nên index không bị phân mảnh như GUID v4.
- **snake_case** cho tên bảng và cột, đặt tự động trong `OnModelCreating` — truy vấn tay trong `psql` không phải trích dẫn kép.
- **Enum lưu dạng chuỗi** — đọc dữ liệu thô không phải tra bảng số.
- **JSONB có converter tường minh** — không bật dynamic JSON của Npgsql, để mỗi cột JSONB phải được khai báo có chủ đích.
- **Concurrency token dùng cột hệ thống `xmin`** — xem mục 6.
- **Xoá mềm** trên `users`, `lessons`, `roleplay_scenarios`; unique index có `WHERE deleted_at IS NULL` nên xoá rồi tạo lại cùng mã là hợp lệ.
- **Ràng buộc CHECK ở tầng DB** cho mọi quy tắc sản phẩm quan trọng: `estimated_minutes BETWEEN 3 AND 12`, `interval_days BETWEEN 1 AND 60`, `lesson_id <> required_lesson_id`, … Chốt ở DB nghĩa là không seeder nào lách qua được.

Bảng nóng nhất là `lesson_mastery`: engine chống nhảy cóc đọc và ghi liên tục. Nó có unique `(user_id, lesson_id)` để hai request chấm điểm về cùng lúc không tạo hai dòng.

---

## 6. Hai cái bẫy đã gặp và cách xử lý

Ghi lại để không ai đạp lại.

### 6.1 `IsRowVersion()` trên Npgsql tạo cột thật

`IsRowVersion()` sinh ra một cột `row_version` NOT NULL mà không ai gán giá trị → **mọi lệnh INSERT hỏng với lỗi 23502**. Lỗi này chỉ lộ ra khi chạy thật với Postgres; migration và build đều xanh.

Cách đúng: ánh xạ vào cột hệ thống `xmin` mà Postgres đã có sẵn (`ConcurrencyTokenExtensions.UseXminAsConcurrencyToken`). Không tốn cột, không tốn trigger, luôn chính xác.

### 6.2 Npgsql 10 không map `string` sang `inet`

Cột IP phải khai `varchar(45)` (đủ cho IPv6). Cột này chỉ dùng để audit, không truy vấn theo dải mạng, nên varchar là đủ.

---

## 7. Vận hành

| Việc | Cách làm | Đánh đổi |
|---|---|---|
| Migration | Chạy tự động lúc API khởi động | Một lệnh, không quên bước. Nhưng **chỉ được chạy một bản sao API** lúc nâng cấp |
| Log | Serilog JSON ra stdout, Docker gom | Không cần sink file, không cần xoay vòng log |
| Truy vết | `CorrelationIdMiddleware` tôn trọng header từ proxy | Một request qua nhiều tầng giữ cùng một mã |
| Rate limit | Theo user khi đã đăng nhập, theo IP khi chưa | Nhiều người sau cùng một NAT văn phòng không làm nhau bị chặn |
| Health | `/health/live` (không chạm DB) · `/health/ready` (có chạm DB) · `/health/startup` | Mất DB thì rút khỏi cân bằng tải, **không** khởi động lại app |

Ba endpoint health có ba mục đích khác nhau. Trộn chúng làm một là nguyên nhân kinh điển khiến container bị giết oan trong lúc chạy migration.

---

## 8. Việc chưa quyết

1. **Nhiều bản sao API.** Migration lúc khởi động chỉ an toàn với một bản sao. Muốn scale ngang phải tách migration thành job riêng hoặc dùng advisory lock.
2. **Lưu file ghi âm.** Hiện là bind mount `./media`. Nhiều VM sẽ cần object storage.
3. **Ngưỡng điểm từng kỹ năng.** Giá trị khởi điểm, chưa hiệu chỉnh trên học viên thật.
