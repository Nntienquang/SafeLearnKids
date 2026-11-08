# Feature Specification: Quản lý bài học

**Feature Branch**: `001-lesson-management`

**Created**: 2026-11-03

**Status**: Draft

**Input**: User description: "Quản lý bài học"

## Clarifications

### Session 2026-11-03

- Q: Khi cần sửa bài đã xuất bản, có chấp nhận đưa bài về Bản nháp để bé tạm không thấy bài cho tới
  khi xuất bản lại không? → A: Chấp nhận; không lưu song song phiên bản cũ.
- Q: Loại bài do Admin tự chọn hay hệ thống tự xác định theo nội dung? → A: Hệ thống tự xác định:
  chỉ video → Video, chỉ bài đọc → Bài đọc, có cả hai → Kết hợp; bài chưa có nội dung giữ Video.
- Q: Thời lượng dự kiến của một bài được tối đa bao nhiêu phút? → A: Chưa đặt giới hạn trên; để
  Sprint 5 quyết định cùng tính năng giới hạn thời gian. Đợt này chỉ yêu cầu không âm.
- Q: Phạm vi theo phân công Trello? → A: Làm US-11, US-12 và US-13; bỏ gắn thẻ (US-14) khỏi tính năng.
- Q: Thời lượng bài do Admin nhập hay tự tính? → A: Tự tính theo nội dung (FR-020); bỏ ô nhập khỏi
  form. Câu trả lời trước về giới hạn tối đa vẫn giữ: để Sprint 5, áp lên con số tự tính.
- Q: Có giữ nút "Xem thử" video YouTube trong trang soạn bài không? → A: Giữ (FR-021).
- Q: Có giữ bước "Chờ duyệt" trước khi xuất bản không? → A: Bỏ; Admin xuất bản thẳng từ Bản nháp, tránh
  trùng với quy trình duyệt nội dung của Sprint 3.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Tạo và sửa thông tin bài học (Priority: P1)

Quản trị viên tạo một bài học mới cho một môn và một lớp: nhập tên bài, mục tiêu/tóm tắt, thời
lượng dự kiến và thứ tự trong môn; loại bài được hệ thống tự xác định theo nội dung (FR-019). Bài mới luôn ở trạng thái
"Bản nháp". Quản trị viên mở lại bài từ danh sách bài của môn để sửa các thông tin này.

**Why this priority**: Không có bài học thì không có gì để thêm nội dung hay xuất bản; đây là phần
tối thiểu để thay thế trang "Soạn bài học" hiện chỉ là giao diện mẫu.

**Independent Test**: Tạo một bài nháp cho Toán · Lớp 3, thấy bài xuất hiện trong danh sách bài của
môn Toán với trạng thái "Bản nháp"; sửa tên bài và thấy tên mới được giữ sau khi tải lại trang.

**Acceptance Scenarios**:

1. **Given** quản trị viên đang ở danh sách bài của môn Toán, **When** chọn "Bài học mới", nhập
   tên, chọn Lớp 3 và lưu, **Then** bài được tạo ở trạng thái "Bản nháp", gắn với Toán · Lớp 3 và
   hiện trong danh sách bài của môn.
2. **Given** môn Toán chỉ được gán Lớp 1–3, **When** quản trị viên mở form tạo bài, **Then** chỉ
   chọn được Lớp 1–3.
3. **Given** một bài đang ở trạng thái "Bản nháp", **When** quản trị viên đổi tên và tóm tắt rồi
   lưu, **Then** thay đổi được giữ lại và thời điểm cập nhật được ghi nhận.
4. **Given** form tạo bài, **When** để trống tên bài hoặc chọn lớp chưa gán cho môn, **Then** hệ thống từ
   chối lưu và chỉ rõ trường bị sai.
5. **Given** hai quản trị viên cùng mở một bài, **When** người thứ hai lưu sau khi người thứ nhất đã
   lưu, **Then** người thứ hai được báo bài đã bị thay đổi và không ghi đè lên thay đổi của người
   thứ nhất.

