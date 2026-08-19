# Giáo trình — từ số 0 đến họp kỹ thuật bằng tiếng Anh

Ngày lập: 2026-08-18 · Nguồn chuẩn: xem [`content-sources.md`](content-sources.md)

Tài liệu này mô tả **học cái gì, theo thứ tự nào, và đo bằng gì**. Nó là hợp đồng giữa phần nội dung và phần engine:
mọi file trong `content/` phải khớp mô tả ở đây, và cổng `ContentValidationTests` kiểm chuyện đó tự động.

---

## 1. Người học mục tiêu

Kỹ sư IT Việt Nam đang đi làm, **mất gốc tiếng Anh**: đọc được lệnh và tài liệu rời rạc nhờ đoán từ khoá,
nhưng không nghe được người ta nói, không dám mở miệng, và viết email thì dịch từng chữ từ tiếng Việt.

Ba giả định định hình mọi thiết kế:

1. **Không có thời gian.** Mỗi buổi 15 phút, học trên đường hoặc giữa hai ticket. Bài dài hơn 12 phút bị cổng validate chặn.
2. **Có sẵn kiến thức nghề.** Học viên biết incident là gì bằng tiếng Việt. Thứ thiếu là **cái tên tiếng Anh và cách nói ra**, không phải khái niệm.
3. **Sợ sai trước mặt đồng nghiệp.** Nên phần nói phải chấm bằng máy, riêng tư, và chấm theo mức *dễ hiểu* chứ không theo giọng bản xứ.

---

## 2. Hai trục: kỹ năng và ngữ cảnh

Giáo trình không phải một đường thẳng. Nó là lưới hai chiều.

### 2.1 Trục dọc — bốn bậc

| Bậc | CEFR | Làm được gì ở cuối bậc |
|---|---|---|
| **L0** | *dưới Pre-A1* | Đọc được bảng chữ cái, nghe và nói được số, giờ, thứ. Nói được ba câu cứu hộ khi không hiểu. |
| **L1** | Pre-A1 | Nói được câu ngắn về bản thân và công việc. Nghe được câu chậm, rõ, có ngắt. |
| **L2** | A1 | Trao đổi được việc thường ngày với đồng nghiệp. Đọc được tin nhắn nội bộ ngắn. |
| **L3** | A2 | Báo cáo được việc mình làm. Đọc được email và ticket. Viết được tin nhắn công việc. |
| **L4** | B1 | Tham gia được họp kỹ thuật, giải thích được vì sao một thứ hỏng, viết được incident report. |

**L4 bám thẳng descriptor CEFR** (Companion Volume, tr. 72):
*"Can communicate with some confidence on familiar routine and non-routine matters related to their interests and professional field. Can exchange, check and confirm information, deal with less routine situations and explain why something is a problem."*

Cụm cuối — *explain why something is a problem* — chính là định nghĩa công việc của một kỹ sư trực sự cố. Đó là lý do đích của giáo trình đặt ở B1, không cao hơn.

**L0 là bậc do dự án tự đặt, không phải bậc CEFR.** CEFR không có mô tả nào dưới Pre-A1, nên L0 không tuyên bố tương đương gì cả. Nó tồn tại vì người mất gốc thật sự cần bốn tuần chỉ để nghe số mà không nhầm.

### 2.2 Trục ngang — ba tầng ngữ cảnh

| Tầng | Nội dung | Ví dụ tình huống |
|---|---|---|
| **Life** — đời sống | Chào hỏi, số, giờ, tiền, ăn uống, đi lại, hỏi đường, mua bán, hẹn gặp | Gọi món trưa; hỏi đường tới toà nhà; đặt xe |
| **Office** — văn phòng | Pantry small talk, xin nghỉ, hỏi việc, nhắn Teams/Slack, họp ngắn, giới thiệu bản thân | Xin nghỉ một ngày; hỏi lại yêu cầu chưa rõ; chào khách tới văn phòng |
| **Professional** — chuyên môn | Vận hành, báo cáo, họp team, xử lý sự cố, viết report | Standup; báo outage; gọi vendor; CAB; review kiến trúc cloud; đề xuất AI use case |

Một học viên mất gốc **phải đi qua Life trước**. Không phải vì Life dễ hơn, mà vì tầng Professional dùng lại toàn bộ số, giờ, và câu hỏi lại của tầng Life — báo sự cố mà nói sai giờ thì cả câu vô nghĩa.

Học viên đã có nền có thể nhảy thẳng vào Office hoặc Professional; bài xếp lớp quyết định điểm vào.

---

## 3. Năm chế độ học

