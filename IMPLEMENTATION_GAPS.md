# Những gì chưa hoàn thiện

Ngày: 2026-08-19 · Phạm vi: kết thúc **Part 2**

Tài liệu này liệt kê trung thực cái gì đã chạy thật, cái gì mới là vỏ, và cái gì chưa động tới.
Mục đích là để không ai đọc README rồi tưởng hệ thống đã dạy được học viên.

---

## 1. Đã chạy thật và đã kiểm chứng

Kiểm chứng bằng cách chạy stack trên Docker Desktop và gọi thật qua proxy, không phải bằng suy luận.

| Hạng mục | Bằng chứng |
|---|---|
| Monorepo 8 dự án .NET + web | `dotnet build` xanh, `npm run build` xanh |
| 43 bảng + migration | Apply thật lên Postgres 16, `information_schema` đếm được 44 bảng (43 + lịch sử migration) |
| Đăng ký | `POST /auth/register` trả 200; đăng ký trùng email trả **y hệt** |
| Đăng nhập | Đặt đúng 2 cookie; `efit_session` có `HttpOnly`, `efit_csrf` không |
| Chống dò tài khoản | Email lạ và sai mật khẩu trả cùng một JSON |
| CSRF | POST thiếu header → 403 `csrf_failed`; có header đúng → qua middleware |
| Phiên | `GET /auth/me` trả đúng danh tính và vai trò `Learner` |
| Dashboard API | `GET /learning/dashboard` trả dữ liệu thật từ DB (số 0 cho tài khoản mới) |
| Health | `/health/ready` trả `{"status":"ready"}` qua Caddy |
| SPA qua proxy | Trang chủ và `/login` render đúng; đăng nhập trên trình duyệt chuyển sang `/learn` |
| Unit test | 176 test xanh: 115 đơn vị, 31 cổng nội dung, 30 tích hợp |

---

## 2. Mới là vỏ, chưa có nghiệp vụ

| Hạng mục | Hiện trạng | Thiếu gì |
|---|---|---|
| **Khu quản trị** | API đã có (tổng quan, mail, nạp lại nội dung, audit, trạng thái AI); giao diện vẫn là màn giữ chỗ | Màn quản lý nội dung, xem DAG, diff và publish |
| **Mốc nghề nghiệp** | Tính theo số bài đã thạo | Điều kiện thật: điểm nói, bài viết đạt, roleplay không dùng gợi ý |
| **Điều hướng Nghe/Nói/Đọc/Viết** | Có trên thanh bên | Chưa có trang tương ứng, bấm vào sẽ không khớp route |

---

## 3. Chưa động tới

Theo thứ tự nên làm:

**Part 2 đã đóng** (19/08/2026). Sáu mục của nó nay nằm ở phần 6b, 6c và 6d.
Còn nợ lại trong phạm vi Part 2: **audio sinh sẵn** (bước Nghe hiện cho mở lời thoại thay thế).

**Part 3 — kỹ năng và vận hành**
7. Dịch vụ nhận dạng giọng nói (container Python): faster-whisper, piper, g2p, chấm 3 trục, nhận xét tiếng Việt
8. Chấm viết bằng luật: điền chỗ trống, sắp câu, email có hướng dẫn
9. Roleplay: graph 5–7 node mỗi kịch bản, 7 kịch bản
10. Ôn tập giãn cách, streak, thông báo
11. Mail: Microsoft Graph và SMTP, outbox, gửi thư thử
12. AI 6 nhà cung cấp: cache, hạ cấp theo ngân sách, fallback không cần AI
13. Admin CMS: quản lý nội dung, xem DAG, diff, publish
14. Worker: outbox, nhắc học, streak, dọn dẹp

---

## 4. Nợ kỹ thuật đã biết

### 4.1 Chưa xác minh dark mode bằng mắt

CSS đã kiểm bằng cách đọc file được phục vụ: có đúng một rule `body`, dùng biến, có khối `.dark`, không có `prefers-color-scheme` nào.
Một phần tử tạo mới resolve `var(--surface-base)` ra đúng màu tối.

Nhưng **không chụp được màn hình để xác nhận bằng mắt**: trình duyệt nhúng của công cụ không composite khung hình,
và `getComputedStyle` cho `html`/`body` trả về giá trị đóng băng — kể cả khi ép `background-color: red !important`
nó vẫn trả màu cũ. Đó là giới hạn của công cụ, không phải lỗi CSS, nhưng **cần mở bằng Chrome thật để xác nhận** trước khi phát hành.

Đã thêm nền lên `html` ngoài `body` để nguồn lan truyền màu canvas là cố định — đúng thực hành bất kể chuyện trên.

### 4.2 Migration chạy lúc khởi động

Chỉ an toàn khi có **một** bản sao API. Muốn scale ngang phải tách migration thành job riêng hoặc dùng advisory lock của Postgres.
Ghi rõ trong `AZURE_VM_DEPLOYMENT.md`.

### 4.3 `AspNetCore.HealthChecks.NpgSql` còn ở 9.0.0

Chưa có bản cho .NET 10. Hiện chưa dùng tới nó (health check tự viết bằng `SELECT 1`), nhưng gói vẫn nằm trong dự án.
Nên gỡ hoặc chờ bản 10.

### 4.4 Cảnh báo NU1510

