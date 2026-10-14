Imports System.Windows.Forms

Public Module Program
    <STAThread>
    Public Sub Main()
#If NET Then
        Application.SetHighDpiMode(HighDpiMode.SystemAware)
#End If
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New MainForm())
    End Sub
End Module
