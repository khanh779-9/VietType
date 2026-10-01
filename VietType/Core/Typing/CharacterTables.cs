using System;

namespace VietType.Core.Typing;

public static class CharacterTables
{
    // Tên file .txt trong thư mục Data\EncodingTables (chỉ ASCII, ngăn cách bằng _),
    // thứ tự phần tử tương ứng 1-1 với PredefinedTableNames.
    public static readonly string[] PredefinedTableFiles =
    [
        "BK_HCM_1",
        "BK_HCM_2",
        "NCR_Decimal",
        "NCR_Hex",
        "TCVN3_ABC",
        "UTF_8",
        "Unicode",
        "Unicode_C_string_Decimal",
        "Unicode_C_string_Hex",
        "Unicode_to_hop",
        "VIQR",
        "VISCII",
        "VNI_Windows",
        "VPS",
        "Vietware_F",
        "Vietware_X",
        "Windows_1258_codepage"
    ];

    public static readonly string[] PredefinedTableNames =
    [
        "BK HCM 1",
        "BK HCM 2",
        "NCR Decimal",
        "NCR Hex",
        "TCVN3 (ABC)",
        "UTF-8",
        "Unicode",
        "Unicode C string Decimal",
        "Unicode C string Hex",
        "Unicode tổ hợp",
        "VIQR",
        "VISCII",
        "VNI Windows",
        "VPS",
        "Vietware F",
        "Vietware X",
        "Windows 1258 codepage"
    ];

    public static readonly string[] Unicode =
    {
        "a", "á", "à", "ã", "ả", "ạ", "ă", "ắ", "ằ", "ẵ", "ẳ", "ặ",
        "â", "ấ", "ầ", "ẫ", "ẩ", "ậ", "e", "é", "è", "ẽ", "ẻ", "ẹ",
        "ê", "ế", "ề", "ễ", "ể", "ệ", "i", "í", "ì", "ĩ", "ỉ", "ị",
        "o", "ó", "ò", "õ", "ỏ", "ọ", "ô", "ố", "ồ", "ỗ", "ổ", "ộ",
        "ơ", "ớ", "ờ", "ỡ", "ở", "ợ", "u", "ú", "ù", "ũ", "ủ", "ụ",
        "ư", "ứ", "ừ", "ữ", "ử", "ự", "y", "ý", "ỳ", "ỹ", "ỷ", "ỵ",
        "đ", "A", "Á", "À", "Ã", "Ả", "Ạ", "Ă", "Ắ", "Ằ", "Ẵ", "Ẳ",
        "Ặ", "Â", "Ấ", "Ầ", "Ẫ", "Ẩ", "Ậ", "E", "É", "È", "Ẽ", "Ẻ",
        "Ẹ", "Ê", "Ế", "Ề", "Ễ", "Ể", "Ệ", "I", "Í", "Ì", "Ĩ", "Ỉ",
        "Ị", "O", "Ó", "Ò", "Õ", "Ỏ", "Ọ", "Ô", "Ố", "Ồ", "Ỗ", "Ổ",
        "Ộ", "Ơ", "Ớ", "Ờ", "Ỡ", "Ở", "Ợ", "U", "Ú", "Ù", "Ũ", "Ủ",
        "Ụ", "Ư", "Ứ", "Ừ", "Ữ", "Ử", "Ự", "Y", "Ý", "Ỳ", "Ỹ", "Ỷ",
        "Ỵ", "Đ",
    };

    public static string Convert(string input, string[] sourceTable, string[] destTable)
    {
        if (string.IsNullOrEmpty(input) || sourceTable == null || destTable == null) return input;
        int len = Math.Min(sourceTable.Length, destTable.Length);
        var sb = new System.Text.StringBuilder(input);
        for (int i = 0; i < len; i++)
        {
            sb.Replace(sourceTable[i], destTable[i]);
        }
        return sb.ToString();
    }
}