`Microsoft.Extensions.Diagnostics.HealthChecks` được framework cung cấp sẵn, khai thêm là thừa. Nên gỡ.

### 4.5 Cảnh báo query filter của EF

EF cảnh báo về quan hệ bắt buộc trỏ tới entity có xoá mềm (`User`, `Lesson`, `RoleplayScenario`).
Hiện chấp nhận được vì chưa xoá mềm ai trong thực tế, nhưng khi bắt đầu xoá mềm phải rà lại từng quan hệ,
nếu không sẽ có bản ghi con mồ côi mà truy vấn không thấy.

### 4.6 Test tích hợp — đã trả nợ (19/08/2026)

`IntegrationTests` nay có **30 test** chạy trên Postgres thật qua Testcontainers: dựng nguyên API bằng
`WebApplicationFactory`, chạy migration và seed đúng 58 file nội dung của production, rồi gọi qua HTTP.

Dùng DB thật chứ không phải DB trong bộ nhớ vì những thứ hay hỏng nhất ở tầng này chỉ tồn tại với
Postgres: migration, cột jsonb, enum lưu dạng chuỗi, query filter xoá mềm, ràng buộc khoá ngoại.
Cả hai lần seeder làm sập API đều **không** lỗi trên DB trong bộ nhớ.

| Nhóm | Phủ cái gì |
|---|---|
| Xác thực (7) | Chống dò tài khoản, cờ trên hai cookie, CSRF, phiên bị thu hồi sau khi đăng xuất |
| Nội dung (5) | 58 bài · 406 bước · 570 câu · 2 đề vào DB; chạy lại seeder giữ nguyên Id; đáp án không dồn một vị trí |
| Vòng học (7) | Đáp án không rời máy chủ; lộ trình đúng thứ tự bậc; chấm tại máy chủ; học trọn bài rồi mở khoá theo đồ thị |
| Xếp lớp và thi vượt (11) | Phục vụ 22 câu không kèm đáp án; mở lại lượt dở; thi lại bốc đề khác; khoảng chờ sau khi trượt; qua thì bỏ qua tiên quyết |

Còn thiếu: chưa phủ đường đổi mật khẩu và chưa có test cho phần quản trị (khu đó vẫn là vỏ).

### 4.7 CI — đã trả nợ (19/08/2026)

`.github/workflows/ci.yml` chạy trên mọi pull request, ba job song song: **backend** (build Release,
cổng nội dung, test đơn vị), **integration** (Testcontainers), **web** (lint, typecheck, build).
Đã chạy đủ bốn bước tại máy ở cấu hình Release trước khi commit.

---

## 5. Nợ về nguồn nội dung

Chi tiết trong [`docs/content-sources.md`](docs/content-sources.md).

1. **NIST SP 800-61 r3 chưa trích được từng trường biểu mẫu** — PDF không parse được bằng công cụ hiện có. Danh sách sáu giai đoạn lấy từ trang mô tả chính thức, chưa đối chiếu với biểu mẫu trong tài liệu.
2. **English Vocabulary Profile chưa tải về dạng máy đọc được** — mới có con số tổng theo bậc (A1 784, A2 1594, B1 2937), chưa có danh sách từ để cổng validate tự kiểm bài có dùng từ vượt bậc hay không.
3. **ITIL 4 dùng nguồn diễn giải** — glossary gốc của Axelos nằm sau tường phí. Định nghĩa đã đối chiếu chéo hai nguồn nhưng vẫn phải ghi chú là nguồn cấp C.
4. **Chưa có nguồn cho ngôn ngữ hội thoại vendor và CAB** — hai tình huống này dựa trên ITIL và kinh nghiệm ngành, chưa có tài liệu mô tả ngôn ngữ chuẩn.

---

## 6. Quyết định đã chốt (2026-08-18)

Đã ghi vào code tại `src/Application/Learning/LearningPolicyOptions.cs` và `apps/api/appsettings.json`.

| Quyết định | Giá trị | Nơi thi hành |
|---|---|---|
| Mục tiêu phút mỗi ngày | **45** | `DailyMinutesTarget`, mặc định của `UserProfile` |
| Ngày tính chuỗi | Đủ mục tiêu phút **và** chạm đủ bốn kỹ năng | `StreakRequiresDailyTarget`, `StreakRequiresAllFourSkills` |
| Giữ file ghi âm | **45 ngày**, chỉ xoá file, giữ lại bản ghi điểm | `SpeechAudioRetentionDays` |
| Thứ tự soạn tầng Chuyên môn | Helpdesk → Infrastructure → Security → Cloud → AI → Reading | `docs/curriculum.md` |

Hệ quả đã hiện trên giao diện: bảng điều khiển ở chế độ đơn kỹ năng cảnh báo cả hai điều — không lên bậc được, và chuỗi ngày không tăng.

Job dọn dẹp thi hành mốc 45 ngày **chưa viết** (thuộc `src/Worker`, Part 3). Tới lúc đó, file ghi âm sẽ tích luỹ vô thời hạn.

## 6b. Part 2 — đang làm

**Đã xong và đã kiểm chứng trên Docker:**

