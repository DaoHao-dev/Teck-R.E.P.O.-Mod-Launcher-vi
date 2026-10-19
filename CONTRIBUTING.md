# Báo lỗi và đóng góp

## Báo lỗi

Mở tab **Issues → New issue → Báo lỗi** và điền mẫu. Một báo lỗi dễ xử lý cần:

- Phiên bản launcher và phiên bản R.E.P.O.
- Các bước gây lỗi, kết quả mong muốn và kết quả thực tế.
- Danh sách mod khác đang dùng, nếu có.
- Ảnh chụp; nếu chữ bị khuất, thêm độ phân giải và mức phóng to giao diện.

Ảnh hoặc log nên chỉ chứa thông tin liên quan đến lỗi; bạn có thể che SteamID và đường dẫn cá nhân trước khi đăng công khai.

## Đề xuất cải tiến

Chọn mẫu **Đề xuất cải tiến**, mô tả tình huống sử dụng và thay đổi bạn mong muốn. Với bản dịch, ghi nhãn hiện tại và cách viết đề xuất.

## Thay đổi mã nguồn

Mô tả thay đổi, lý do và cách kiểm tra trong pull request. Nếu sửa launcher, chạy `build.ps1` và kiểm tra self-test, ảnh xem trước. Nếu sửa bảng dịch, cần biên dịch lại plugin dịch và đóng gói lại các mod liên quan trước khi thay EXE; chỉ chạy build launcher sẽ tiếp tục nhúng gói hiện có.