---

### User Story 2 - Quản lý nội dung trong bài (Priority: P2)

Trong trang soạn bài, quản trị viên thêm các phần nội dung theo thứ tự bé sẽ học: video lấy từ kho
video đã được duyệt an toàn và bài đọc (văn bản). Quản trị viên sửa tiêu đề, đổi thứ tự hoặc xóa
từng phần.

**Why this priority**: Một bài không có nội dung thì bé không học được; nhưng phần này chỉ có giá trị
khi bài đã tồn tại (Story 1).

**Independent Test**: Với một bài nháp có sẵn, thêm một video đã duyệt và một bài đọc, đổi thứ tự
hai phần, tải lại trang và thấy đúng thứ tự mới; xóa bài đọc và thấy bài chỉ còn video.

**Acceptance Scenarios**:

1. **Given** một bài nháp, **When** quản trị viên thêm phần "Video" và chọn một video trong kho đã
   được duyệt, **Then** phần video xuất hiện cuối danh sách nội dung, kèm tiêu đề và thời lượng video.
2. **Given** kho có video ở trạng thái chờ duyệt hoặc bị chặn, **When** quản trị viên chọn video cho
   bài, **Then** những video đó không có trong danh sách chọn.
3. **Given** một bài nháp, **When** quản trị viên thêm phần "Bài đọc" với nội dung văn bản, **Then**
   phần bài đọc được lưu; nếu để trống nội dung thì hệ thống từ chối.
4. **Given** bài có 3 phần nội dung, **When** quản trị viên chuyển phần thứ 3 lên đầu, **Then** thứ
   tự mới được lưu và hiển thị đúng sau khi tải lại.
5. **Given** bài có một phần nội dung, **When** quản trị viên xóa phần đó (sau khi xác nhận), **Then**
   phần đó biến mất khỏi bài.

---

### User Story 3 - Vòng đời bài: xuất bản, lưu trữ (Priority: P3)

Bài đi qua các trạng thái Bản nháp → Đã xuất bản → Lưu trữ. Mọi quản trị viên, kể cả người soạn
bài, được xuất bản thẳng từ Bản nháp. Bài chỉ được xuất bản khi có ít nhất 1 phần nội dung hợp lệ;
quiz không bắt buộc. Bài đã xuất bản có thể chuyển sang Lưu trữ để ngừng hiển thị với bé, và bài lưu trữ có
thể đưa về Bản nháp để sửa tiếp.

**Why this priority**: Kiểm soát việc bài nào đến tay trẻ là yêu cầu an toàn cốt lõi của sản phẩm,
nhưng chỉ có ý nghĩa sau khi đã soạn được bài và nội dung.

**Independent Test**: Với một bài nháp đã có nội dung, đưa bài qua từng bước tới "Đã xuất bản", rồi
"Lưu trữ", rồi về "Bản nháp"; thử xuất bản một bài không có nội dung và thấy bị từ chối kèm lý do.

**Acceptance Scenarios**:

1. **Given** một bài nháp chưa có nội dung, **When** quản trị viên chọn "Xuất bản", **Then** hệ thống
   từ chối và nêu rõ điều kiện còn thiếu.
2. **Given** một bài nháp có nội dung hợp lệ, **When** một quản trị viên (có thể là chính người soạn)
   chọn "Xuất bản", **Then** bài chuyển sang "Đã xuất bản", ghi lại người xuất bản và thời điểm xuất bản.
3. **Given** một bài "Đã xuất bản", **When** quản trị viên chọn "Lưu trữ", **Then** bài chuyển sang
   "Lưu trữ" và không còn được tính là bài đang mở cho bé.
4. **Given** một bài không ở trạng thái "Bản nháp", **When** quản trị viên muốn sửa nội dung,
   **Then** hệ thống yêu cầu đưa bài về "Bản nháp" trước, để nội dung đang hiển thị cho bé không bị
   đổi khi chưa xuất bản lại.