| Hạng mục | Bằng chứng |
|---|---|
| Schema nội dung YAML | `LessonDocument`, 58 file bài thật trong `content/lessons/` |
| Cổng chất lượng | `LessonValidator`, 21 quy tắc, 21 test xanh, chạy không cần DB |
| Bộ đọc YAML | `YamlContentLoader`, hash chuẩn hoá xuống dòng nên Windows và Linux ra cùng giá trị |
| Seeder | Nạp 58 bài, 406 bước học, 89 cạnh tiên quyết. Chạy lại báo "không đổi", không đụng dữ liệu |
| Khớp hình minh hoạ | `IllustrationCoverageTests` so danh mục máy chủ với bảng tra TSX — khoá lệch sẽ làm bài rơi về hình mặc định mà không báo lỗi nào |
| Màn ôn tập giãn cách | `ReviewService` + `/learn/review`. Chạy thật trên 16 câu của tài khoản kiểm thử: đúng thì 2 ngày giãn thành 5 và ease lên 2.60, sai thì về 1 ngày và ease xuống 2.35. Mastery của bài **không đổi** sau khi ôn sai. 8 test đơn vị |
| Bài xếp lớp | `PlacementService` + `/placement`. Hai đề 26 câu, **phục vụ 22** (xem 6c). Chạy trọn hai lượt qua trình duyệt: làm đúng hết ra L4 tầng Chuyên môn; hồ sơ nghe yếu ra L1 dù điểm chung ở mức L2. Thi lại bốc đúng đề còn lại. 21 test đơn vị, 8 test cổng chất lượng |
| Engine chống nhảy cóc | `PrerequisiteEngine`, 16 test xanh: ngưỡng riêng từng kỹ năng, tiên quyết cứng/mềm, xem trước, thi vượt, suy giảm theo thời gian |
| Chọn bài kế | Dashboard trả LIFE-01 kèm lý do sinh từ engine, hiển thị đúng trên giao diện |

**Vòng học lõi đã khép kín** — kiểm chứng bằng cách học trọn một bài qua giao diện thật trên Docker:

| Bước | Kết quả thật |
|---|---|
| Sinh `lesson_items` | 8–9 item mỗi bài, upsert theo mã |
| Màn học 7 bước | Nghe → Từ vựng → Nhắc lại → Nói → Đọc → Viết → Kiểm tra |
| Chấm trắc nghiệm | Máy chủ chấm, client không bao giờ thấy đáp án trước khi nộp |
| Chấm viết bằng luật | Ba dạng: điền chỗ trống, sắp câu, email có hướng dẫn |
| Chốt bài | LIFE-02 đạt 100 → `Mastered`, ghi `lesson_state_events` |
| Mở khoá dây chuyền | LIFE-03 chuyển `Available`; LIFE-04 vẫn khoá kèm con số cụ thể |
| Ôn tập giãn cách | 8 câu vào hàng đợi, khoảng cách nhân hệ số giãn, trần 60 ngày |
| Dự báo | 2 ngày còn lại, tính từ nhịp học thật (20 phút trong 7 ngày) |

**Nợ đã trả:** seeder nay upsert bước học theo `(lesson_id, order_index)` thay vì xoá rồi tạo lại, nên sửa nội dung không còn cascade xoá hàng đợi ôn tập của học viên.

**Nội dung hiện có: 58 bài — đủ cả ba tầng** (18/08/2026).
Trong DB đo được: 406 bước học, 570 câu hỏi, 89 cạnh tiên quyết.
Cả 58 bài qua cổng chất lượng và mang một khoá hình minh hoạ riêng.

| Nhánh | Số bài | Mã |
|---|---|---|
| Đời sống | 16 | LIFE-01 → LIFE-16, PreA1 lên A2 |
| Văn phòng | 12 | OFF-01 → OFF-12, A2 lên B1 |
| Helpdesk | 5 | HD-01 → HD-05 |
| Hạ tầng | 7 | INF-01 → INF-07 |
| Bảo mật | 4 | SEC-01 → SEC-04 |
| Cloud | 6 | CLD-01 → CLD-06 |
| AI | 4 | AI-01 → AI-04 |
| Đọc chuyên sâu | 4 | RD-01 → RD-04 |

Thứ tự nhánh chuyên môn bám đúng đề xuất đã chốt: Helpdesk → Hạ tầng → Bảo mật → Cloud → AI → Đọc.

**Chưa làm:** audio sinh sẵn (bước Nghe hiện cho mở lời thoại thay thế).

### Ba chỗ hiện chưa chấm được, và app nói thẳng điều đó

1. **Kỹ năng Nói chưa có bộ chấm.** Bước Nhắc lại và Nói trả `graded: false`, **không** ghi bản ghi điểm, và **không** tính vào mastery. Màn học hiện cảnh báo rõ; kết quả cuối bài cũng ghi "kỹ năng Nói chưa được chấm ở bản này". Cho điểm 100 vì không chấm nổi là nói dối học viên rằng họ đã thạo.
2. **Audio chưa sinh.** Bước Nghe hiện cho mở lời thoại để vẫn làm được câu hỏi. Đây là giải pháp tạm, không phải thiết kế.
3. **Bước từ vựng chấm 100 khi xem xong.** Đây là đánh giá thật chứ không phải điểm bịa: mục tiêu của bước đó đúng là tiếp xúc với từ mới.

**Nợ này đã trả** (19/08/2026): seeder và luồng học nay có test tích hợp chạy trên Postgres thật — xem mục 4.6.

## 6c. Bài xếp lớp — soạn 26 câu, phục vụ 22