Học viên chọn ở onboarding và đổi được bất cứ lúc nào. **Đổi chế độ không xoá tiến độ đã có.**

Thứ tự ưu tiên kỹ năng toàn hệ thống: **Nghe → Nói → Đọc → Viết.**

| Chế độ | Lấy bước nào của bài | Dành cho ai | Cần gì |
|---|---|---|---|
| **Hỗn hợp** *(mặc định)* | Đủ 7 bước theo thứ tự nghe → nói → đọc → viết | Người muốn dùng được tiếng Anh thật trong công việc | Loa + micro |
| **Chỉ Nghe** | Listen, phần nghe của Quiz | Người đi làm nghe họp không kịp; học được lúc lái xe | Chỉ cần loa |
| **Chỉ Nói** | Shadow, Speak | Người đọc hiểu ổn nhưng không dám mở miệng | Micro + HTTPS |
| **Chỉ Đọc** | Read, câu hỏi đọc hiểu | Người cần đọc tài liệu, log, ticket nhanh | Không cần gì |
| **Chỉ Viết** | Write | Người phải viết email, report, change request | Không cần gì |

**Vì sao Nghe đứng đầu:** descriptor CEFR cho nghe ở A2 vẫn còn kèm điều kiện *"provided people articulate clearly and slowly"*.
Nghĩa là nghe là kỹ năng chín chậm nhất và cần nhiều giờ tiếp xúc nhất — bắt đầu sớm nhất thì mới kịp.

**Hai hệ quả của chế độ đơn kỹ năng, app nói thẳng ngay lúc chọn:**

1. **Không đủ để lên bậc.** Checkpoint cuối mỗi bậc đòi cả bốn trục đạt ngưỡng riêng. Học chỉ-nghe sẽ mở hết bài trong bậc nhưng không qua được checkpoint.
2. **Không giữ được chuỗi ngày.** Một ngày chỉ tính vào chuỗi khi chạm đủ bốn kỹ năng *và* đủ mục tiêu phút. Phút học vẫn được cộng và điểm trục đó vẫn lên — chỉ riêng chuỗi thì không.

Cả hai đều hiển thị ngay trên bảng điều khiển khi đang ở chế độ đơn kỹ năng, không để học viên phát hiện sau ba tuần.

### Mục tiêu mỗi ngày

**45 phút** — khoảng ba buổi 15 phút, hoặc bốn bài liền. Bài dài tối đa 12 phút nên 45 phút luôn gói trọn vài bài chứ không cắt giữa bài.

Học viên đổi được con số này trong cài đặt, nhưng **quy tắc tính chuỗi luôn bám mục tiêu đang đặt**: học năm phút rồi thoát không giữ được chuỗi.

Toàn bộ các con số này nằm ở một chỗ trong code: `LearningPolicyOptions`.

---

## 4. Bảy bước của một bài

Mỗi bài 10–12 phút, cùng một khung, khác nhau ở nội dung:

| # | Bước | Kỹ năng | Làm gì | Chấm thế nào |
|---|---|---|---|---|
| 1 | **Listen** | Nghe | Nghe đoạn hội thoại nghề, tốc độ theo bậc (L1 0.85 → L4 1.3) | Trắc nghiệm nghe hiểu |
| 2 | **Vocab** | — | 6 từ/cụm mới, có IPA, nghĩa Việt, và một chunk dùng được ngay | Nhận diện |
| 3 | **Shadow** | Nói | Nghe câu mẫu rồi nhắc lại | Chấm phát âm tại máy chủ |
| 4 | **Speak** | Nói | Trả lời một câu hỏi thật của tình huống | Ba trục: phát âm, trôi chảy, truyền đạt |
| 5 | **Read** | Đọc | Đọc email / ticket / log / postmortem thật của tình huống | Câu hỏi skim và scan |
| 6 | **Write** | Viết | Điền chỗ trống, sắp câu, hoặc viết email có hướng dẫn | Chấm bằng luật, đạt ≥ 80 |
| 7 | **Quiz** | Tổng hợp | 4–6 câu trộn cả bốn kỹ năng | Trắc nghiệm |

**Sáu từ mỗi bài** không phải con số cảm tính: Nation & Webb (2008) đặt tải từ vựng của người lớn ở mức 5–8 mục mỗi buổi.
Nhồi 15 từ một bài thì học viên nhớ được 3.

---

## 5. Nội dung theo tầng

### 5.1 Tầng Life — 16 bài (L0–L2)