5. **Given** bất kỳ lần chuyển trạng thái nào, **When** thao tác thành công, **Then** nhật ký quản
   trị ghi lại người thực hiện, bài, trạng thái cũ và mới.

---

### User Story 4 - Xóa bài (Priority: P4)

Quản trị viên xóa bài nháp tạo nhầm.

**Why this priority**: Hữu ích cho việc giữ danh mục gọn gàng nhưng không chặn luồng soạn và xuất bản.

**Independent Test**: Tạo bài nháp rồi xóa và thấy bài biến mất khỏi danh sách bài của môn.

**Acceptance Scenarios**:

1. **Given** một bài nháp chưa từng được bé học, giao hay yêu thích, **When** quản trị viên xóa (sau
   khi xác nhận), **Then** bài và các phần nội dung của nó bị xóa vĩnh viễn.
2. **Given** một bài đã có dữ liệu học tập, được giao, được yêu thích hoặc bị báo cáo, **When** quản
   trị viên xóa, **Then** hệ thống từ chối và gợi ý chuyển bài sang "Lưu trữ".

### Edge Cases

- Môn của bài bị ẩn: bài vẫn soạn được nhưng trang soạn bài hiển thị cảnh báo môn đang ẩn.
- Quản trị viên đổi lớp của bài sang lớp chưa được gán cho môn: hệ thống không cho chọn lớp đó.
- Video trong bài bị chặn sau khi bài đã xuất bản: trang soạn bài đánh dấu phần video đó là không còn
  hợp lệ; bài không được xuất bản lại cho tới khi thay video.
- Hai quản trị viên cùng chuyển trạng thái một bài: chỉ thao tác đầu tiên có hiệu lực, người còn lại
  được báo bài đã thay đổi.
- Bài đọc rất dài hoặc chứa thẻ HTML/script: nội dung được hiển thị như văn bản thuần, không thực thi.
- Mở một bài không tồn tại hoặc đã bị xóa: hiển thị trang "không tìm thấy".
- Người không phải quản trị viên truy cập trang soạn bài hoặc gửi thao tác: bị từ chối.
- Chế độ chạy không có cơ sở dữ liệu nghiệp vụ (demo trong bộ nhớ): trang thông báo tính năng không
  khả dụng thay vì báo lỗi.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Chỉ người có vai trò Quản trị viên MUST được xem trang soạn bài và thực hiện mọi thao
  tác tạo, sửa, xóa, chuyển trạng thái bài học.
- **FR-002**: Quản trị viên MUST tạo được bài học với: tên (bắt buộc, tối đa 200 ký tự), môn, lớp,
  tóm tắt/mục tiêu (tối đa 500 ký tự) và thứ tự trong môn. Admin không nhập loại bài (FR-019) và
  thời lượng (FR-020).
- **FR-003**: Lớp của bài MUST thuộc các lớp đã được gán cho môn đó.
- **FR-004**: Bài mới MUST ở trạng thái "Bản nháp" và ghi lại người tạo cùng thời điểm tạo.
- **FR-005**: Hệ thống MUST phát hiện khi hai người cùng sửa một bài và không cho thay đổi sau ghi
  đè lặng lẽ lên thay đổi trước.
- **FR-006**: Quản trị viên MUST thêm, sửa tiêu đề, đổi thứ tự và xóa được các phần nội dung của bài.
- **FR-007**: Phần nội dung loại Video MUST tham chiếu một video trong kho có trạng thái "Đã duyệt";
  phần loại Bài đọc MUST có nội dung văn bản không rỗng.
- **FR-008**: Hệ thống MUST hỗ trợ hai loại nội dung: Video và Bài đọc. Tài liệu và Hình ảnh tải
  lên không có trong đợt này.
