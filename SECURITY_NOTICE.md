# Thông báo về cảnh báo Windows Defender

**Trạng thái: đang xác minh — tạm dừng phân phối bản EXE.**

## Đã ghi nhận gì?

Ngày 19/10/2026, Microsoft Defender phát hiện file `Teck-PS1-VI-EN.exe` với tên **Trojan:Win32/Bearfoos.A!ml**, mức **Severe**. Cảnh báo xuất hiện sau khi tạo bản EXE. Ảnh lịch sử bảo vệ hiển thị trạng thái **Restored**, nghĩa là file đã được đưa ra khỏi vùng cách ly; trạng thái này không phải xác nhận an toàn.

## Vì sao xảy ra cảnh báo?

Chưa xác định được nguyên nhân cụ thể. Hiện chưa có kết quả phân tích từ Microsoft để xác nhận file là mã độc hay bị nhận diện nhầm.

Launcher đóng gói thư viện nạp mod, DLL mod bên thứ ba và plugin dịch; chức năng cài đặt ghi các thành phần vào thư mục game được chọn. Đây là mô tả cách hoạt động của ứng dụng, **không phải bằng chứng rằng các thao tác đó gây ra cảnh báo**. Tương tự, việc ứng dụng chưa ký số hoặc là công cụ mod không đủ để kết luận cảnh báo là báo nhầm.

## Đã kiểm tra đến đâu?

- Lệnh quét Microsoft Defender đối với thư mục các gói mod hiện có trả về “found no threats”. Kết quả này chỉ áp dụng cho lần quét đó và không xác nhận toàn bộ EXE an toàn.
- Lần quét riêng EXE chưa hoàn tất, trả về lỗi `0x80508023`; đây không phải kết quả “sạch”.
- Các kiểm thử cài/cập nhật/gỡ, bản dịch và build kiểm tra tính đúng của chương trình, không thay thế phân tích mã độc. Một số bản build trước đã được chạy để kiểm thử.
- Chưa có kết luận từ Microsoft và chưa hoàn thành phân tích bảo mật toàn bộ thành phần bên thứ ba.

## Cách xử lý hiện tại

- Tạm dừng đính kèm EXE đang bị phát hiện vào Releases; kho này cung cấp mã nguồn và tài liệu để xem xét.
- Không yêu cầu người dùng tắt Defender, thêm ngoại lệ hoặc khôi phục file đang bị cảnh báo.
- Nếu đã có EXE, tạm ngừng chạy và dùng Windows Security để cách ly/xử lý phát hiện. Nếu đã chạy, thực hiện Full scan; có thể chạy thêm Defender Offline để kiểm tra.
- Có thể gửi mẫu qua cổng phân tích của Microsoft. Chưa có mã hồ sơ hay kết quả gửi mẫu để công bố.

Tự build lại, đổi tên file hoặc không gặp cảnh báo trên một máy khác không đủ để chứng minh an toàn. Chỉ cập nhật trạng thái phân phối khi có thêm bằng chứng xác minh; hiện không cam kết thời điểm phát hành EXE trở lại.

## Nguồn tham khảo

- [Thông tin Bearfoos.A!ml của Microsoft](https://www.microsoft.com/en-us/wdsi/threats/malware-encyclopedia-description?name=Trojan%3AWin32%2FBearfoos.A%21ml)
- [Giải thích Protection history](https://support.microsoft.com/en-us/windows/security/windows-security/protection-history-in-the-windows-security-app)
- [Gửi mẫu cho Microsoft phân tích](https://www.microsoft.com/en-us/wdsi/filesubmission)
- [Hướng dẫn Microsoft Defender Offline](https://learn.microsoft.com/en-us/defender-endpoint/microsoft-defender-offline)
