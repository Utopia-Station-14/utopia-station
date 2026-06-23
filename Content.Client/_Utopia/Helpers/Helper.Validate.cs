using System.Text.RegularExpressions;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Utopia.Helpers;

public static partial class UtopiaHelper
{
    private static readonly Regex LineEditPatternNumbers = new Regex("[^0-9]");

    public static void ValidateLineEditNumbers(LineEdit lineEdit, int length)
    {
        var text = LineEditPatternNumbers.Replace(lineEdit.Text, string.Empty);

        if (text.Length > length)
        {
            text = text[..length];
        }

        lineEdit.Text = text;
    }
}