- **FR-009**: Hệ thống MUST quản lý trạng thái bài theo các bước: Bản nháp → Đã xuất bản → Lưu trữ,
  và cho phép đưa bài Đã xuất bản hoặc Lưu trữ về Bản nháp. Bài ở trạng thái "Chờ duyệt" có sẵn trong
  dữ liệu cũ chỉ được đưa về Bản nháp; giao diện không tạo trạng thái này nữa.
- **FR-010**: Hệ thống MUST chặn xuất bản khi bài chưa có ít nhất 1 phần nội dung hợp lệ
  (video còn ở trạng thái đã duyệt, hoặc bài đọc không rỗng) và nêu rõ điều kiện còn thiếu. Quiz
  không phải điều kiện xuất bản.
- **FR-011**: Mọi quản trị viên, kể cả người soạn bài, MUST xuất bản được bài; bài chỉ xuất bản được
  từ trạng thái "Bản nháp". Khi xuất bản, hệ thống MUST ghi lại người xuất bản và thời
  điểm xuất bản.
- **FR-012**: Nội dung và thông tin của bài không ở trạng thái Bản nháp MUST không sửa
  được cho tới khi bài được đưa về Bản nháp. Khi bài đã xuất bản được đưa về Bản nháp, bài ngừng hiển
  thị với bé cho tới khi được xuất bản lại; hệ thống không giữ song song phiên bản đang xuất bản.
  Trước khi đưa bài đã xuất bản về Bản nháp, hệ thống MUST xác nhận với Admin rằng bé sẽ tạm không
  thấy bài.
- **FR-013**: Quản trị viên MUST xóa được bài nháp chưa có dữ liệu liên quan (học tập, giao bài, yêu
  thích, báo cáo, câu hỏi, quiz); bài có dữ liệu liên quan MUST bị từ chối xóa kèm gợi ý lưu trữ.
- **FR-014**: (Đã bỏ ngày 2026-11-03) Gắn thẻ phân loại bài thuộc card Trello US-14, ngoài phạm vi.
- **FR-015**: Mọi thao tác tạo, sửa, xóa, chuyển trạng thái bài và thay đổi nội dung MUST được ghi
  vào nhật ký quản trị, gồm người thực hiện, bài liên quan và mô tả thay đổi.
- **FR-016**: Danh sách bài của môn trên trang "Môn học và bài học" MUST mở được trang soạn của từng
  bài và nút "Bài học mới" MUST mở form tạo bài cho môn đang chọn.
- **FR-017**: Nội dung do quản trị viên nhập (tên, tóm tắt, bài đọc) MUST được hiển thị như văn bản
  thuần, không thực thi mã.
- **FR-018**: Mọi thao tác thất bại do dữ liệu không hợp lệ hoặc xung đột MUST hiển thị thông báo
  tiếng Việt rõ ràng và không làm mất dữ liệu đã nhập trên form.
- **FR-019**: Hệ thống MUST tự cập nhật loại bài mỗi khi nội dung bài thay đổi: chỉ có Video → Video;
  chỉ có Bài đọc (hoặc tài liệu/hình ảnh có sẵn) → Bài đọc; có cả video và loại khác → Kết hợp; bài
  chưa có nội dung → Video.
- **FR-020**: Hệ thống MUST tự tính thời lượng bài (phút, làm tròn lên) mỗi khi nội dung thay đổi: cộng
  thời lượng thật của các video và thời gian đọc ước tính của các bài đọc (100 chữ mỗi phút); tài
  liệu/hình ảnh không cộng thêm; bài chưa có nội dung có thời lượng 0.
- **FR-021**: Trang soạn bài MUST cho Admin xem thử từng video YouTube ngay
  trong trang (trình phát nhúng chế độ hạn chế cookie, chỉ tải khi mở). Phát video cho bé vẫn thuộc
  Sprint 4.

### Key Entities *(include if feature involves data)*