Hai đề song song trong `content/placement/`, mỗi đề đúng 26 câu: Nghe 6 · Nói 4 · Đọc 6 · Viết 6 ·
Từ vựng–ngữ pháp 2 · Likert 2.

**Bốn câu Nói hiện không được phục vụ.** Chưa có dịch vụ chấm phát âm, và một điểm nói tự khai
thì tệ hơn không có điểm nào. Nội dung đã soạn sẵn và nằm trong DB; bật
`LearningPolicy:PlacementSpeakingEnabled` là chúng xuất hiện, không phải soạn lại đề.
Khi tắt, trục Nói báo **"chưa đo được"** chứ không bị tính 0 — tính 0 sẽ kéo mọi học viên xuống một bậc
bằng một phép đo không tồn tại.

Phần Nghe dùng giọng của trình duyệt, giống bước Nghe trong màn học (xem `use-speech.ts`).
Cùng một giải pháp tạm, không tạo tiền lệ mới.

**Luật xếp bậc.** Điểm chung quy ra L0–L4, nhưng **bậc không được vượt bậc của trục yếu nhất quá một mức**.
Người đọc viết tốt mà nghe không nổi vào thẳng L4 sẽ gặp bài đầu tiên đã không theo được rồi bỏ.
Kết quả hiện thẳng lý do hạ bậc kèm tên trục và con số.

L0 và L1 cùng ánh xạ về `PreA1` vì enum không có mức nào thấp hơn; khác biệt nằm ở tầng đề xuất
và câu giải thích, không ở bậc engine đọc.

| Kiểm chứng trên Docker | Kết quả |
|---|---|
| Làm đúng 22/22 qua giao diện | L4, tầng Chuyên môn, Nghe/Đọc/Viết 100, Nói "chưa đo được" |
| Hồ sơ nghe yếu (Nghe 17, Đọc 29, Viết 100) | Điểm chung 48.6 ở mức L2 nhưng xếp **L1**, kèm câu nêu đúng trục Nghe |
| Thi lại sau khi nộp đề A | Bốc đúng đề B |
| Bỏ dở rồi gọi `/start` lại | Mở lại đúng lượt cũ, trả về mã các câu đã làm |
| Câu của đề khác, câu Nói, lượt lạ | Cùng một 404 với cùng một câu thông báo |
| POST thiếu header CSRF | 403 |
| Hồ sơ học sau khi nộp | `current_level` và `current_layer` ghi theo kết quả |

**Hệ quả lớn nhất không nằm ở màn xếp lớp.** Bảng điều khiển trả sớm sang màn mời thi khi
`placementCompleted` là false, nên trước đây toàn bộ dãy ô thống kê, chuỗi ngày, bảng bốn kỹ năng và
mốc nghề nghiệp đều đã dựng xong mà không ai xem được. Nộp xong bài xếp lớp là chúng hiện ra.

## 6d. Thi vượt và màn lộ trình — Part 2 đã đóng

**Thi vượt** (`ChallengeService`, `/learn/lesson/{code}/challenge`) cho học viên qua một bài mà
không học tuần tự. Ba luật chống lách, cả ba đều có test:

| Luật | Vì sao |
|---|---|
| Ngưỡng 85, cao hơn ngưỡng học thường 80 | Bỏ qua cả bài giảng thì phải chứng minh nhiều hơn, nếu không thi vượt là đường vòng dễ hơn đường chính |
| Một trục kỹ năng hổng vẫn trượt | Cùng quy tắc `SkillsBelowThreshold` với học thường, gọi vào đúng một chỗ |
| Trượt thì chờ 12 tiếng | Một bài chỉ chừng mười câu; thi lại ngay là mời học viên dò đáp án cho tới khi trúng |

Bài dưới 6 câu chấm được thì không mở thi vượt. Câu Nói và câu Viết bị loại khỏi đề: một bên
chưa có bộ chấm, một bên chấm bằng rubric riêng chứ không theo chỉ số lựa chọn.

Qua rồi vẫn nợ ôn tập, nhưng khác học thường: câu đúng hẹn 7 ngày (họ vừa chứng minh biết rồi),
câu sai về 1 ngày. Trượt **không** làm trạng thái bài tệ đi — chỉ ghi một sự kiện để tính khoảng chờ.

| Kiểm chứng trên Docker | Kết quả |
|---|---|
| SEC-01, đúng 6/9 | 66.7 điểm, trượt vì trục Nghe hổng, nêu đúng tên trục |
| Thi lại ngay, gọi thẳng API với đáp án đúng hết | Bị chặn, đề trả về 0 câu |
| CLD-06, đúng 12/13 | 92.3 điểm, qua. `unlocked_by_challenge = true`, có vé trong `challenge_passes` |
| Hàng đợi ôn sau khi qua | 12 câu đúng hẹn 7 ngày, 1 câu sai hẹn 1 ngày |
| Lộ trình sau khi qua | CLD-06 `Mastered` trong khi CLD-05 vẫn `Locked` — đúng nghĩa đường tắt |

**Màn lộ trình** (`/learn/roadmap`) trước đây vẫn là màn giữ chỗ dù API đã xong từ lâu. Nay hiện
58 bài theo ba tầng, mỗi bài khoá kèm con số còn thiếu, và mỗi bài chưa thạo đều có nút thi vượt.
Đã gỡ hai component giữ chỗ nay không ai dùng — một trong hai trùng tên `LessonPlayerPage` với
component thật, để lại là bẫy cho lần sửa sau.

