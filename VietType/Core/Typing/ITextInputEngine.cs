namespace VietType.Core.Typing;

public interface ITextInputEngine
{
    bool Enabled { get; set; }
    bool ModernToneRemoval { get; set; }
    int SpellCheckLevel { get; set; }
    string GetBuffer();
    InputTransformResult ProcessKey(char c, bool isShift);
    InputTransformResult ProcessBackspace();
    InputTransformResult ProcessBoundary(char boundary);
    InputTransformResult RestoreWord();
    void Reset();
}
