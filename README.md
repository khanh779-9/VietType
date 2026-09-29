# VietType

<div align="center">

![VietType Logo](VietType/Resources/VietType-Logo.svg)

**Bộ gõ tiếng Việt hiện đại, mượt mà và nhẹ nhàng dành cho hệ điều hành Windows.**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078D6.svg)](https://microsoft.com/windows)
[![WPF](https://img.shields.io/badge/UI-WPF%20XAML-00599E.svg)]()

</div>

---

## Giới thiệu

VietType là bộ gõ tiếng Việt được thiết kế và phát triển bằng C# trên nền tảng .NET 8 và WPF. Với triết lý đơn giản, hiện đại và tối ưu hiệu năng, VietType mang lại trải nghiệm gõ phím mượt mà, phản hồi tức thì và tương thích tối đa với các ứng dụng Windows 10/11.

---

## Tính năng nổi bật

- **Giao diện Fluent tối giản và hiện đại**:
  - Hỗ trợ đầy đủ chủ đề Sáng (Light), Tối (Dark) và Tự động theo hệ thống Windows (Auto).
  - Bo góc tinh tế, phân tách bố cục rõ ràng theo từng trang chức năng.

- **Hỗ trợ đầy đủ các kiểu gõ thông dụng**:
  - Telex
  - VNI
  - VIQR
  - Telex mở rộng

- **Hỗ trợ 17 Bảng mã tiếng Việt chuẩn hóa**:
  - Unicode (Dựng sẵn), Unicode (Tổ hợp)
  - TCVN3 (ABC), VNI Windows, VIQR
  - Vietnamese locale CP 1258
  - BKHCM 1, BKHCM 2
  - Vietware X, Vietware F
  - Unicode UTF-8, Unicode NCR Decimal, Unicode NCR Hex, Unicode C String
  - TCVN 6064, VPS, VISCII

- **Gợi ý từ thông minh (Auto Complete)**:
  - Dự đoán và gợi ý từ vựng tiếng Việt nhanh chóng, hỗ trợ tăng tốc độ soạn thảo văn bản.

- **Trang Gõ tắt và Phím tắt chuyên biệt**:
  - Gõ tắt (Shortcuts / Macro): Tự động thay thế từ viết tắt thành văn bản hoàn chỉnh.
  - Phím tắt (Hotkeys): Tùy biến tổ hợp phím chuyển đổi chế độ gõ và thao tác nhanh.

- **Kiểm tra chính tả và phục hồi từ gốc**:
  - Nhận diện lỗi từ vựng tiếng Việt theo thời gian thực.
  - Tự động hoàn tác/khôi phục từ gốc khi phát hiện sai hoặc khi người dùng chỉnh sửa.

- **Tương thích quyền Administrator**:
  - Hỗ trợ chạy với quyền Quản trị viên để gõ mượt mà trên các ứng dụng nâng cao, IDE, Terminal và Game.

- **Menu khay hệ thống (System Tray) tiện lợi**:
  - Truy cập nhanh kiểu gõ, bảng mã, bật/tắt gợi ý và các tùy chọn trực tiếp từ khay hệ thống với thiết kế phẳng, liền mạch.

---

## Cấu trúc dự án

```text
VietType/
├── LICENSE                         # Giấy phép mã nguồn mở MIT
├── README.md                       # Tài liệu dự án
├── VietType.slnx                   # File giải pháp Solution .NET
└── VietType/
    ├── VietType.csproj             # Dự án WPF .NET 8.0-windows
    ├── App.xaml / App.xaml.cs      # Điểm khởi chạy ứng dụng và quản lý vòng đời
    ├── MainWindow.xaml / .cs       # Cửa sổ chính, Navigation, Tray Icon Menu
    ├── Core/                       # Bộ lõi xử lý gõ phím và giải thuật tiếng Việt
    │   ├── Engine/                 # Keyboard hook, engine xử lý phím
    │   ├── Models/                 # Dữ liệu từ điển, cấu hình gõ
    │   └── Encodings/              # Logic chuyển đổi ký tự và mã hóa
    ├── Controls/                   # Các Custom Control và giao diện người dùng
    ├── Pages/                      # Các trang cấu hình giao diện
    │   ├── HomePage.xaml           # Bảng điều khiển chính (Kiểu gõ, Bảng mã)
    │   ├── ShortcutsPage.xaml      # Quản lý danh sách gõ tắt (Macro)
    │   ├── HotkeysPage.xaml        # Cài đặt tổ hợp phím tắt chức năng
    │   ├── InputPage.xaml          # Tùy chọn gõ và gợi ý từ vựng
    │   ├── AdvancedPage.xaml       # Cấu hình nâng cao, khởi động cùng Windows
    │   └── AboutPage.xaml          # Thông tin tác giả và bản quyền
    ├── Data/
    │   └── EncodingTables/         # Tập hợp 17 bảng mã tiếng Việt
    ├── Infrastructure/             # Lưu trữ cài đặt, Registry, IO
    ├── Platform/                   # Win32 API, Elevation Helper
    ├── Themes/                     # Bộ màu, Style XAML, Light/Dark Mode
    └── Resources/                  # Vector icon SVG, font chữ, logo ứng dụng
```

---

## Hướng dẫn cài đặt và chạy ứng dụng

### Yêu cầu hệ thống
- Hệ điều hành: Windows 10 / 11 (x64 / ARM64).
- Bộ công cụ phát triển: [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) hoặc mới hơn.
- Môi trường khuyến nghị: Visual Studio 2022 / Visual Studio Code với C# Dev Kit.

### Các bước biên dịch

1. Khôi phục phụ thuộc (Restore dependencies):
   ```bash
   dotnet restore VietType.slnx
   ```

2. Biên dịch dự án (Build):
   ```bash
   dotnet build VietType.slnx -c Release
   ```

3. Chạy ứng dụng (Run):
   ```bash
   dotnet run --project VietType/VietType.csproj
   ```

---

## Bản quyền

Dự án được phân phối dưới giấy phép mã nguồn mở [MIT License](LICENSE).

---

## Tác giả

- Tran Khanh ([@khanh779-9](https://github.com/khanh779-9))