## 6e. Part 3 đợt 1 — vận hành (19/08/2026)

`src/Worker` trước đây là template mặc định in "Worker running at" mỗi giây. Nay chạy thật:

| Job | Nhịp | Việc |
|---|---|---|
| Hộp thư đi | mỗi phút | Gửi thư chờ, lùi lịch 1-5-25-125 phút khi hỏng, bỏ cuộc sau 5 lần |
| Nhắc học | mỗi giờ | Nhắc theo giờ địa phương từng người; nhắc ôn khi có câu tới hạn |
| Chuỗi ngày | mỗi giờ | Tiêu vé nghỉ hoặc báo đứt chuỗi ngay sau nửa đêm địa phương; cấp vé thứ Hai |
| Dọn ghi âm | mỗi ngày | Thi hành mốc 45 ngày đã chốt — chỉ xoá file, giữ nguyên dòng điểm |

Chạy mỗi giờ chứ không mỗi ngày vì "20 giờ tối" của mỗi học viên rơi vào một giờ UTC khác nhau.

**Chuỗi ngày trước đây là ô chết.** Dòng `streaks` được tạo lúc đăng ký rồi không ai cập nhật,
nên bảng điều khiển luôn hiện 0. Nay `StreakService` tính lại sau mỗi bước học, và màn hình
nói thẳng còn thiếu gì thay vì hiện số 0 im lặng.

**Gửi thư đã chạy thật**, không phải chỉ có mã: dựng Mailpit trong `docker-compose.dev.yml`,
cấu hình SMTP vào `mail_settings`, worker gửi và thư tới hộp đúng tiêu đề tiếng Việt.
Microsoft Graph vẫn chưa nối — chọn provider đó thì trả lỗi rõ ràng chứ không im lặng nuốt thư.

## 6f. Part 3 đợt 3-6 (19/08/2026)

### Roleplay — 7 kịch bản, chấm ba mức

`RoleplayService` + `/learn/roleplay`. Kịch bản là đồ thị có hướng trong YAML, cổng chất lượng
25 luật kiểm cả tính liên thông: node không ai tới được, đường không có kết thúc, lượt không có
lựa chọn nào đạt — đều chặn cứng.

Điểm khác mọi phần chấm khác: **ba mức thay vì đúng/sai**. `curt` là câu đúng ngữ pháp nhưng
cộc lốc — lỗi phổ biến nhất của kỹ sư Việt Nam nói tiếng Anh với đối tác, và là lý do phần này
tồn tại. Chấm nhị phân sẽ xoá mất đúng thứ cần dạy.

| Mã | Tình huống | Lượt |
|---|---|---|
| RP-01 | Gọi vendor support, đòi mốc thời gian | 10 |
| RP-02 | Standup ba phần | 7 |
| RP-03 | Xin duyệt thay đổi trước CAB | 9 |
| RP-04 | Bàn giao ca lúc 2 giờ sáng | 9 |
| RP-05 | Nhận ticket từ người dùng không rành kỹ thuật | 8 |
| RP-06 | Nói với sếp khi quá tải | 8 |
| RP-07 | Hỏi cloud vendor về giá và SLA | 8 |

Chạy thật: chọn toàn phương án cộc lốc trên RP-01 ra 62.5 điểm, kết thúc không thành công.

### AI — 6 nhà cung cấp, kiểm chứng bằng Ollama

`AiGateway` là cửa vào duy nhất, thứ tự cố định: cache → ngân sách → nhà cung cấp → fallback.
Nguyên tắc bao trùm là **app phải chạy được khi không có AI**.

Ba chế độ ngân sách để không bao giờ tắt đột ngột giữa tháng: dưới 70% chạy đủ, 70-90% hạ T2
xuống T1 và cache giữ lâu gấp đôi, trên 90% chỉ còn cache và fallback.

**Đã kiểm bằng model thật**: dựng Ollama trong compose dev với qwen2.5:0.5b. Lần gọi đầu 577ms
qua nhà cung cấp thật, lần hai 25ms từ cache, nội dung khớp. Năm nhà còn lại (Anthropic, OpenAI,
Gemini, OpenRouter, Azure) có client nhưng **chưa kiểm được** vì không có khoá API.

### Khu quản trị

`/api/v1/admin/*`: tổng quan kèm sức khoẻ nội dung (bài không có câu hỏi, câu ôn mồ côi,
phân bố vị trí đáp án), cấu hình gửi thư, nạp lại nội dung không cần khởi động lại, nhật ký
kiểm toán. Bí mật đi vào thì không đi ra — chỉ trả cờ có/không, không trả dạng che một phần.

### Chấm phát âm — đã chạy thật

`SpeechService` + container `onerahmet/openai-whisper-asr-webservice` chạy faster-whisper
model tiny.en. **Giọng học viên không rời máy chủ.**

Ba trục có trọng số khác nhau: truyền đạt 45%, phát âm 35%, trôi chảy 20% — nói sai vài âm
còn đỡ hơn nói thiếu ý.

**Giới hạn phải nói rõ: chấm ở mức TỪ, không phải âm vị.** Hệ thống biết học viên có nói ra
đúng từ hay không và với tốc độ nào, nhưng không biết họ phát âm /θ/ thành /t/ hay /s/.
Chấm âm vị cần bộ g2p và bước gióng hàng, chưa có.