| Mã | Nội dung | Bậc |
|---|---|---|
| LIFE-01 → 04 | Bảng chữ cái, đánh vần tên; số 0–100; giờ và thứ; ngày tháng | L0 |
| LIFE-05 → 06 | Ba câu cứu hộ (*Sorry? · Could you say that again? · Could you speak more slowly?*); chào hỏi và tạm biệt | L0 |
| LIFE-07 → 09 | Bốn cụm lịch sự; giới thiệu bản thân ba câu; nhờ và xin phép | L1 |
| LIFE-10 → 13 | Gọi món; đi lại và đặt xe; hỏi đường; mua bán và tiền | L1–L2 |
| LIFE-14 → 16 | Hẹn gặp và đổi hẹn; nói về thời tiết và sức khoẻ; kể một ngày của mình | L2 |

Ba câu cứu hộ dạy ở LIFE-05, tức **bài thứ năm của cả giáo trình**. Lý do: học viên cần công cụ thoát hiểm trước khi cần bất cứ thứ gì khác.
Không có nó, buổi nói chuyện thật đầu tiên sẽ kết thúc bằng im lặng và học viên bỏ học.

### 5.2 Tầng Office — 12 bài (L2–L3)

| Mã | Nội dung | Bậc |
|---|---|---|
| OFF-01 → 03 | Chào buổi sáng và pantry 60 giây; giới thiệu bản thân với team; hỏi tên và vai trò người mới | L2 |
| OFF-04 → 06 | Xin nghỉ phép; hỏi làm rõ yêu cầu; báo tiến độ công việc | L2–L3 |
| OFF-07 → 09 | Đặt và dời lịch họp; xin giúp khi bị kẹt; từ chối lịch sự | L3 |
| OFF-10 → 12 | Viết tin nhắn Teams/Slack ngắn; viết email nội bộ; nói trong họp online (mute, share, hear me?) | L3 |

### 5.3 Tầng Professional — 30 bài (L3–L4)

Chia theo sáu nhánh nghề. Học viên học nhánh chính trước, các nhánh khác mở sau checkpoint.

#### Nhánh Helpdesk — 5 bài
Nhận ticket và xác nhận · hỏi thông tin để chẩn đoán · hướng dẫn người dùng từng bước · giải thích cho người không rành kỹ thuật · leo thang (*escalate*) và bàn giao.

Từ vựng lõi lấy từ ITIL 4: *incident · problem · service request · workaround · escalation · service desk · SLA*.
Phân biệt **incident** với **problem** và **service request** là nội dung bài riêng — nói nhầm ba thứ này trong cuộc gọi vendor là hiểu lầm có hậu quả.

#### Nhánh Infrastructure & Operations — 7 bài
Standup ba phần (hôm qua / hôm nay / vướng mắc) · báo outage và degradation · cập nhật trạng thái sự cố · bàn giao ca trực ·
họp change review và CAB · nói về ảo hoá, backup, DR · nói về mạng, VLAN, routing, firewall.

Change request bám thuật ngữ ITIL: *request for change · standard change · emergency change · change authority*.

#### Nhánh Security — 4 bài
Báo sự cố bảo mật · nói về quyền truy cập và phân quyền · trao đổi về lỗ hổng và bản vá · viết security advisory nội bộ.

Bám bốn giai đoạn NIST SP 800-61: *detection → analysis → containment → eradication → recovery → lessons learned*.

#### Nhánh Cloud — 6 bài
Trình bày lựa chọn VM / container / Kubernetes / serverless · nói về HA và DR · nói về scaling và chi phí ·
review kiến trúc · viết architecture note · viết migration plan.

Bài "trình bày lựa chọn kiến trúc" buộc học viên nêu đánh đổi theo **ít nhất hai trong sáu trụ cột** của AWS Well-Architected
(*operational excellence · security · reliability · performance efficiency · cost optimization · sustainability*) — đó là cách một buổi review thật diễn ra.

#### Nhánh AI — 4 bài
Trình bày AI use case cho stakeholder không chuyên · nói về model selection, RAG, fine-tuning ·
nói về latency, accuracy, monitoring · viết AI proposal và risk assessment.

Bài viết risk assessment bám bốn chức năng NIST AI RMF: *Govern · Map · Measure · Manage*.

#### Nhánh Reading chuyên sâu — 4 bài
Đọc log tìm lỗi · đọc release notes và changelog · đọc tài liệu API · đọc postmortem.

Bài postmortem dùng đúng 13 mục của Google SRE (Date · Authors · Status · Summary · Impact · Root Causes · Trigger ·
Resolution · Detection · Action Items · Lessons Learned · Timeline · Supporting information).
Học viên đọc xong phải chỉ ra được đâu là *root cause* và đâu chỉ là *trigger* — hai thứ hay bị lẫn.

---

## 6. Năm mốc nghề nghiệp

