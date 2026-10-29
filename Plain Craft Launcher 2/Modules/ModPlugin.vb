''' <summary>
''' 插件系统。
''' 支持四种形式：
'''   1. VB.NET 插件（推荐）：.pclfePlugin 包（zip）内包含 plugin.json 与 plugin.dll，由启动器反射加载。
'''   2. Python 插件（旧版）：包内含 plugin.json 与 main.py，点击按钮时用 Python 执行。
'''   3. 声明式插件（旧版）：.json 直接定义按钮动作（url / folder / command / hint）。
'''   4. 内置插件：无对应文件，始终存在且不可删除。
''' VB.NET 插件的编写与构建见仓库根目录的 grand 工具（grand new / grand build / grand pack）。
''' </summary>
Public Module ModPlugin

    ''' <summary>
    ''' 插件类型。
    ''' </summary>
    Public Enum PluginKind
        ''' <summary>旧版声明式（.json）。</summary>
        Declarative = 0
        ''' <summary>旧版 Python（main.py）。</summary>
        Python = 1
        ''' <summary>VB.NET 插件（plugin.dll）。</summary>
        DotNet = 2
        ''' <summary>启动器内置。</summary>
        BuiltIn = 3
    End Enum

    ''' <summary>
    ''' 插件按钮。
    ''' </summary>
    Public Class PluginButton
        ''' <summary>动作标识，点击时传给插件的 Execute（Python 插件为 argv(1)）。</summary>
        Public Property Id As String = ""
        Public Property Text As String = ""
        ''' <summary>旧版声明式动作类型：url / folder / command / file / hint。</summary>
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
        Public Property Kind As PluginKind = PluginKind.Declarative
        ''' <summary>入口文件（相对插件目录）。.dll 表示 VB.NET 插件，.py 表示旧版 Python 插件。</summary>
        Public Property Entry As String = ""
        ''' <summary>插件所在目录（末尾带反斜杠）。</summary>
        Public Property Folder As String = ""
        ''' <summary>插件原始文件路径。</summary>
        Public Property SourcePath As String = ""
        ''' <summary>是否为启动器内置插件（无对应文件，不可删除）。</summary>
        Public Property IsBuiltIn As Boolean = False
        Public Property Buttons As New List(Of PluginButton)
        ''' <summary>VB.NET 插件实例，其它类型为 Nothing。</summary>
        Public Property Instance As PluginApi.IPlugin
        ''' <summary>VB.NET 插件宿主，其它类型为 Nothing。</summary>
        Public Property Host As PluginApi.IPluginHost
        ''' <summary>是否已调用过插件的 Load（被禁用的插件不会加载）。</summary>
        Public Property IsLoaded As Boolean = False
        ''' <summary>实例缓存键，用于刷新时复用同一个插件实例。</summary>
        Public Property CacheKey As String = ""

        Public ReadOnly Property IsPython As Boolean
            Get
                Return Kind = PluginKind.Python
            End Get
        End Property

        Public ReadOnly Property IsDotNet As Boolean
            Get
                Return Kind = PluginKind.DotNet
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

    ''' <summary>VB.NET 插件实例缓存，键为「dll 路径|文件时间戳大小」，避免重复加载程序集与重复调用 Load。</summary>
    Private ReadOnly DotNetCache As New Dictionary(Of String, PluginDefinition)
    ''' <summary>当前可接收游戏事件的插件。</summary>
    Private ReadOnly ActivePlugins As New List(Of PluginDefinition)
    Private AssemblyResolverReady As Boolean = False

    ''' <summary>
    ''' 加载全部插件。单个插件出错不会影响其他插件。
    ''' </summary>
    Public Function LoadAll() As List(Of PluginDefinition)
        Dim Result As New List(Of PluginDefinition)
        Try
            DirectoryUtils.Create(PluginFolder)
            EnsureAssemblyResolver()
            '1. .pclfePlugin 包（VB.NET 或 Python）
            For Each PackagePath In DirectoryUtils.EnumerateFiles(PluginFolder, False)
                Dim CurrentPath As String = PackagePath.ToString
                If Not CurrentPath.EndsWithF(".pclfePlugin", True) Then Continue For
                Try
                    Dim TargetFolder As String = UnpackPackage(CurrentPath)
                    Dim Def As PluginDefinition = LoadPackage(TargetFolder, CurrentPath)
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
        '内置插件（始终存在，不可删除）
        Try
            Result.Insert(0, GetBuiltInPlugin())
        Catch ex As Exception
            Logger.Warn(ex, "加载内置插件失败")
        End Try
        SyncActivePlugins(Result)
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
            Dim PkgInfo = FileUtils.GetInfo(PackagePath)
            Stamp = PkgInfo.LastWriteTime.ToString("o") & "|" & PkgInfo.Length.ToString()
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
            '插件更新会重建解压目录，先把 data 目录挪出去，解压完再放回来，避免插件数据丢失
            Dim DataFolder As String = IO.Path.Combine(Target, "data")
            Dim DataBackup As String = PluginFolder & "." & FolderName & ".data\"
            Dim HasData As Boolean = DirectoryUtils.Exists(DataFolder)
            If HasData Then DirectoryUtils.Move(DataFolder, DataBackup)
            If DirectoryUtils.Exists(Target) Then DirectoryUtils.Delete(Target)
            FileUtils.ExtractToDirectory(PackagePath, Target)
            If HasData AndAlso DirectoryUtils.Exists(DataBackup) Then DirectoryUtils.Move(DataBackup, DataFolder)
            Try
                FileUtils.Write(Marker, Stamp)
            Catch
            End Try
        End If
        Return Target
    End Function

    ''' <summary>
    ''' 读取解压后的插件目录：优先按 VB.NET 插件处理，其次 Python。
    ''' </summary>
    Private Function LoadPackage(Folder As String, SourcePath As String) As PluginDefinition
        Dim JsonPath As String = Folder & "plugin.json"
        If Not FileUtils.Exists(JsonPath) Then Throw New Exception("插件包中缺少 plugin.json")
        Dim Json As JObject = FileUtils.ReadAsString(JsonPath).DeserializeJson()
        If Json Is Nothing Then Throw New Exception("plugin.json 无法解析")
        Dim Entry As String = If(Json("entry")?.ToString, "").Trim()
        '确定 DLL 路径：优先用 entry 指定的，其次用约定名 plugin.dll
        Dim DllPath As String = ""
        If Entry.EndsWithF(".dll", True) Then
            DllPath = IO.Path.Combine(Folder, Entry)
        ElseIf FileUtils.Exists(IO.Path.Combine(Folder, "plugin.dll")) Then
            DllPath = IO.Path.Combine(Folder, "plugin.dll")
        End If
        If Not String.IsNullOrEmpty(DllPath) Then Return LoadDotNetPlugin(Folder, SourcePath, DllPath, Json)
        Return LoadPythonPlugin(Folder, SourcePath)
    End Function

    ''' <summary>
    ''' 加载 VB.NET 插件：反射实例化、读取元数据与按钮，并调用 Load。
    ''' </summary>
    Private Function LoadDotNetPlugin(Folder As String, SourcePath As String, DllPath As String, Json As JObject) As PluginDefinition
        If Not FileUtils.Exists(DllPath) Then Throw New Exception("插件包中缺少 " & IO.Path.GetFileName(DllPath))
        '缓存键：同一文件不重复加载
        Dim Stamp As String = ""
        Try
            Dim DllInfo = FileUtils.GetInfo(DllPath)
            Stamp = DllInfo.LastWriteTime.ToString("o") & "|" & DllInfo.Length.ToString()
        Catch
        End Try
        Dim Key As String = DllPath & "|" & Stamp
        Dim Cached As PluginDefinition = Nothing
        If DotNetCache.TryGetValue(Key, Cached) Then
            ApplyManifest(Cached, Json)
            TryLoadPlugin(Cached)
            Return Cached
        End If
        Dim Def As New PluginDefinition With {
            .Kind = PluginKind.DotNet,
            .Entry = IO.Path.GetFileName(DllPath),
            .Folder = Folder,
            .SourcePath = SourcePath,
            .CacheKey = Key
        }
        ApplyManifest(Def, Json)
        '用字节加载，避免插件文件被占用（否则无法更新与删除）
        Dim Asm As Reflection.Assembly = Reflection.Assembly.Load(FileUtils.ReadAsBytes(DllPath))
        Dim PluginType As Type = FindPluginType(Asm)
        If PluginType Is Nothing Then Throw New Exception("程序集中没有找到插件类型（需继承 PluginApi.PluginBase 或实现 PluginApi.IPlugin）")
        Dim Inst As PluginApi.IPlugin = DirectCast(Activator.CreateInstance(PluginType), PluginApi.IPlugin)
        Def.Instance = Inst
        '元数据：清单没写的，用插件自身声明的补上
        If String.IsNullOrWhiteSpace(Def.Name) Then Def.Name = Inst.Name
        If String.IsNullOrWhiteSpace(Def.Version) Then Def.Version = Inst.Version
        If String.IsNullOrWhiteSpace(Def.Author) Then Def.Author = Inst.Author
        If String.IsNullOrWhiteSpace(Def.Description) Then Def.Description = Inst.Description
        If String.IsNullOrWhiteSpace(Def.Name) Then Def.Name = PathUtils.GetFileNameWithoutExtension(SourcePath)
        '按钮
        Try
            Dim Actions As IEnumerable(Of PluginApi.PluginAction) = Inst.GetActions()
            If Actions IsNot Nothing Then
                For Each ActionItem In Actions
                    If ActionItem Is Nothing OrElse String.IsNullOrWhiteSpace(ActionItem.Text) Then Continue For
                    Dim Item As New PluginButton With {
                        .Id = If(String.IsNullOrWhiteSpace(ActionItem.Id), ActionItem.Text, ActionItem.Id),
                        .Text = ActionItem.Text,
                        .ToolTip = If(ActionItem.ToolTip, ""),
                        .Style = If(ActionItem.Style, ""),
                        .Type = "dotnet"
                    }
                    Def.Buttons.Add(Item)
                Next
            End If
        Catch ex As Exception
            Logger.Warn(ex, $"读取插件按钮失败（{Def.Name}）")
        End Try
        Def.Host = New PluginHostImpl(Def)
        DotNetCache(Key) = Def
        TryLoadPlugin(Def)
        Return Def
    End Function

    ''' <summary>
    ''' 从清单补全元数据，只覆盖清单中确实写了的字段。
    ''' </summary>
    Private Sub ApplyManifest(Def As PluginDefinition, Json As JObject)
        If Json Is Nothing Then Return
        Dim Name As String = Json("name")?.ToString
        Dim Version As String = Json("version")?.ToString
        Dim Author As String = Json("author")?.ToString
        Dim Description As String = Json("description")?.ToString
        If Not String.IsNullOrWhiteSpace(Name) Then Def.Name = Name.Trim()
        If Not String.IsNullOrWhiteSpace(Version) Then Def.Version = Version.Trim()
        If Not String.IsNullOrWhiteSpace(Author) Then Def.Author = Author.Trim()
        If Not String.IsNullOrWhiteSpace(Description) Then Def.Description = Description.Trim()
    End Sub

    ''' <summary>
    ''' 调用插件的 Load（只调用一次，被禁用时不调用）。
    ''' </summary>
    Private Sub TryLoadPlugin(Def As PluginDefinition)
        If Def Is Nothing OrElse Def.Instance Is Nothing OrElse Def.IsLoaded Then Return
        If IsDisabled(Def.Name) Then Return
        Def.IsLoaded = True
        Try
            Def.Instance.Load(Def.Host)
        Catch ex As Exception
            Logger.Error(ex, $"插件「{Def.Name}」加载时出错", LogBehavior.Alert)
        End Try
    End Sub

    ''' <summary>
    ''' 在程序集中找出第一个可用的插件类型。
    ''' </summary>
    Private Function FindPluginType(Asm As Reflection.Assembly) As Type
        Dim Types As Type()
        Try
            Types = Asm.GetTypes()
        Catch ex As Reflection.ReflectionTypeLoadException
            Types = ex.Types.Where(Function(T) T IsNot Nothing).ToArray()
        End Try
        For Each T In Types
            If T Is Nothing OrElse T.IsAbstract OrElse T.IsInterface OrElse T.IsGenericTypeDefinition Then Continue For
            If Not GetType(PluginApi.IPlugin).IsAssignableFrom(T) Then Continue For
            If T.GetConstructor(Type.EmptyTypes) Is Nothing Then Continue For
            Return T
        Next
        Return Nothing
    End Function

    ''' <summary>
    ''' 注册程序集依赖解析，让插件自带的依赖 dll 能被找到。
    ''' </summary>
    Private Sub EnsureAssemblyResolver()
        If AssemblyResolverReady Then Return
        AssemblyResolverReady = True
        AddHandler AppDomain.CurrentDomain.AssemblyResolve, AddressOf ResolvePluginAssembly
    End Sub

    Private Function ResolvePluginAssembly(sender As Object, args As ResolveEventArgs) As Reflection.Assembly
        Try
            Dim SimpleName As String = New Reflection.AssemblyName(args.Name).Name
            For Each FilePath In DirectoryUtils.EnumerateFiles(PluginFolder, True, SimpleName & ".dll")
                Return Reflection.Assembly.Load(FileUtils.ReadAsBytes(FilePath.ToString()))
            Next
        Catch ex As Exception
            Logger.Warn(ex, "解析插件依赖失败")
        End Try
        Return Nothing
    End Function

    ''' <summary>
    ''' 刷新可接收游戏事件的插件列表。
    ''' </summary>
    Private Sub SyncActivePlugins(AllPlugins As List(Of PluginDefinition))
        SyncLock ActivePlugins
            ActivePlugins.Clear()
            For Each Def In AllPlugins
                If Def.Kind = PluginKind.DotNet AndAlso Def.IsLoaded AndAlso Not IsDisabled(Def.Name) Then ActivePlugins.Add(Def)
            Next
        End SyncLock
    End Sub

    ''' <summary>
    ''' 读取 Python 插件（目录下的 plugin.json）。
    ''' </summary>
    Private Function LoadPythonPlugin(Folder As String, SourcePath As String) As PluginDefinition
        Dim JsonPath As String = Folder & "plugin.json"
        If Not FileUtils.Exists(JsonPath) Then Throw New Exception("插件包中缺少 plugin.json")
        Dim Json As JObject = FileUtils.ReadAsString(JsonPath).DeserializeJson()
        If Json Is Nothing Then Throw New Exception("plugin.json 无法解析")
        Dim Def As New PluginDefinition With {
            .Kind = PluginKind.Python,
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
            .Kind = PluginKind.Declarative,
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
        Select Case Plugin.Kind
            Case PluginKind.DotNet
                RunDotNetPlugin(Plugin, Button)
                Return
            Case PluginKind.Python
                RunPythonPlugin(Plugin, Button)
                Return
        End Select
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
                Case "file"
                    If String.IsNullOrEmpty(Value) Then Throw New Exception("按钮未填写文件路径")
                    If FileUtils.Exists(Value) Then
                        OpenExplorer(Value)
                        Return
                    End If
                    Dim ParentFolder As String = IO.Path.GetDirectoryName(Value)
                    If Not String.IsNullOrEmpty(ParentFolder) AndAlso DirectoryUtils.Exists(ParentFolder) Then
                        OpenExplorer(ParentFolder)
                        Return
                    End If
                    Throw New Exception("文件不存在：" & Value)
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
    ''' 执行 VB.NET 插件的按钮，运行在后台线程，异常不会影响启动器。
    ''' </summary>
    Private Sub RunDotNetPlugin(Plugin As PluginDefinition, Button As PluginButton)
        If Plugin.Instance Is Nothing Then
            Hint($"插件「{Plugin.Name}」未能加载，无法执行", HintType.Red)
            Return
        End If
        If IsDisabled(Plugin.Name) Then
            Hint($"插件「{Plugin.Name}」已被禁用", HintType.Red)
            Return
        End If
        RunInThread(
        Sub()
            Try
                Plugin.Instance.Execute(Button.Id)
            Catch ex As Exception
                Logger.Error(ex, $"插件「{Plugin.Name}」执行「{Button.Text}」失败")
                RunInUi(Sub() Hint($"插件「{Plugin.Name}」出错：{ex.Message}", HintType.Red))
            End Try
        End Sub)
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
                Info.Arguments = """" & EntryPath & """ " & If(Button.Id, "")
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
    ''' 广播游戏启动事件。
    ''' </summary>
    Public Sub NotifyGameStarted(InstanceName As String)
        BroadcastGameEvent(True, InstanceName)
    End Sub

    ''' <summary>
    ''' 广播游戏退出事件。
    ''' </summary>
    Public Sub NotifyGameExited(InstanceName As String)
        BroadcastGameEvent(False, InstanceName)
    End Sub

    Private Sub BroadcastGameEvent(Started As Boolean, InstanceName As String)
        Dim Targets As List(Of PluginDefinition)
        SyncLock ActivePlugins
            If Not ActivePlugins.Any() Then Return
            Targets = ActivePlugins.ToList()
        End SyncLock
        RunInThread(
        Sub()
            For Each Def In Targets
                Try
                    Dim EventPlugin As PluginApi.IPluginGameEvent = TryCast(Def.Instance, PluginApi.IPluginGameEvent)
                    If EventPlugin Is Nothing Then Continue For
                    If Started Then
                        EventPlugin.OnGameStarted(InstanceName)
                    Else
                        EventPlugin.OnGameExited(InstanceName)
                    End If
                Catch ex As Exception
                    Logger.Warn(ex, $"插件「{Def.Name}」处理游戏事件失败")
                End Try
            Next
        End Sub)
    End Sub

    ''' <summary>
    ''' 删除插件前调用：把实例从缓存中移除，便于文件删除后下次重新加载。
    ''' </summary>
    Public Sub ForgetPlugin(Plugin As PluginDefinition)
        If Plugin Is Nothing Then Return
        SyncLock DotNetCache
            If Not String.IsNullOrEmpty(Plugin.CacheKey) Then DotNetCache.Remove(Plugin.CacheKey)
        End SyncLock
        SyncLock ActivePlugins
            ActivePlugins.RemoveAll(Function(Item) Item Is Plugin)
        End SyncLock
    End Sub

    ''' <summary>
    ''' 插件类型的中文名称，用于界面显示。
    ''' </summary>
    Public Function GetKindName(Plugin As PluginDefinition) As String
        If Plugin Is Nothing Then Return ""
        If Plugin.IsBuiltIn Then Return "内置"
        Select Case Plugin.Kind
            Case PluginKind.DotNet
                Return "VB.NET"
            Case PluginKind.Python
                Return "Python（旧版）"
            Case Else
                Return "声明式"
        End Select
    End Function

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
                For Each PathEntry In PathVar.Split(";"c)
                    If String.IsNullOrWhiteSpace(PathEntry) Then Continue For
                    For Each ExeName In Names
                        Try
                            Dim Full As String = IO.Path.Combine(PathEntry.Trim(), ExeName)
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

    ''' <summary>
    ''' 内置插件：不需要任何文件，始终可用。
    ''' </summary>
    Private Function GetBuiltInPlugin() As PluginDefinition
        Dim Def As New PluginDefinition With {
            .Kind = PluginKind.BuiltIn,
            .Name = "系统工具",
            .Version = "1.0.0",
            .Author = "PCLFE",
            .Description = "启动器内置插件，提供日志等常用入口。",
            .IsBuiltIn = True
        }
        Def.Buttons.Add(New PluginButton With {.Text = "打开启动日志", .Type = "file", .Value = Paths.Base & "PCL\Log1.txt", .ToolTip = "打开最近一次启动的日志文件；若不存在则打开日志目录"})
        Def.Buttons.Add(New PluginButton With {.Text = "打开日志文件夹", .Type = "folder", .Value = Paths.Base & "PCL\"})
        Return Def
    End Function

    ''' <summary>已禁用的插件名称列表。</summary>
    Public Function GetDisabledNames() As List(Of String)
        Try
            Dim Raw As String = Settings.Get(Of String)("PluginDisabled")
            If String.IsNullOrEmpty(Raw) Then Return New List(Of String)
            Return Raw.Split({"¨"}, StringSplitOptions.RemoveEmptyEntries).ToList()
        Catch
            Return New List(Of String)
        End Try
    End Function

    ''' <summary>插件是否被禁用。</summary>
    Public Function IsDisabled(PluginName As String) As Boolean
        Return GetDisabledNames().Contains(PluginName)
    End Function

    ''' <summary>设置插件启用状态。</summary>
    Public Sub SetDisabled(PluginName As String, Disabled As Boolean)
        Try
            Dim Names As List(Of String) = GetDisabledNames()
            Names.RemoveAll(Function(Item) Item = PluginName)
            If Disabled Then Names.Add(PluginName)
            Settings.Set("PluginDisabled", Names.Join("¨"))
        Catch ex As Exception
            Logger.Warn(ex, "保存插件启用状态失败")
        End Try
    End Sub

End Module