Kiểm bằng file âm thanh thật sinh từ bộ đọc của Windows:

| Đọc | Bản ghi nhận được | Điểm |
|---|---|---|
| "I cannot access the shared folder" | đúng nguyên văn | 100/100/100 |
| "I access folder" | "I access folder." | 50/100/60, tổng 64.5, nêu đúng ba từ mất: cannot, the, shared |

## 6g. Part 3 đợt 7-8 (19/08/2026)

### Giao diện ghi âm trong màn học

Bước Nói giờ ghi âm thật. `useRecorder` bọc MediaRecorder và lo hai chỗ dễ sai: **tắt micro
sau khi ghi** (không gọi `track.stop()` thì đèn micro sáng mãi và học viên tưởng bị nghe lén)
và **thu hồi URL tạm** (mỗi lần ghi lại mà không revoke thì blob cũ nằm lại tới khi đóng tab).

Ở tổng kết đợt trước tôi ghi rằng micro cần HTTPS nên bản dev không dùng được. **Sai** —
trình duyệt coi localhost là ngữ cảnh an toàn, `getUserMedia` chạy bình thường trên HTTP localhost.

Chấm từng câu chứ không gộp cả bước, vì nhận xét chỉ hữu ích khi gắn với đúng câu vừa đọc.
Nút đi tiếp không đòi phải chấm hết: micro hỏng hay phòng ồn không được phép khoá cả bài học.

### Khu quản trị — giao diện

Năm tab: tổng quan, nội dung, gửi thư, AI, nhật ký. Tải cả bốn nguồn một lần rồi chia sang
các tab, vì người vận hành hay nhảy qua lại khi dò sự cố.

Kiểm chạy thật trên app:

| Việc | Kết quả |
|---|---|
| Nạp lại nội dung từ YAML | 58 bài, 2 đề, 7 kịch bản — đều "không đổi", phát hiện hash chạy đúng |
| Lưu cấu hình SMTP rồi gửi thư thử | Worker gửi trong dưới 5 giây, Mailpit nhận được |
| Đổi cổng 1025→2525, bỏ trống ô mật khẩu | Mật khẩu giữ nguyên, mã hoá 69 ký tự, API không trả về |
| Nhật ký kiểm toán | Chỉ ghi tên việc và cờ, không ghi giá trị |

## 6h. Part 3 đợt 9 (19/08/2026) — tra cứu nội dung và đồ thị tiên quyết

Màn này **chỉ đọc, cố ý**. Nguồn sự thật của nội dung là file YAML; seeder upsert theo mã bài và
bỏ qua bài còn nguyên hash. Cho sửa bài thẳng vào DB thì lần nạp lại kế tiếp ghi đè mất mà không
báo gì — nên biên tập vẫn đi qua file, còn màn này lo phần file không cho thấy được: hình dạng
của cả kho bài khi ghép lại.

### Hình dạng thật của lộ trình

`GET /api/v1/admin/content/graph` trả nút, cạnh và các số chỉ tính được khi nhìn cả đồ thị:

| Số đo | Giá trị |
|---|---|
| Bài / cạnh tiên quyết | 58 / 89 (66 cứng, 23 mềm) |
| Đường dài nhất | 43 bậc, phủ 74% kho bài |
| Bậc 0 đến 30 | mỗi bậc đúng một bài |
| Mở ngày đầu | LIFE-01, LIFE-02 |

Nghĩa là lộ trình gần như một **đường thẳng**, không phải cây phân nhánh. Đây không phải lỗi:
người mất gốc cần thứ tự cố định, không có ngã rẽ để đi sai. Cái giá là ai chỉ cần tiếng Anh mảng
Cloud vẫn phải cày hết phần đời sống trước. Đó là quyết định nội dung, nên báo ở mức *info*.

Bản đầu của tôi gắn cờ từng bài chặn quá một phần tư kho bài, và bắn **34 cảnh báo trên 58 bài**.
Đúng số học nhưng vô dụng — trong chuỗi thẳng thì bài nào cũng chặn hết phần đuôi, nên xếp hạng
từng nút chỉ là xếp hạng vị trí trong hàng. Đã đổi thành một chẩn đoán ở mức cả đồ thị.

## 6i. Part 3 đợt 10 (19/08/2026) — chặn nhảy cóc, xếp lớp thành cổng vào

Ba thay đổi đi cùng nhau. Bỏ một cái là hai cái kia hỏng nghĩa.

### A. Thi vượt không còn mở khoá bài sau

Trước: qua bài thi vượt ghi mastery 85, thừa mức 65–70 mà cổng đòi, nên bài kế tiếp mở ngay.
Một người có thể đi hết chuỗi bài chỉ bằng trắc nghiệm, không học bài nào.

Nay thi vượt chỉ có nghĩa **miễn học bài đó**. Cờ `UnlockedByChallenge` (vốn đã có trên
`LessonMastery` nhưng chưa ai đọc) giờ được engine xét: điểm có từ thi vượt tính là 0 khi xét
tiên quyết. Học thật sau đó sẽ xoá cờ, nếu không người từng thi vượt rồi quay lại học tử tế
sẽ kẹt vĩnh viễn.

