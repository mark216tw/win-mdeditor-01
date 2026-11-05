namespace VibeCode.MdEditor.Models;

public static class EditorFontCatalog
{
    public static IReadOnlyList<(string DisplayName, string FamilyName)> All { get; } =
    [
        ("新細明體", "PMingLiU"),
        ("標楷體", "DFKai-SB"),
        ("微軟正黑體", "Microsoft JhengHei"),
        ("Cascadia Mono", "Cascadia Mono"),
        ("Consolas", "Consolas"),
        ("Courier New", "Courier New"),
        ("Noto Sans TC", "Noto Sans TC"),
    ];
}
