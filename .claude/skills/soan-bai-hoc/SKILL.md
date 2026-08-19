---
name: soan-bai-hoc
description: Soạn một bài học mới cho giáo trình English for IT (file YAML trong content/lessons/). Dùng khi cần thêm bài vào một nhánh có sẵn hoặc mở nhánh mới. Chứa schema đầy đủ, mọi luật của cổng kiểm định, và các bẫy đã từng làm hỏng bài.
---

# Soạn bài học mới

Mỗi bài là một file YAML trong `content/lessons/<nhánh>/<MÃ>.yaml`, trung bình 270 dòng.
Seeder đọc đệ quy toàn bộ `content/lessons/`, nên thêm thư mục mới là tự nhận.

## Quy trình

1. Chọn mã và nhánh. Mã phải là duy nhất toàn giáo trình, slug cũng vậy.
2. Copy khung từ một bài cùng nhánh làm mẫu — đừng viết từ đầu.
3. Chạy cổng kiểm định **ngay sau bài đầu tiên**, trước khi soạn tiếp:
   `dotnet test tests/ContentValidationTests`
4. Soạn xong hết thì cập nhật các con số gắn cứng trong test (xem mục cuối).
5. Chạy `dotnet test` toàn bộ.

Soạn cả loạt rồi mới kiểm là cách chắc chắn nhất để phải sửa lại tất cả.

## Luật của cổng kiểm định

Vi phạm là seeder **từ chối nạp cả bộ nội dung**, không chỉ bài sai.

| Mã | Luật |
|---|---|
| E010 | Bắt buộc có `explanation.why_vi` và `how_vi` |
| E011 | Tối thiểu **2** `common_mistakes`, mỗi mục cần `why_vi` và `fix_vi` |
| E020/E021 | `estimated_minutes` tối đa **12**, không được quá ngắn |
| E030 | Từ vựng phải trong khoảng **5–8** mục |
| E031/E032 | Mỗi từ cần `chunk` và `meaning_vi` |
| E041 | Tổng `mastery_weights` phải **đúng bằng 1.0** |
| E052 | Có trọng số cho kỹ năng nào thì phải có phần dạy kỹ năng đó |
| E060 | Bài trọng tâm nói cần tối thiểu **4** `speaking_drills` |
| E062 | Drill `respond` bắt buộc có `accept_patterns` |
| E071 | `answer` phải nằm trong khoảng số lượng `choices` |
| E072 | Không được có hai lựa chọn trùng nhau |
| E083 | Phần viết bắt buộc có `sample_en` |
| E103 | Bậc của bài tiên quyết phải **≤** bậc của bài này |
| E104 | DAG tiên quyết không được có chu trình |
| E110 | `illustration` phải là khoá có trong `IllustrationCatalogue.cs` |

## Ba cái bẫy đã thật sự làm hỏng bài

### 1. E111 — dấu nháy thoát trong block scalar

Trong khối `>` hoặc `|`, YAML **không xử lý escape**, nên `\"` là hai ký tự thật.

```yaml
    why_vi: >
      Người học dịch thành \"hạn sử dụng\".    # SAI — validator báo E111
      Người học dịch thành "hạn sử dụng".      # ĐÚNG
```

Nhưng trong chuỗi có nháy thì `\"` lại **bắt buộc**:

```yaml
  - mistake: "Nói \"I want\" khi gọi món"      # ĐÚNG
```

Phân biệt: dòng bắt đầu bằng `key:` hoặc `- key:` là chuỗi có nháy → dùng `\"`.
Dòng thụt vào bên dưới `>` là nội dung block → dùng `"` trần.

### 2. Đáp án dồn về một vị trí

Có test chạy trên **toàn giáo trình**: không vị trí nào được chiếm quá 50% tổng số câu.
Từng có lúc 79% câu dồn vào vị trí 1 — chọn mãi ô đó là qua gần hết giáo trình.

Khi soạn, rải `answer` đều giữa 0, 1, 2 trong từng bài.

### 3. Con số gắn cứng trong test tích hợp

Thêm bài là ba test đỏ ngay. Phải cập nhật:

- `tests/IntegrationTests/ContentSeedingTests.cs` — số bài, số activity, số item, số khoá hình
- `tests/IntegrationTests/LearningFlowTests.cs` — số bài trên lộ trình

Cách lấy số thật: chạy test, đọc dòng `Actual:` trong thông báo lỗi.

## Nhánh mới

Thêm giá trị vào `LearningTrack` trong `src/Domain/Enums/CoreEnums.cs`.
Cột lưu dạng chuỗi nên **không cần migration**.

Bài đầu của nhánh vào cửa riêng thì để `prerequisites: []`.
Nối tiếp nhánh khác thì trỏ tới bài cuối của nhánh đó.

## Chất lượng nội dung

Phần này cổng kiểm định không bắt được, nhưng là thứ làm bài đáng học:

- **Bối cảnh phải thật.** "Bảy giờ tối thứ Sáu, bạn dẫn hai người bạn nước ngoài đi ăn" —
  không phải "Đây là hội thoại ở nhà hàng".
- **`common_mistakes` phải là lỗi người Việt thật sự mắc**, và `why_vi` phải giải thích
  *vì sao* người Việt mắc lỗi đó — thường là do dịch thẳng cấu trúc tiếng Việt, hoặc do
  tiếng Việt không có âm đó.
- **Mỗi drill nói cần một điểm phát âm cụ thể**, không phải "đọc to câu này".
- **`memory_trick_vi` phải gói cả bài thành một câu nhớ được**, thường là ba bước.