- **Bài học**: đơn vị học thuộc một môn và một lớp; có tên, tóm tắt, loại bài, thời lượng, thứ tự,
  trạng thái, người tạo, người xuất bản, các mốc thời gian tạo/cập nhật/xuất bản.
- **Phần nội dung**: một mục trong bài theo thứ tự (video, bài đọc, tài liệu, hình ảnh); có tiêu đề
  và thứ tự; video tham chiếu Video nguồn, bài đọc chứa văn bản, tài liệu/hình ảnh chứa tệp.
- **Video nguồn**: video trong kho đã qua kiểm duyệt an toàn (Chờ duyệt, Đã duyệt, Bị chặn); bài học
  chỉ dùng video Đã duyệt. Việc duyệt video thuộc tính năng khác.
- **Môn học / Lớp**: đã có từ tính năng quản lý danh mục; quyết định bài thuộc đâu và lớp nào hợp lệ.
- **Nhật ký quản trị**: bản ghi thao tác của quản trị viên trên bài học.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Quản trị viên tạo một bài nháp có 2 phần nội dung (1 video, 1 bài đọc) trong dưới 3 phút.
- **SC-002**: 100% bài ở trạng thái "Đã xuất bản" thỏa điều kiện xuất bản tại thời điểm xuất bản;
  không có bài nào được xuất bản khi thiếu nội dung.
- **SC-003**: 100% phần nội dung video trong bài tham chiếu video đã được duyệt an toàn tại thời điểm
  bài được xuất bản; video bị chặn sau đó được đánh dấu trong trang soạn bài và chặn việc xuất bản lại.
- **SC-004**: 100% thao tác thay đổi bài học có bản ghi tương ứng trong nhật ký quản trị.
- **SC-005**: Không có trường hợp mất thay đổi do hai quản trị viên cùng sửa một bài.
- **SC-006**: Danh sách bài và trang soạn bài hiển thị trong dưới 2 giây với một môn có 200 bài.

## Assumptions

- Chỉ vai trò Quản trị viên dùng tính năng này; vai trò Giáo viên/Kiểm duyệt chưa có quyền ghi.
- Bài được tạo từ trang "Môn học và bài học" đã có, theo môn đang chọn.
- Kho video nguồn và việc duyệt video ("Kênh gợi ý") là tính năng riêng; ở đây chỉ chọn video đã duyệt.
- Quản lý câu hỏi và quiz là tính năng riêng ("Câu hỏi và quiz"); tính năng này không tạo/sửa quiz.
- Phía bé và phụ huynh hiện vẫn đọc bài từ dữ liệu mẫu; việc chuyển các trang đó sang đọc bài đã xuất
  bản thật nằm ngoài phạm vi đợt này.
- Giới hạn thời lượng tối đa của bài để Sprint 5 (cùng tính năng giới hạn thời gian), áp lên thời
  lượng tự tính. Bài mẫu cũ giữ thời lượng đã nhập cho tới lần đầu nội dung của bài thay đổi.
- Tốc độ đọc 100 chữ mỗi phút là ước tính cho trẻ tiểu học; không tính thời gian dừng suy nghĩ hay
  làm quiz.
- Tải lên tài liệu và hình ảnh cho bài để đợt sau; dữ liệu mẫu đã có loại nội dung này vẫn hiển
  thị được nhưng không tạo mới được.
- Phân loại bài bằng thẻ (card Trello US-14) nằm ngoài phạm vi; xóa bài vẫn xóa theo các thẻ đã gắn
  (dữ liệu cũ) nhờ ràng buộc có sẵn trong database.
- Đối chiếu Trello (Sprint 2): User Story 1, 3, 4 thuộc card US-12 "Quản lý bài học"; User Story 2
  thuộc card US-13 "Gắn nội dung vào bài học".
- Xóa bài là vĩnh viễn và chỉ áp dụng cho bài nháp không có dữ liệu liên quan; các trường hợp khác
  dùng Lưu trữ.
