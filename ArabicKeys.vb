' ---------------------------------------------------------------------------
'  Arabic keyboard layout (Windows "Arabic 101").
'  Lets a physical keyboard select letters whether Windows is set to Arabic
'  or to English: an English key press is translated to the Arabic letter
'  printed on the same key.
' ---------------------------------------------------------------------------
Public Module ArabicKeys

    ''' <summary>On-screen keyboard rows, left to right as on a real keyboard.</summary>
    Public ReadOnly Rows As String()() = {
        New String() {"ذ", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"},
        New String() {"ض", "ص", "ث", "ق", "ف", "غ", "ع", "ه", "خ", "ح", "ج", "د"},
        New String() {"ش", "س", "ي", "ب", "ل", "أ", "ت", "ن", "م", "ك", "ط"},
        New String() {"ئ", "ء", "ؤ", "ر", "ى", "ة", "و", "ز", "ظ"}}

    ' English key -> Arabic letter on the same physical key
    Private ReadOnly LatinToArabic As New Dictionary(Of Char, String) From {
        {"`"c, "ذ"},
        {"q"c, "ض"}, {"w"c, "ص"}, {"e"c, "ث"}, {"r"c, "ق"}, {"t"c, "ف"}, {"y"c, "غ"},
        {"u"c, "ع"}, {"i"c, "ه"}, {"o"c, "خ"}, {"p"c, "ح"}, {"["c, "ج"}, {"]"c, "د"},
        {"a"c, "ش"}, {"s"c, "س"}, {"d"c, "ي"}, {"f"c, "ب"}, {"g"c, "ل"}, {"h"c, "أ"},
        {"j"c, "ت"}, {"k"c, "ن"}, {"l"c, "م"}, {";"c, "ك"}, {"'"c, "ط"},
        {"z"c, "ئ"}, {"x"c, "ء"}, {"c"c, "ؤ"}, {"v"c, "ر"}, {"n"c, "ى"}, {"m"c, "ة"},
        {","c, "و"}, {"."c, "ز"}, {"/"c, "ظ"}}

    ''' <summary>
    ''' The text shown for a label. The letter ه on its own looks like a zero or a circle,
    ''' so it is shown with a tatweel after it: هـ
    ''' </summary>
    Public Function Display(label As String) As String
        If label = "ه" Then Return "هـ"
        Return label
    End Function

    ''' <summary>The Al-Arabiya font if it is installed (it goes by several names), otherwise Tahoma.</summary>
    Public Function DefaultLetterFont() As String
        Dim candidates = {"Alarabiya Font", "AlArabiya Font", "Al-Arabiya", "AlArabiya", "Al Arabiya", "Alarabiya"}
        Dim installed = New HashSet(Of String)(System.Drawing.FontFamily.Families.Select(Function(f) f.Name),
                                               StringComparer.OrdinalIgnoreCase)
        For Each c In candidates
            If installed.Contains(c) Then
                Return System.Drawing.FontFamily.Families.First(Function(f) String.Equals(f.Name, c, StringComparison.OrdinalIgnoreCase)).Name
            End If
        Next
        ' any installed family whose name contains "arabiya"
        Dim fam = System.Drawing.FontFamily.Families.FirstOrDefault(Function(f) f.Name.IndexOf("arabiya", StringComparison.OrdinalIgnoreCase) >= 0)
        Return If(fam IsNot Nothing, fam.Name, "Tahoma")
    End Function

    ''' <summary>
    ''' Turns a typed character into a board label: an Arabic letter, a digit "0".."9",
    ''' or Nothing if the key means nothing on the board.
    ''' </summary>
    Public Function Translate(ch As Char) As String
        ' Arabic-Indic digits ٠..٩ -> 0..9
        If ch >= ChrW(&H660) AndAlso ch <= ChrW(&H669) Then Return ChrW(AscW("0"c) + AscW(ch) - &H660).ToString()
        If Char.IsDigit(ch) AndAlso AscW(ch) < 128 Then Return ch.ToString()
        ' every form of alef is treated as أ, the letter used on the boards
        If ch = "ا"c OrElse ch = "إ"c OrElse ch = "آ"c OrElse ch = "أ"c Then Return "أ"
        If AscW(ch) >= &H621 AndAlso AscW(ch) <= &H64A Then Return ch.ToString()
        Dim lower = Char.ToLowerInvariant(ch)
        Dim ar As String = Nothing
        If LatinToArabic.TryGetValue(lower, ar) Then Return ar
        Return Nothing
    End Function
End Module
