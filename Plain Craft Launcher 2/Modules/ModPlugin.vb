''' <summary>
''' 插件系统：从 PCL\Plugins\*.json 加载声明式插件，并在百宝箱页生成按钮。
''' 插件只能执行受限操作（打开网址 / 打开文件夹 / 运行命令 / 显示提示），不会加载任意代码。
''' </summary>
Public Module ModPlugin

    ''' <summary>
    ''' 插件中的一个按钮。
    ''' </summary>
    Public Class PluginButton
        ''' <summary>按钮显示的文字。</summary>
        Public Property Text As String = ""
        ''' <summary>操作类型：url（打开网址）、folder（打开文件夹）、command（运行命令）、hint（显示提示）。</summary>
        Public Property Type As String = "url"
        ''' <summary>操作参数。</summary>
        Public Property Value As String = ""
        ''' <summary>鼠标悬停提示，可留空。</summary>
        Public Property ToolTip As String = ""
    End Class

    ''' <summary>
    ''' 一个插件。
    ''' </summary>
    Public Class PluginDefinition
        Public Property Name As String = ""
        Public Property Version As String = "1.0.0"
        Public Property Author As String = ""
        Public Property Description As String = ""
        Public Property FilePath As String = ""
        Public Property Buttons As New List(Of PluginButton)
    End Class

    ''' <summary>
    ''' 插件目录：PCL\Plugins\。
    ''' </summary>
    Public ReadOnly Property PluginFolder As String
        Get
            Return Paths.Base & "PCL\Plugins\"
        End Get
    End Property

    ''' <summary>
    ''' 加载全部插件。单个插件出错不会影响其他插件。
    ''' </summary>
    Public Function LoadAll() As List(Of PluginDefinition)
        Dim Result As New List(Of PluginDefinition)
        Try
            DirectoryUtils.Create(PluginFolder)
            For Each FilePath In DirectoryUtils.EnumerateFiles(PluginFolder, False)
                Dim CurrentPath As String = FilePath.ToString
                If Not CurrentPath.EndsWithF(".json", True) Then Continue For
                Try
                    Dim Json As JObject = FileUtils.ReadAsString(CurrentPath).DeserializeJson()
                    If Json Is Nothing Then Continue For
                    Dim Def As New PluginDefinition With {
                        .Name = If(Json("name")?.ToString, ""),
                        .Version = If(Json("version")?.ToString, "1.0.0"),
                        .Author = If(Json("author")?.ToString, ""),
                        .Description = If(Json("description")?.ToString, ""),
                        .FilePath = CurrentPath
                    }
                    If String.IsNullOrWhiteSpace(Def.Name) Then
                        Def.Name = PathUtils.GetFileNameWithoutExtension(CurrentPath)
                    End If
                    Dim ButtonArray As JArray = TryCast(Json("buttons"), JArray)
                    If ButtonArray IsNot Nothing Then
                        For Each Item As JObject In ButtonArray
                            Dim Button As New PluginButton With {
                                .Text = If(Item("text")?.ToString, ""),
                                .Type = If(Item("type")?.ToString, "url").Trim().ToLower(),
                                .Value = If(Item("value")?.ToString, ""),
                                .ToolTip = If(Item("tooltip")?.ToString, "")
                            }
                            If Not String.IsNullOrWhiteSpace(Button.Text) Then Def.Buttons.Add(Button)
                        Next
                    End If
                    If Def.Buttons.Any() Then Result.Add(Def)
                Catch ex As Exception
                    Logger.Warn(ex, $"插件加载失败：{CurrentPath}")
                End Try
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取插件目录失败")
        End Try
        Return Result
    End Function

    ''' <summary>
    ''' 执行插件按钮。必须在 UI 线程调用。
    ''' </summary>
    Public Sub ExecuteButton(Button As PluginButton)
        If Button Is Nothing Then Return
        Try
            Dim Value As String = Environment.ExpandEnvironmentVariables(If(Button.Value, "").Trim())
            Select Case If(Button.Type, "url").Trim().ToLower()
                Case "url"
                    If String.IsNullOrEmpty(Value) Then Throw New Exception("按钮未填写网址")
                    OpenWebsite(Value)
                Case "folder"
                    If String.IsNullOrEmpty(Value) Then Throw New Exception("按钮未填写路径")
                    If Not DirectoryUtils.Exists(Value) Then DirectoryUtils.Create(Value)
                    OpenExplorer(Value)
                Case "command"
                    If String.IsNullOrEmpty(Value) Then Throw New Exception("按钮未填写命令")
                    System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo(Value) With {.UseShellExecute = True})
                Case "hint"
                    Hint(Value, HintType.Blue)
                Case Else
                    Throw New Exception("未知的按钮类型：" & Button.Type)
            End Select
        Catch ex As Exception
            Logger.Error(ex, $"执行插件按钮失败（{Button.Text}）", LogBehavior.Alert)
        End Try
    End Sub

End Module
