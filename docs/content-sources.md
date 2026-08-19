# Nguồn nội dung — cái gì được phép bịa, cái gì không

Ngày lập: 2026-08-18

Nguyên tắc của dự án: **thang level và cấu trúc tài liệu nghề nghiệp phải bám nguồn ngoài; câu thoại và bài tập thì tự biên soạn.**
Chép nguyên câu từ giáo trình thương mại là vi phạm bản quyền; bịa cấu trúc một bản postmortem hay một change request là dạy sai nghề.
Hai việc khác nhau, và tài liệu này phân định ranh giới đó.

---

## 1. Thang level — bám CEFR, không tự chế

| Hạng mục | Nguồn | Cấp |
|---|---|---|
| Descriptor Pre-A1 → B1 | [CEFR Companion Volume 2020, Council of Europe](https://rm.coe.int/common-european-framework-of-reference-for-languages-learning-teaching/16809ea0d4) | A — chuẩn chính thức, PDF gốc |
| Kích thước từ vựng theo bậc | [English Vocabulary Profile, Cambridge](https://englishprofile.org/?menu=english-vocabulary-profile) | A — chuẩn chính thức |
| Tải từ vựng mỗi bài | Nation & Webb (2008), *Evaluating the vocabulary load of written text* | B — học thuật |

**Descriptor dùng trực tiếp trong thiết kế** (trích nguyên văn Companion Volume):

- **Overall oral interaction, B1** (tr. 72): *"Can communicate with some confidence on familiar routine and non-routine matters related to their interests and professional field. Can exchange, check and confirm information, deal with less routine situations and explain why something is a problem."*
  → Đây chính là mô tả một kỹ sư báo sự cố. Bài nhánh Infrastructure và Cloud gắn nhãn B1 đứng vững trên descriptor này.

- **Overall oral comprehension, B1** (tr. 48): *"Can understand straightforward factual information about common everyday or job-related topics… provided people articulate clearly in a generally familiar variety."*
  → Cụm *"provided people articulate clearly"* là lý do nhân vật khách hàng nói nhanh nuốt âm chỉ xuất hiện ở bậc B1, không sớm hơn.

- **Overall phonological control, A2** (tr. 135): *"Systematic mispronunciation of phonemes does not hinder intelligibility, provided the interlocutor makes an effort…"*
  → Chuẩn của CEFR là **dễ hiểu**, không phải **chuẩn xác**. Ngưỡng chấm phát âm phải theo mức dễ hiểu, không lấy giọng bản xứ làm chuẩn.

- **Pre-A1 không có descriptor ngữ âm.** Thang ngữ âm Companion Volume bắt đầu từ A1. Vì vậy bậc L1 của app **cố ý không tuyên bố tương đương** khung KNLNN 6 bậc của Việt Nam.

**Số liệu áp dụng:**
- EVP: A1 ≈ 784 từ, A2 ≈ 1.594 từ, B1 ≈ 2.937 từ (tích luỹ).
- Nation & Webb: người lớn tiếp thu được **5–8 mục từ mới mỗi buổi**. Dự án chốt **6 từ/bài** — nằm trong khoảng, không phải con số cảm tính.

---

## 2. Cấu trúc tài liệu nghề — bám chuẩn ngành

Đây là phần mà bản cũ còn yếu và là lý do chính phải tra nguồn: nếu dạy học viên viết incident report theo cấu trúc tự nghĩ ra,
họ sẽ viết sai định dạng mà công ty thật đang dùng.

### 2.1 Postmortem sự cố — Google SRE

Nguồn: [Google SRE Book, Appendix D — Example Postmortem](https://sre.google/sre-book/example-postmortem/) và [Chương 15 — Postmortem Culture](https://sre.google/sre-book/postmortem-culture/) (cấp A, xuất bản công khai).

13 mục chuẩn, dùng nguyên làm khung cho bài đọc R-* và bài viết nhánh Infrastructure:

1. Date · 2. Authors · 3. Status · 4. Summary · 5. Impact · 6. Root Causes · 7. Trigger ·
8. Resolution · 9. Detection · 10. Action Items · 11. Lessons Learned (*What went well* / *What went wrong* / *Where we got lucky*) · 12. Timeline · 13. Supporting information

Nguyên tắc **blameless** trích từ chương 15: trọng tâm chuyển từ *ai gây lỗi* sang *hệ thống nào cho phép lỗi đó gây hậu quả*.
Bài học phải dạy học viên viết câu không quy trách nhiệm cá nhân — đây là yêu cầu ngôn ngữ thật, không phải phép lịch sự chung chung.

### 2.2 Vận hành dịch vụ — ITIL 4

Nguồn: [ITIL 4 glossary](https://www.manageengine.com/products/service-desk/itsm/itil-4-glossary-terms.html) (cấp C — diễn giải thương mại của chuẩn Axelos; định nghĩa khớp chuẩn nhưng **không phải bản gốc**).

Thuật ngữ lõi, dùng làm mục từ vựng bắt buộc của nhánh Helpdesk và Infrastructure:

| Thuật ngữ | Định nghĩa |
|---|---|
| Incident | An unplanned interruption or quality reduction of an IT service |
| Problem | The cause of one or more incidents |
| Major incident | An incident that has a significant impact on business operations |
| Workaround | A temporary solution that minimizes the impact of an incident or problem yet to be resolved |
| Service request | End-user request for information, advice, a standard change, or a service |
| Standard change | A pre-authorized, low-risk change that is proven and well understood |
| Emergency change | A change requiring immediate implementation, fast-tracked with increased risk |
| SLA | Agreement defining the required IT services and service levels |
| Escalation | Seeking specialist help or higher-level decision-making to progress a task |
| Service desk | The point of communication between IT service provider and end users |

Phân biệt **incident / problem / service request / change** là kiến thức nghề mà học viên helpdesk phải nói đúng bằng tiếng Anh —
nói nhầm incident thành problem trong cuộc gọi với vendor là hiểu lầm có hậu quả thật.

### 2.3 Sự cố bảo mật — NIST SP 800-61

Nguồn: [NIST SP 800-61 Rev. 3](https://csrc.nist.gov/pubs/sp/800/61/r3/final) (cấp A — chuẩn chính phủ Hoa Kỳ, công khai).

Bốn giai đoạn xử lý và động từ đi kèm: **detection → analysis → containment → eradication → recovery → lessons learned**.
Rev. 3 đã tái cấu trúc để bám khung CSF 2.0.

*Lưu ý kiểm chứng:* bản PDF r2 tải về được nhưng công cụ không đọc được nội dung (file nhị phân 1,5 MB).
Danh sách giai đoạn ở trên lấy từ trang mô tả chính thức của NIST, **chưa đối chiếu từng trường biểu mẫu trong PDF**.
Trước khi soạn bài viết incident report bảo mật, phải mở PDF r3 và trích đúng danh mục trường. Xem `IMPLEMENTATION_GAPS.md`.

### 2.4 Kiến trúc cloud — AWS Well-Architected

Nguồn: [AWS Well-Architected Framework — The pillars](https://docs.aws.amazon.com/wellarchitected/latest/framework/the-pillars-of-the-framework.html) (cấp A — tài liệu nhà cung cấp, công khai).

Sáu trụ cột, dùng làm khung cho bài trình bày kiến trúc nhánh Cloud:
**operational excellence · security · reliability · performance efficiency · cost optimization · sustainability**

Một bài "trình bày lựa chọn kiến trúc" phải buộc học viên nêu đánh đổi theo ít nhất hai trụ cột — đó là cách review thật diễn ra.

### 2.5 Rủi ro AI — NIST AI RMF 1.0

Nguồn: [NIST AI RMF Core](https://airc.nist.gov/airmf-resources/airmf/5-sec-core/) (cấp A — chuẩn NIST, công bố 26/01/2023).

Bốn chức năng lõi: **Govern · Map · Measure · Manage**.
Đặc tính AI đáng tin cậy: validity, safety, security, accountability, fairness.

Bài "đề xuất AI use case" và "AI risk assessment" bám bốn chức năng này thay vì tự nghĩ ra mục lục.

---

## 3. Phát âm người Việt — bám nghiên cứu peer-reviewed

Kế thừa từ khảo sát của dự án trước, các lỗi được chọn dạy đều có nghiên cứu hậu thuẫn, không phải quan sát cá nhân:

- Phụ âm /θ/ và /ð/ — nghiên cứu trên người học Việt trưởng thành
- Phụ âm cuối (/s/, /z/, /f/, /v/) bị nuốt
- Cụm phụ âm cuối (str-, -nts, -sks)

Ba nhóm này thành ba bài nền tảng riêng, không gộp.

---

## 4. Ranh giới bản quyền

- **Được phép**: dùng descriptor CEFR làm tiêu chí, dùng cấu trúc tài liệu công khai (postmortem, ITIL, NIST, AWS) làm khung bài, dùng thuật ngữ ngành.
- **Không được phép**: chép câu thoại, bài tập, hay đoạn văn từ giáo trình thương mại (Oxford English for IT, sách Cambridge, BBC, VOA).
  Các nguồn đó **chỉ dùng để đối chiếu mức độ**, không lấy nội dung.
- Mọi hội thoại, email mẫu, log, ticket trong `content/` là **tự biên soạn 100%**, đặt trong bối cảnh hư cấu của dự án.

---

## 5. Việc chưa làm được

1. **NIST SP 800-61 r3 chưa trích được từng trường biểu mẫu** — PDF không parse được bằng công cụ hiện có. Cần mở thủ công.
2. **EVP word list chưa tải về dạng máy đọc được** — hiện mới có con số tổng theo bậc, chưa có danh sách từ để cổng validate tự kiểm bài có dùng từ vượt bậc hay không.
3. **ITIL 4 dùng nguồn diễn giải, không phải glossary gốc của Axelos** — glossary gốc nằm sau tường phí. Định nghĩa đã đối chiếu chéo hai nguồn nhưng vẫn nên ghi chú cấp C.
4. **Chưa có nguồn cho tiếng Anh giao tiếp vendor/CAB** — hai tình huống này hiện dựa trên ITIL và kinh nghiệm ngành, chưa có tài liệu mô tả ngôn ngữ hội thoại chuẩn.