Câu giải thích khoá phải nói riêng ca này. "Bạn đang có 0 điểm" về một bài đang hiện là đã thạo
thì người đọc kết luận hệ thống đếm sai, chứ không hiểu là mình cần học bài đó thật.

Giao diện phải hiện khác bài học thật, nếu không hai câu trên màn hình chọi nhau. Bài đánh dấu
mang huy hiệu **"Đã đánh dấu biết"** với biểu tượng riêng, kèm câu nói thẳng đường tắt này không
mở được bài sau, và **không tính vào con số "đã thạo"** ở ô tóm tắt lẫn ở từng tầng.

Quan trọng nhất: bài đánh dấu **vẫn giữ nút học** (`Học để mở bài sau`). Trước đó mọi bài
`Mastered` đều bị ẩn nút này, nên bài vừa thi vượt rơi vào ngõ cụt — hệ thống bảo "cần học bài đó
rồi mới mở bài sau" mà không còn chỗ nào bấm để học.

Huy hiệu mới lộ ra một lỗi tiếp cận có sẵn: ba tông ngữ nghĩa của `Badge` dùng **cùng một màu
cho cả chữ lẫn nền pha loãng**, nên trên nền sáng chỉ còn success 2.59, warning 1.98, danger
3.53 — dưới mức 4.5 mà chữ 12px cần. Tông `brand` vốn đã làm đúng (`text-brand-700` trên
`bg-brand-50`); ba tông kia bị bỏ quên. Thêm token `--color-*-text` đậm hơn cho nền sáng, và
khối `.dark` trả về bản gốc vì nền tối vốn đã đạt 5.35. Đo lại trên phần tử thật: sáng 4.88,
tối 5.35. Ảnh hưởng 3 huy hiệu có sẵn ở trang ôn tập và nhập vai, không riêng huy hiệu mới.

### B. Xếp lớp thành cổng vào theo bậc

`LessonStateReason.PlacementUnlock` đã nằm trong enum từ đầu nhưng chưa nơi nào dùng. Nay thi
xếp lớp đạt bậc nào thì được ghi công **mọi bài bậc thấp hơn** — thấp hơn, không phải thấp hơn
hoặc bằng. Vì `CefrLevel` dừng ở B1 nên tầng Professional (đều B1) không bao giờ tự mở.

Đo thật: đạt B1 ghi công đúng 20 bài (PreA1 5, A1 5, A2 10), học viên vào thẳng OFF-05.

Điểm từng kỹ năng để trống, cố ý: bài xếp lớp đo trình độ chung chứ không đo từng bài, bịa ra
điểm Nghe/Nói cho mỗi bài là nói dối học viên.

### C. Các track chuyên môn chạy song song

Trước: `Foundation → HD (5 bài) → INF (7 bài) → CLD/SEC`, còn AI và Reading treo vào giữa.
Muốn học tiếng Anh Cloud phải qua đủ Helpdesk và Infrastructure — không có lý do sư phạm nào.

Nay mọi track chuyên môn mở khoá bằng checkpoint OFF-12; quan hệ giữa các track chuyên môn hạ
xuống mềm (gợi ý, không khoá). Xong Foundation là sáu nhánh mở cùng lúc.

| Số đo | Trước | Sau |
|---|---|---|
| Đường dài nhất | 43 bậc | 34 bậc |
| Phủ bao nhiêu kho bài | 74% | 59% |
| Cạnh cứng bắt qua track | 9, nối chuỗi | 7, đều trỏ về Foundation |

## 7. Lỗi đã sửa sau Part 1

