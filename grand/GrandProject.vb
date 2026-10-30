Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text
Imports System.Diagnostics

''' <summary>
''' 插件工程：读取清单、定位编译器与启动器、编译、打包。
''' </summary>
Friend Class GrandProject

    ''' <summary>工程目录（绝对路径，末尾不带分隔符）。</summary>
    Public Property Folder As String = ""
    ''' <summary>plugin.json 路径。</summary>
    Public Property ManifestPath As String = ""
    ''' <summary>插件名称（取自清单）。</summary>
    Public Property Name As String = ""
    ''' <summary>插件版本（取自清单）。</summary>
    Public Property Version As String = "1.0.0"
    ''' <summary>编译产物路径。</summary>
    Public ReadOnly Property OutputDll As String
        Get
            Return IO.Path.Combine(Folder, "bin", "plugin.dll")
        End Get
    End Property

    Public Shared Function Load(ProjectFolder As String) As GrandProject
        If Not IO.Directory.Exists(ProjectFolder) Then Throw New Exception("工程目录不存在：" & ProjectFolder)
        Dim Manifest As String = IO.Path.Combine(ProjectFolder, "plugin.json")
        If Not IO.File.Exists(Manifest) Then
            Throw New Exception("工程目录中没有 plugin.json：" & ProjectFolder & vbCrLf & "可用 grand new <名称> 创建新工程。")
        End If
        Dim Result As New GrandProject With {
            .Folder = ProjectFolder.TrimEnd("\"c),
            .ManifestPath = Manifest
        }
        Dim ManifestJson As String = IO.File.ReadAllText(Manifest, Text.Encoding.UTF8)
        Result.Name = ReadJsonString(ManifestJson, "name")
        Dim Ver As String = ReadJsonString(ManifestJson, "version")
        If Not String.IsNullOrWhiteSpace(Ver) Then Result.Version = Ver
        If String.IsNullOrWhiteSpace(Result.Name) Then
            Result.Name = IO.Path.GetFileName(ProjectFolder)
        End If
        Return Result
    End Function

    Private Shared Function ReadJsonString(Json As String, Key As String) As String
        Try
            Dim M As Text.RegularExpressions.Match =
                Text.RegularExpressions.Regex.Match(Json, """" & Key & """\s*:\s*""([^""]*)""")
            If M.Success Then
                '反转义，保证 Windows 路径可用
                Dim Value As String = M.Groups(1).Value
                Value = Value.Replace("\\", "\")
                Value = Value.Replace("\/", "/")
                Return Value
            End If
        Catch
        End Try
        Return ""
    End Function

    ''' <summary>
    ''' 查找启动器 exe：命令行 → 环境变量 → grand.json → 向上查找常见位置。
    ''' </summary>
    Public Shared Function FindPclExe(ProjectFolder As String, Hint As String) As String
        '1. 命令行指定
        If Not String.IsNullOrWhiteSpace(Hint) AndAlso IO.File.Exists(Hint) Then Return IO.Path.GetFullPath(Hint)
        '2. 环境变量
        Dim FromEnv As String = Environment.GetEnvironmentVariable("PCL_EXE")
        If Not String.IsNullOrWhiteSpace(FromEnv) AndAlso IO.File.Exists(FromEnv) Then Return IO.Path.GetFullPath(FromEnv)
        '3. 向上查找 grand.json 中的 pcl 字段
        Dim Config As String = FindUpwards(ProjectFolder, "grand.json")
        If Not String.IsNullOrWhiteSpace(Config) Then
            Dim PathValue As String = ReadJsonString(IO.File.ReadAllText(Config, Text.Encoding.UTF8), "pcl")
            If Not String.IsNullOrWhiteSpace(PathValue) Then
                Dim Full As String = IO.Path.GetFullPath(IO.Path.Combine(IO.Path.GetDirectoryName(Config), PathValue.Replace("/", "\")))
                If IO.File.Exists(Full) Then Return Full
            End If
        End If
        '4. 从工程目录与工具目录分别向上查找
        Dim Candidates As String() = {
            "Plain Craft Launcher 2\bin\Debug\Plain Craft Launcher 2.exe",
            "Plain Craft Launcher 2\bin\Release\Plain Craft Launcher 2.exe",
            "bin\Debug\Plain Craft Launcher 2.exe",
            "bin\Release\Plain Craft Launcher 2.exe",
            "Plain Craft Launcher 2.exe"
        }
        For Each StartDir In {ProjectFolder, IO.Path.GetDirectoryName(Reflection.Assembly.GetExecutingAssembly().Location)}
            Dim Found As String = SearchUpwards(StartDir, Candidates, 8)
            If Not String.IsNullOrWhiteSpace(Found) Then Return Found
        Next
        Return ""
    End Function

    Private Shared Function FindUpwards(StartDir As String, FileName As String) As String
        Try
            Dim Dir As New IO.DirectoryInfo(StartDir)
            Dim Level As Integer = 0
            While Dir IsNot Nothing AndAlso Level < 8
                Dim File As String = IO.Path.Combine(Dir.FullName, FileName)
                If IO.File.Exists(File) Then Return File
                Dir = Dir.Parent
                Level += 1
            End While
        Catch
        End Try
        Return ""
    End Function

    Private Shared Function SearchUpwards(StartDir As String, RelativePaths As String(), MaxLevels As Integer) As String
        Try
            Dim Dir As New IO.DirectoryInfo(StartDir)
            Dim Level As Integer = 0
            While Dir IsNot Nothing AndAlso Level < MaxLevels
                For Each RelativePath In RelativePaths
                    Dim Full As String = IO.Path.Combine(Dir.FullName, RelativePath)
                    If IO.File.Exists(Full) Then Return Full
                Next
                '顺带在 Plain Craft Launcher 2\bin 下递归找一次，兼容 bin\x64\Debug 这类输出目录
                Dim BinDir As String = IO.Path.Combine(Dir.FullName, "Plain Craft Launcher 2\bin")
                If IO.Directory.Exists(BinDir) Then
                    Dim Hits As String() = IO.Directory.GetFiles(BinDir, "Plain Craft Launcher 2.exe", IO.SearchOption.AllDirectories)
                    If Hits.Length > 0 Then Return Hits(0)
                End If
                Dir = Dir.Parent
                Level += 1
            End While
        Catch
        End Try
        Return ""
    End Function

    ''' <summary>
    ''' 查找 vbc.exe（.NET Framework 自带）。
    ''' </summary>
    Public Shared Function FindCompiler() As String
        Dim Windows As String = Environment.GetFolderPath(Environment.SpecialFolder.Windows)
        Dim Dirs As String() = {
            "Microsoft.NET\Framework64\v4.0.30319",
            "Microsoft.NET\Framework\v4.0.30319"
        }
        For Each SubDir In Dirs
            Dim Vbc As String = IO.Path.Combine(Windows, SubDir, "vbc.exe")
            If IO.File.Exists(Vbc) Then Return Vbc
        Next
        Return ""
    End Function

    ''' <summary>是否已经编译出 plugin.dll。</summary>
    Public ReadOnly Property IsBuilt As Boolean
        Get
            Return IO.File.Exists(OutputDll)
        End Get
    End Property

    ''' <summary>是否有源文件比产物更新（含尚未编译的情况）。</summary>
    Public Function SourcesNewerThanOutput() As Boolean
        If Not IsBuilt Then Return True
        Try
            Dim DllTime As Date = IO.File.GetLastWriteTime(OutputDll)
            For Each File In IO.Directory.GetFiles(Folder, "*.vb", IO.SearchOption.AllDirectories)
                If File.IndexOf("\bin\", StringComparison.OrdinalIgnoreCase) >= 0 Then Continue For
                If File.IndexOf("\obj\", StringComparison.OrdinalIgnoreCase) >= 0 Then Continue For
                If IO.File.GetLastWriteTime(File) > DllTime Then Return True
            Next
        Catch
        End Try
        Return False
    End Function

    ''' <summary>
    ''' 编译插件工程，成功返回 True。
    ''' </summary>
    Public Function Build(PclExe As String) As Boolean
        Dim Vbc As String = FindCompiler()
        If String.IsNullOrWhiteSpace(Vbc) Then
            Throw New Exception("未找到 vbc.exe，请先安装 .NET Framework 4.x（Windows 一般自带）。")
        End If
        If String.IsNullOrWhiteSpace(PclExe) OrElse Not IO.File.Exists(PclExe) Then
            Throw New Exception("未找到启动器 exe（工程：" & Folder & "）。" & vbCrLf &
                                "请用 --pcl ""路径\Plain Craft Launcher 2.exe"" 指定，或在工程目录的 grand.json 里写：" & vbCrLf &
                                "{ ""pcl"": ""..\\Plain Craft Launcher 2\\bin\\Debug\\Plain Craft Launcher 2.exe"" }" & vbCrLf &
                                "如果启动器还没有编译产物，请先在 Rider / Visual Studio 里生成一次。")
        End If
        '源文件
        Dim Sources As New List(Of String)
        For Each File In IO.Directory.GetFiles(Folder, "*.vb", IO.SearchOption.AllDirectories)
            Dim Relative As String = File.Substring(Folder.Length).TrimStart("\"c)
            If Relative.StartsWith("bin\", StringComparison.OrdinalIgnoreCase) Then Continue For
            If Relative.StartsWith("obj\", StringComparison.OrdinalIgnoreCase) Then Continue For
            Sources.Add(File)
        Next
        If Sources.Count = 0 Then Throw New Exception("工程中没有 .vb 源文件：" & Folder)
        '输出目录
        Dim BinFolder As String = IO.Path.Combine(Folder, "bin")
        IO.Directory.CreateDirectory(BinFolder)
        '参数
        Dim Framework As String = IO.Path.GetDirectoryName(Vbc)
        Dim Arguments As New List(Of String) From {
            "/nologo",
            "/target:library",
            "/optionstrict-",
            "/optioninfer+",
            "/optionexplicit+",
            "/codepage:65001",
            "/r:" & Quote(IO.Path.Combine(Framework, "System.dll")),
            "/r:" & Quote(IO.Path.Combine(Framework, "System.Core.dll")),
            "/r:" & Quote(PclExe)
        }
        '常用框架程序集：存在就引用，方便插件自己画界面（WinForms / WPF）
        For Each DllName In New String() {"System.Windows.Forms.dll", "System.Drawing.dll", "System.Xaml.dll"}
            Dim Full As String = IO.Path.Combine(Framework, DllName)
            If IO.File.Exists(Full) Then Arguments.Add("/r:" & Quote(Full))
        Next
        Dim WpfFolder As String = IO.Path.Combine(Framework, "WPF")
        For Each DllName In New String() {"PresentationCore.dll", "PresentationFramework.dll", "WindowsBase.dll"}
            Dim Full As String = IO.Path.Combine(WpfFolder, DllName)
            If IO.File.Exists(Full) Then Arguments.Add("/r:" & Quote(Full))
        Next
        '自动引用启动器目录下的其它程序集，便于插件使用 Newtonsoft.Json 等已有依赖
        For Each Dll In IO.Directory.GetFiles(IO.Path.GetDirectoryName(PclExe), "*.dll")
            If Not IsManagedAssembly(Dll) Then Continue For
            Arguments.Add("/r:" & Quote(Dll))
        Next
        Arguments.Add("/out:" & Quote(OutputDll))
        For Each Source In Sources
            Arguments.Add(Quote(Source))
        Next
        Dim Info As New ProcessStartInfo(Vbc) With {
            .UseShellExecute = False,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .CreateNoWindow = True,
            .WorkingDirectory = Folder,
            .Arguments = String.Join(" ", Arguments)
        }
        Using Proc As Process = Process.Start(Info)
            Dim StandardOut As String = Proc.StandardOutput.ReadToEnd()
            Dim StandardErr As String = Proc.StandardError.ReadToEnd()
            Proc.WaitForExit()
            If Not String.IsNullOrWhiteSpace(StandardOut) Then Console.WriteLine(StandardOut.Trim())
            If Not String.IsNullOrWhiteSpace(StandardErr) Then Console.WriteLine(StandardErr.Trim())
            If Proc.ExitCode <> 0 Then
                Console.WriteLine("[失败] 编译未通过（退出码 " & Proc.ExitCode & "）")
                Return False
            End If
        End Using
        If Not IO.File.Exists(OutputDll) Then
            Console.WriteLine("[失败] 编译未生成 " & OutputDll)
            Return False
        End If
        Return True
    End Function

    ''' <summary>
    ''' 打包为 .pclfePlugin（zip：plugin.json + plugin.dll）。
    ''' </summary>
    Public Function Pack() As String
        If Not IO.File.Exists(OutputDll) Then Throw New Exception("尚未编译，请先运行 grand build。")
        Dim PackageName As String = MakeSafeFileName(Name) & ".pclfePlugin"
        Dim Target As String = IO.Path.Combine(Folder, PackageName)
        If IO.File.Exists(Target) Then IO.File.Delete(Target)
        Using Archive As New IO.Compression.ZipArchive(IO.File.Open(Target, IO.FileMode.CreateNew), IO.Compression.ZipArchiveMode.Create)
            AddEntry(Archive, ManifestPath, "plugin.json")
            AddEntry(Archive, OutputDll, "plugin.dll")
        End Using
        Return Target
    End Function

    Private Shared Sub AddEntry(Archive As IO.Compression.ZipArchive, SourceFile As String, EntryName As String)
        Dim Entry As IO.Compression.ZipArchiveEntry = Archive.CreateEntry(EntryName, IO.Compression.CompressionLevel.Optimal)
        Using Input As IO.Stream = IO.File.OpenRead(SourceFile)
            Using Output As IO.Stream = Entry.Open()
                Input.CopyTo(Output)
            End Using
        End Using
    End Sub

    Private Shared Function MakeSafeFileName(Raw As String) As String
        Dim Invalid As Char() = IO.Path.GetInvalidFileNameChars()
        Dim Builder As New Text.StringBuilder
        For Each C In Raw
            If Array.IndexOf(Invalid, C) >= 0 Then
                Builder.Append("_"c)
            Else
                Builder.Append(C)
            End If
        Next
        Dim Result As String = Builder.ToString().Trim()
        If String.IsNullOrWhiteSpace(Result) Then Return "plugin"
        Return Result
    End Function

    Private Shared Function Quote(Text As String) As String
        Return """" & Text & """"
    End Function

    ''' <summary>
    ''' 判断是否为托管程序集（避免把原生 dll 传给编译器）。
    ''' </summary>
    Private Shared Function IsManagedAssembly(Path As String) As Boolean
        Try
            Reflection.AssemblyName.GetAssemblyName(Path)
            Return True
        Catch
            Return False
        End Try
    End Function

End Class