Roadmap không hiển thị "bạn đã học 23/58 bài". Nó hiển thị năm mốc học viên nói được với sếp:

| Mốc | Mở khi | Đo bằng |
|---|---|---|
| **Tự tin dự standup** | Xong OFF-06 + INF-01 | Nói trọn ba phần standup, điểm truyền đạt ≥ 75 |
| **Tự viết incident report** | Xong SEC-01 + INF-03 | Bài viết đủ 6 trường bắt buộc, đạt ≥ 80 |
| **Tự gọi vendor support** | Xong HD-05 + INF-04 | Hoàn thành roleplay vendor không dùng gợi ý |
| **Tự trình bày cloud solution** | Xong CLD-04 | Nói được đánh đổi theo 2 trụ cột, điểm nói ≥ 75 |
| **Tự đề xuất AI use case** | Xong AI-04 | Bài viết proposal đủ 4 mục RMF, đạt ≥ 80 |

---

## 7. Xếp lớp và chống nhảy cóc

- **Xếp lớp**: 26 câu, 18 phút, hai đề song song. Đo bốn trục kỹ năng riêng cộng trục phụ từ vựng–ngữ pháp.
  Kết quả xếp L0–L4 **và** đề xuất tầng ngữ cảnh vào học.
- **Chống nhảy cóc**: mỗi bài có tiên quyết cứng hoặc mềm. Ngưỡng xét **riêng từng kỹ năng** — điểm tổng cao
  không che được trục yếu. Bài bị khoá luôn hiện con số cụ thể còn thiếu, không hiện chữ "chưa đủ điều kiện".
- **Thi vượt**: bài nào cũng có nút thi vượt, lấy câu khó của bài, chấm tại máy chủ, đạt 85% thì mở khoá ngay.
- **Ôn tập giãn cách**: xếp lịch theo từng câu, khoảng cách mới bằng khoảng cách cũ nhân hệ số giãn, trần 60 ngày.

---

## 8. Cổng chất lượng nội dung

`ContentValidationTests` chặn publish nếu bất kỳ điều nào sau đây sai:

1. Thiếu giải thích tiếng Việt (`explanation`) hoặc có dưới 2 lỗi thường gặp (`common_mistakes`).
2. Tổng `mastery_weights` khác 1.0.
3. DAG tiên quyết có chu trình.
4. Có cạnh tiên quyết đi từ bậc cao xuống bậc thấp.
5. `est_minutes` lớn hơn 12.
6. Bài nhánh nói có dưới 4 drill.
7. Bài khai `SupportedSkills` chứa kỹ năng mà không có bước tương ứng — nguyên nhân số một của lỗi "chọn chế độ Chỉ Nghe rồi mở bài thấy trống".
8. Số mục từ vựng ngoài khoảng 5–8.

---

## 9. Quyết định đã chốt

| Hạng mục | Giá trị | Ngày chốt |
|---|---|---|
| Mục tiêu phút mỗi ngày | 45 phút | 2026-08-18 |
| Ngày tính chuỗi | Phải đủ mục tiêu phút **và** chạm đủ bốn kỹ năng | 2026-08-18 |
| Giữ file ghi âm | 45 ngày rồi xoá file, **giữ lại điểm** | 2026-08-18 |
| Thứ tự soạn tầng Chuyên môn | Helpdesk → Infrastructure → Security → Cloud → AI → Reading | 2026-08-18 |

Lý do thứ tự soạn bắt đầu từ Helpdesk: đó là nơi kỹ sư mất gốc buộc phải mở miệng nói tiếng Anh sớm nhất — người dùng cuối gọi tới, không chờ được.

Giữ ghi âm 45 ngày là điểm cân bằng: đủ dài để gom dữ liệu hiệu chỉnh ngưỡng chấm phát âm qua trọn một chu kỳ học, đủ ngắn để không tích luỹ giọng nói vô thời hạn. Job dọn dẹp chỉ xoá **file**, bản ghi điểm ở lại để vẽ tiến bộ.

## 10. Việc chưa chốt

1. ~~**Số bài tầng Professional (30) là dự kiến**, chưa soạn xong.~~
   Đã soạn xong ngày 18/08/2026: 58/58 bài, cả 58 qua cổng chất lượng và có hình minh hoạ riêng.
   Phân bố thực tế: Life 16, Office 12, Helpdesk 5, Infrastructure 7, Security 4, Cloud 6, AI 4, Reading 4.
2. **Ngưỡng điểm từng kỹ năng** (hiện 65 cho từng trục, 80 cho mastery, 85 cho thi vượt) là giá trị khởi điểm, chưa hiệu chỉnh trên học viên thật.
