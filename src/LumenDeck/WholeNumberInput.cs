using System.Globalization;

namespace LumenDeck;

// Native parsing clamps or substitutes invalid text. Preserve the draft until
// the user repairs it, including on blur, Enter, arrow keys and spinner clicks.
internal sealed class WholeNumberInput : NumericUpDown
{
    public bool TryGetDraft(out int value)
    {
        var culture = CultureInfo.CurrentCulture;
        var style = NumberStyles.Integer | (ThousandsSeparator ? NumberStyles.AllowThousands : NumberStyles.None);
        if (!int.TryParse(Text, style, culture, out value) || value < Minimum || value > Maximum) return false;
        string separator = culture.NumberFormat.NumberGroupSeparator;
        // Accept native grouped formatting, but never reinterpret malformed groups
        // such as 12,5 as 125 in a culture whose group separator is a comma.
        return !ThousandsSeparator || separator.Length == 0 || !Text.Contains(separator) ||
            Text.Trim() == value.ToString("N0", culture);
    }

    protected override void ValidateEditText()
    {
        if (TryGetDraft(out _)) base.ValidateEditText();
    }

    protected override void UpdateEditText()
    {
        if (!UserEdit || TryGetDraft(out _)) base.UpdateEditText();
    }

    public override void UpButton() { if (TryGetDraft(out _)) base.UpButton(); }
    public override void DownButton() { if (TryGetDraft(out _)) base.DownButton(); }
}

