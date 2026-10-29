''' <summary>
''' 插件宿主实现：把插件调用转接到启动器自身的设施上。
''' 所有方法都可在任意线程调用，UI 相关操作会自动切回 UI 线程。
''' </summary>
Friend NotInheritable Class PluginHostImpl
    Implements PluginApi.IPluginHost

    Private ReadOnly Def As ModPlugin.PluginDefinition

    Public Sub New(Definition As ModPlugin.PluginDefinition)
        Def = Definition
    End Sub

    Public ReadOnly Property PluginName As String Implements PluginApi.IPluginHost.PluginName
        Get
            Return Def.Name
        End Get
    End Property

    Public ReadOnly Property PluginFolder As String Implements PluginApi.IPluginHost.PluginFolder
        Get
            Return Def.Folder
        End Get
    End Property

    Public ReadOnly Property DataFolder As String Implements PluginApi.IPluginHost.DataFolder
        Get
            Dim Result As String = IO.Path.Combine(Def.Folder, "data")
            Try
                DirectoryUtils.Create(Result)
            Catch ex As Exception
                Logger.Warn(ex, $"创建插件数据目录失败（{Def.Name}）")
            End Try
            Return Result
        End Get
    End Property

    Public ReadOnly Property LauncherVersion As String Implements PluginApi.IPluginHost.LauncherVersion
        Get
            Return VersionDisplay
        End Get
    End Property

    Public ReadOnly Property ApiVersion As Integer Implements PluginApi.IPluginHost.ApiVersion
        Get
            Return PluginApi.PluginInfo.ApiVersion
        End Get
    End Property

    Public Sub Log(Text As String, Optional Level As PluginApi.PluginLogLevel = PluginApi.PluginLogLevel.Info) Implements PluginApi.IPluginHost.Log
        Dim Line As String = $"[插件:{Def.Name}] {Text}"
        Select Case Level
            Case PluginApi.PluginLogLevel.Warning
                Logger.Warn(Line)
            Case PluginApi.PluginLogLevel.Error
                Logger.Error(Line)
            Case Else
                Logger.Info(Line)
        End Select
    End Sub

    Public Sub Hint(Text As String) Implements PluginApi.IPluginHost.Hint
        RunInUi(Sub() ModMain.Hint(Text, HintType.Blue))
    End Sub

    Public Sub Alert(Text As String) Implements PluginApi.IPluginHost.Alert
        RunInUi(Sub() MyMsgBox(Text, "插件 · " & Def.Name))
    End Sub

    Public Sub OpenWebsite(Url As String) Implements PluginApi.IPluginHost.OpenWebsite
        RunInUi(Sub() ModBase.OpenWebsite(Url))
    End Sub

    Public Sub OpenFolder(Folder As String) Implements PluginApi.IPluginHost.OpenFolder
        RunInUi(
        Sub()
            Try
                Dim Target As String = Environment.ExpandEnvironmentVariables(Folder)
                If Not DirectoryUtils.Exists(Target) Then DirectoryUtils.Create(Target)
                OpenExplorer(Target)
            Catch ex As Exception
                Logger.Error(ex, "打开插件指定的目录失败", LogBehavior.Alert)
            End Try
        End Sub)
    End Sub

End Class
