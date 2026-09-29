namespace VietType.Core.Typing;

public readonly record struct InputTransformResult(bool Handled, string? OutputText, int BackspaceCount)
{
    public static InputTransformResult PassThrough() => new(false, null, 0);
    public static InputTransformResult Replace(string output, int backspaceCount) => new(true, output, backspaceCount);
}
