''' <summary>
''' 插件系统。
''' 支持两种形式：
'''   1. .pclfePlugin 包（zip）：解压到 PCL\Plugins\&lt;名称&gt;\，内含 plugin.json 与 main.py，点击按钮时用 Python 执行。
'''   2. 旧版 .json 声明式插件：直接定义按钮动作（url / folder / command / hint），无需 Python。
''' </summary>
Public Module ModPlugin

    ''' <summary>
    ''' 插件按钮。
    ''' </summary>
    Public Class PluginButton
        ''' <summary>传给 Python 的动作标识（argv(1)）。</summary>
        Public Property Id As String = ""
        Public Property Text As String = ""
        ''' <summary>旧版声明式动作类型：url / folder / command / hint。</summary>
        Public Property Type As String = ""
        Public Property Value As String = ""
        Public Property ToolTip As String = ""
        ''' <summary>按钮样式：danger / highlight，可留空。</summary>
        Public Property Style As String = ""
    End Class

    ''' <summary>
    ''' 一个插件。
    ''' </summary>
    Public Class PluginDefinition
        Public Property Name As String = ""
        Public Property Version As String = "1.0.0"
        Public Property Author As String = ""
        Public Property Description As String = ""
        ''' <summary>Python 入口文件（相对插件目录），非空表示这是 Python 插件。</summary>
        Public Property Entry As String = ""
        ''' <summary>插件所在目录（Python 插件为解压后的目录）。</summary>
        Public Property Folder As String = ""
        ''' <summary>插件原始文件路径。</summary>
        Public Property SourcePath As String = ""
        Public Property Buttons As New List(Of PluginButton)

        Public ReadOnly Property IsPython As Boolean
            Get
                Return Not String.IsNullOrEmpty(Entry)
            End Get
        End Property
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
            '1. .pclfePlugin 包
            For Each PackagePath In DirectoryUtils.EnumerateFiles(PluginFolder, False)
                Dim CurrentPath As String = PackagePath.ToString
                If Not CurrentPath.EndsWithF(".pclfePlugin", True) Then Continue For
                Try
                    Dim TargetFolder As String = UnpackPackage(CurrentPath)
                    Dim Def As PluginDefinition = LoadPythonPlugin(TargetFolder, CurrentPath)
                    If Def IsNot Nothing Then Result.Add(Def)
                Catch ex As Exception
                    Logger.Warn(ex, $"插件包加载失败：{CurrentPath}")
                End Try
            Next
            '2. 旧版 .json
            For Each JsonPath In DirectoryUtils.EnumerateFiles(PluginFolder, False)
                Dim CurrentPath As String = JsonPath.ToString
                If Not CurrentPath.EndsWithF(".json", True) Then Continue For
                Try
                    Dim Def As PluginDefinition = LoadJsonPlugin(CurrentPath)
                    If Def IsNot Nothing Then Result.Add(Def)
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
    ''' 解压 .pclfePlugin 到 PCL\Plugins\&lt;名称&gt;\，包有更新时自动重新解压。
    ''' </summary>
    Private Function UnpackPackage(PackagePath As String) As String
        Dim FolderName As String = PathUtils.GetFileNameWithoutExtension(PackagePath)
        Dim Target As String = PluginFolder & FolderName & "\"
        Dim Marker As String = PluginFolder & "." & FolderName & ".unpacked"
        Dim Stamp As String = ""
        Try
            Stamp = FileUtils.GetInfo(PackagePath).LastWriteTimeUtc.ToString("o")
        Catch
        End Try
        Dim NeedUnpack As Boolean = False
        If Not DirectoryUtils.Exists(Target) Then
            NeedUnpack = True
        ElseIf Not FileUtils.Exists(Marker) Then
            NeedUnpack = True
        ElseIf FileUtils.ReadAsString(Marker) <> Stamp Then
            NeedUnpack = True
        End If
        If NeedUnpack Then
            If DirectoryUtils.Exists(Target) Then DirectoryUtils.Delete(Target)
            FileUtils.ExtractToDirectory(PackagePath, Target)
            Try
                FileUtils.Write(Marker, Stamp)
            Catch
            End Try
        End If
        Return Target
    End Function

    ''' <summary>
    ''' 读取 Python 插件（目录下的 plugin.json）。
    ''' </summary>
    Private Function LoadPythonPlugin(Folder As String, SourcePath As String) As PluginDefinition
        Dim JsonPath As String = Folder & "plugin.json"
        If Not FileUtils.Exists(JsonPath) Then Throw New Exception("插件包中缺少 plugin.json")
        Dim Json As JObject = FileUtils.ReadAsString(JsonPath).DeserializeJson()
        If Json Is Nothing Then Throw New Exception("plugin.json 无法解析")
        Dim Def As New PluginDefinition With {
            .Name = If(Json("name")?.ToString, ""),
            .Version = If(Json("version")?.ToString, "1.0.0"),
            .Author = If(Json("author")?.ToString, ""),
            .Description = If(Json("description")?.ToString, ""),
            .Entry = If(Json("entry")?.ToString, "main.py"),
            .Folder = Folder,
            .SourcePath = SourcePath
        }
        If String.IsNullOrWhiteSpace(Def.Name) Then Def.Name = PathUtils.GetFileNameWithoutExtension(SourcePath)
        Dim ButtonArray As JArray = TryCast(Json("buttons"), JArray)
        If ButtonArray IsNot Nothing Then
            For Each Item As JObject In ButtonArray
                Dim Button As New PluginButton With {
                    .Id = If(Item("id")?.ToString, ""),
                    .Text = If(Item("text")?.ToString, ""),
                    .ToolTip = If(Item("tooltip")?.ToString, ""),
                    .Style = If(Item("style")?.ToString, "")
                }
                If String.IsNullOrWhiteSpace(Button.Id) Then Button.Id = Button.Text
                If Not String.IsNullOrWhiteSpace(Button.Text) Then Def.Buttons.Add(Button)
            Next
        End If
        If Not Def.Buttons.Any() Then Return Nothing
        Return Def
    End Function

    ''' <summary>
    ''' 读取旧版声明式插件（.json）。
    ''' </summary>
    Private Function LoadJsonPlugin(JsonPath As String) As PluginDefinition
        Dim Json As JObject = FileUtils.ReadAsString(JsonPath).DeserializeJson()
        If Json Is Nothing Then Return Nothing
        Dim Def As New PluginDefinition With {
            .Name = If(Json("name")?.ToString, ""),
            .Version = If(Json("version")?.ToString, "1.0.0"),
            .Author = If(Json("author")?.ToString, ""),
            .Description = If(Json("description")?.ToString, ""),
            .SourcePath = JsonPath
        }
        If String.IsNullOrWhiteSpace(Def.Name) Then Def.Name = PathUtils.GetFileNameWithoutExtension(JsonPath)
        Dim ButtonArray As JArray = TryCast(Json("buttons"), JArray)
        If ButtonArray IsNot Nothing Then
            For Each Item As JObject In ButtonArray
                Dim Button As New PluginButton With {
                    .Id = If(Item("id")?.ToString, ""),
                    .Text = If(Item("text")?.ToString, ""),
                    .Type = If(Item("type")?.ToString, "url").Trim().ToLower(),
                    .Value = If(Item("value")?.ToString, ""),
                    .ToolTip = If(Item("tooltip")?.ToString, ""),
                    .Style = If(Item("style")?.ToString, "")
                }
                If Not String.IsNullOrWhiteSpace(Button.Text) Then Def.Buttons.Add(Button)
            Next
        End If
        If Not Def.Buttons.Any() Then Return Nothing
        Return Def
    End Function

    ''' <summary>
    ''' 执行插件按钮。必须在 UI 线程调用。
    ''' </summary>
    Public Sub ExecuteButton(Plugin As PluginDefinition, Button As PluginButton)
        If Plugin Is Nothing OrElse Button Is Nothing Then Return
        If Plugin.IsPython Then
            RunPythonPlugin(Plugin, Button)
            Return
        End If
        '旧版声明式动作
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

    ''' <summary>
    ''' 用 Python 执行插件入口，把按钮 Id 作为第一个参数传入。
    ''' </summary>
    Private Sub RunPythonPlugin(Plugin As PluginDefinition, Button As PluginButton)
        Dim Python As String = ResolvePython()
        If Python Is Nothing Then
            Hint("未找到 Python。请在 设置 → 系统 中填写 python.exe 的完整路径。", HintType.Red)
            Return
        End If
        Dim EntryPath As String = IO.Path.Combine(Plugin.Folder, Plugin.Entry)
        If Not FileUtils.Exists(EntryPath) Then
            Hint($"插件入口文件不存在：{Plugin.Entry}", HintType.Red)
            Return
        End If
        RunInThread(
        Sub()
            Try
                Dim Info As New ProcessStartInfo(Python)
                Info.Arguments = """" & EntryPath & """ """ & If(Button.Id, "")
                Info.WorkingDirectory = Plugin.Folder
                Info.UseShellExecute = False
                Info.RedirectStandardOutput = True
                Info.RedirectStandardError = True
                Info.CreateNoWindow = True
                Info.StandardOutputEncoding = Encoding.UTF8
                Info.StandardErrorEncoding = Encoding.UTF8
                Using Proc As System.Diagnostics.Process = System.Diagnostics.Process.Start(Info)
                    If Proc Is Nothing Then Throw New Exception("无法启动 Python 进程")
                    If Not Proc.WaitForExit(120000) Then
                        Try
                            Proc.Kill()
                        Catch
                        End Try
                        RunInUi(Sub() Hint($"插件「{Plugin.Name}」执行超时，已终止", HintType.Red))
                        Return
                    End If
                    Dim OutText As String = If(Proc.StandardOutput.ReadToEnd(), "").Trim()
                    Dim ErrText As String = If(Proc.StandardError.ReadToEnd(), "").Trim()
                    If Not String.IsNullOrEmpty(ErrText) Then
                        RunInUi(Sub() Hint($"插件「{Plugin.Name}」出错：{ErrText}", HintType.Red))
                    ElseIf Not String.IsNullOrEmpty(OutText) Then
                        RunInUi(Sub() MyMsgBox(OutText, Plugin.Name & " · " & Button.Text))
                    Else
                        RunInUi(Sub() Hint($"插件「{Plugin.Name}」已执行完成", HintType.Green))
                    End If
                End Using
            Catch ex As Exception
                Logger.Error(ex, $"运行 Python 插件失败（{Plugin.Name}）")
                RunInUi(Sub() Hint($"运行插件失败：{ex.Message}", HintType.Red))
            End Try
        End Sub)
    End Sub

    ''' <summary>
    ''' 查找可用的 Python：设置项 → PATH → 常见安装目录。
    ''' </summary>
    Public Function ResolvePython() As String
        '1. 用户指定
        Try
            Dim Custom As String = If(Settings.Get(Of String)("PluginPythonPath"), "").Trim()
            If Not String.IsNullOrEmpty(Custom) AndAlso FileUtils.Exists(Custom) Then Return Custom
        Catch
        End Try
        '2. PATH
        Try
            Dim PathVar As String = Environment.GetEnvironmentVariable("PATH")
            If Not String.IsNullOrEmpty(PathVar) Then
                Dim Names As String() = {"python.exe", "python3.exe"}
                For Each Dir In PathVar.Split(";"c)
                    If String.IsNullOrWhiteSpace(Dir) Then Continue For
                    For Each Name In Names
                        Try
                            Dim Full As String = IO.Path.Combine(Dir.Trim(), Name)
                            If IO.File.Exists(Full) Then Return Full
                        Catch
                        End Try
                    Next
                Next
            End If
        Catch
        End Try
        '3. 常见安装目录
        Try
            Dim Roots As New List(Of String)
            Roots.Add(IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs\Python"))
            Roots.Add("C:\")
            For Each Root In Roots
                If Not IO.Directory.Exists(Root) Then Continue For
                For Each SubFolder In IO.Directory.GetDirectories(Root, "Python3*")
                    Try
                        Dim Exe As String = IO.Path.Combine(SubFolder, "python.exe")
                        If IO.File.Exists(Exe) Then Return Exe
                    Catch
                    End Try
                Next
            Next
        Catch
        End Try
        Return Nothing
    End Function

End Module