| Lỗi | Nguyên nhân | Đã sửa |
|---|---|---|
| Đã đăng nhập nhưng bấm mục nào cũng bị hỏi đăng ký | Các đường dẫn `/learn/*` chưa khai route, rơi vào catch-all `<Navigate to="/" />` là trang tiếp thị | Khai đủ 9 route con trong khung học viên; catch-all nay phân biệt theo trạng thái đăng nhập; đường dẫn lạ trong khu học viên về bảng điều khiển |
| Trang chủ không có mảng đời sống và văn phòng | Chỉ quảng bá năm nhánh chuyên môn | Thêm khối "Ba tầng" đặt trước năm nhánh nghề, có minh hoạ và câu mẫu thật |
| API không khởi động được, lặp vô hạn | Seeder lưu làm hai lượt; giữa hai lượt EF so nhầm concurrency token của `lessons` | Bỏ token khỏi bảng nội dung (chỉ có một người ghi là seeder), và gộp về **một** lượt lưu duy nhất — khoá chính là GUID v7 sinh phía client nên không cần lưu trước để lấy Id |
| Lệnh DELETE thừa khớp 0 dòng | `lesson.Activities.Clear()` trên navigation đã nạp khiến EF phát sinh DELETE cho những dòng mà `ExecuteDelete` vừa xoá | Không `Include(Activities)` nữa; đếm bước học bằng truy vấn riêng |
| Bài có hash đúng nhưng không có bước học nào | Seed crash giữa hai lượt lưu để lại trạng thái nửa vời, lần chạy sau thấy hash khớp nên bỏ qua | Điều kiện bỏ qua nay đòi cả hash khớp **và** có bước học, nên lần chạy sau tự chữa |
| Học viên kẹt ở màn xếp lớp, không đi tiếp được | Dashboard chặn cứng khi chưa xếp lớp, mà bài xếp lớp chưa làm | Thêm lối đi thứ hai "bắt đầu luôn từ số 0" — với người mất gốc, kết quả xếp lớp gần như chắc chắn là bài đầu tiên |
| Rò đáp án trong payload bước Nghe/Đọc/Quiz | Ghi nguyên đối tượng câu hỏi (kèm `answer`) vào `payload_json` | Câu hỏi chỉ sống trong `lesson_items`; chỉ cột `prompt` được map ra DTO. Đã kiểm: phản hồi không còn chuỗi `answer` nào |
| Mọi câu trắc nghiệm đều bị chấm sai | Seeder ghi `{"Answer":0}` (PascalCase), bộ chấm đọc `"answer"` — `JsonDocument` phân biệt hoa thường nên luôn trả -1, **không ném lỗi nào** | Tra tên thuộc tính không phân biệt hoa thường, có test khoá lại |
| Engine và màn chấm bài kết luận ngược nhau | Player loại kỹ năng chưa chấm được, engine thì không, nên bài vừa "thạo" lại hiện "chưa thạo" | Đưa quy tắc về một chỗ duy nhất trong `SkillsBelowThreshold`: kỹ năng chưa có điểm thì bỏ qua, không tính là yếu |
| `confrim` bị 0 điểm dù chỉ gõ đảo hai chữ | Levenshtein tính đảo ký tự thành 2 lỗi, vượt ngưỡng chấp nhận | Đổi sang Damerau-Levenshtein: đảo ký tự liền nhau tính 1 lỗi |
| Lộ trình hiện sai thứ tự, LIFE-06 đứng trước LIFE-01 | Cột `level` lưu dạng chuỗi nên `ORDER BY` của Postgres sắp theo bảng chữ cái: "A1" < "A2" < "PreA1" | Sắp xếp trong bộ nhớ theo giá trị enum thay vì trong truy vấn |
| Đề xếp lớp hiện trắng phần câu hỏi, không báo lỗi nào | Seeder ghi `prompt_json` dạng PascalCase, client đọc camelCase | Seeder ghi camelCase để khớp mọi trường khác của API; `PlacementService` dùng chung một bộ tuỳ chọn JSON |
| Seed đề lần thứ hai làm sập API lúc khởi động | Thêm câu qua `form.Items` của một form đang tracked: khoá là GUID v7 sinh phía client nên EF đánh dấu Modified thay vì Added, sinh UPDATE khớp 0 dòng | Thêm thẳng vào `db.PlacementFormItems`, xoá bằng `ExecuteDelete`, và bọc lượt seed trong try/catch để đề hỏng không làm sập app |
| Ai chọn ô đầu tiên cũng qua được phần trắc nghiệm | Cả 28 câu trắc nghiệm của hai đề đều có đáp án đúng ở vị trí 0 | Trải đáp án ra bốn vị trí; thêm luật cổng chất lượng P025 chặn khi quá nửa số câu dồn về một vị trí, có test chứng minh luật bắt được |
| Câu cuối bị tính 0 điểm khi bấm Nộp bài ngay sau khi trả lời | `commit()` bắn request lưu rồi `onSubmit()` chạy song song, máy chủ chấm trước khi câu cuối tới nơi | Nút Nộp bài `await` câu cuối lưu xong mới nộp |
| Chọn mãi ô giữa là qua được phần lớn giáo trình | 451 trên 570 câu (79%) có đáp án ở vị trí 1; 10 bài dồn hết vào một vị trí. Thi vượt biến chuyện này thành đường tắt thật sự | Trải đáp án ra ba vị trí (nay 36/30/34); thêm luật cổng chất lượng E072 chặn khi quá nửa số câu của một bài dồn về một vị trí |
| `Map` của lucide-react che mất `Map` của JavaScript | Import icon cùng tên với hàm dựng có sẵn, ngay trên chỗ dùng `new Map()` | Đổi tên khi import thành `MapIcon` |
| Mọi múi giờ rơi về UTC trong im lặng | `Directory.Build.props` bật `InvariantGlobalization` cho cả solution; ở chế độ đó .NET chỉ biết UTC và `FindSystemTimeZoneById` trả về UTC mà **không ném lỗi**. Nhắc học 20 giờ sẽ gửi vào 3 giờ sáng giờ Việt Nam | Tắt chế độ đó; thêm `libicu74` và `tzdata` vào ảnh API lẫn worker; gom việc đổi giờ về `LocalDay` thay vì cộng cứng 7 tiếng ở từng chỗ |
| Chuỗi ngày luôn bằng 0 | Dòng `streaks` tạo lúc đăng ký nhưng không nơi nào cập nhật | `StreakService` tính lại sau mỗi bước học |
| Không ai xây được chuỗi ngày | Luật đòi chạm đủ bốn kỹ năng, nhưng bước Nói chưa chấm được nên không ghi bản ghi nào — trục Nói vĩnh viễn trống | Tách "đã làm" khỏi "có điểm" bằng cột `graded`; mọi truy vấn tính điểm lọc `graded = true` |
| Truy vấn theo ngày địa phương ném lỗi trên Postgres | Npgsql từ chối `DateTimeOffset` có offset khác 0; nửa đêm giờ Việt Nam mang offset +07. DB trong bộ nhớ cho qua nên chỉ test tích hợp bắt được | `.ToUniversalTime()` trước khi đưa vào truy vấn |
