Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text

''' <summary>
''' grand —— PCLFE 插件构建工具。
''' 用法：
'''   grand new &lt;名称&gt; [--dir 输出目录]                       创建插件工程
'''   grand build [工程目录] [--pcl 启动器exe路径]              编译为 bin\plugin.dll
'''   grand pack [工程目录] [--pcl 启动器exe路径]               打包为 xxx.pclfePlugin
'''   grand install [工程目录] [--pcl 启动器exe路径]            编译 + 打包 + 复制到 PCL\Plugins\
''' </summary>
Module Program

    Private Const ToolName As String = "grand"
    Private Const ToolVersion As String = "1.0.0"

    Function Main(Args As String()) As Integer
        Try
            If Args Is Nothing OrElse Args.Length = 0 Then
                ShowHelp()
                Return 0
            End If
            Dim Command As String = Args(0).Trim().ToLower()
            Dim Options As New CommandOptions(Args)
            Select Case Command
                Case "new", "init"
                    Return CommandNew(Options)
                Case "build"
                    Return CommandBuild(Options, False)
                Case "pack"
                    Return CommandPack(Options)
                Case "install"
                    Return CommandBuild(Options, True)
                Case "help", "-h", "--help", "/?"
                    ShowHelp()
                    Return 0
                Case Else
                    Console.WriteLine("未知命令：" & Command)
                    Console.WriteLine()
                    ShowHelp()
                    Return 1
            End Select
        Catch ex As Exception
            Console.WriteLine()
            Console.WriteLine("[错误] " & ex.Message)
            Return 1
        End Try
    End Function

#Region "命令"

    Private Function CommandNew(Options As CommandOptions) As Integer
        Dim Name As String = Options.First
        If String.IsNullOrWhiteSpace(Name) Then
            Console.WriteLine("用法：grand new <名称> [--dir 输出目录]")
            Return 1
        End If
        Dim Root As String = Options.GetValue("dir", Environment.CurrentDirectory)
        Dim Folder As String = IO.Path.Combine(Root, Name)
        If IO.Directory.Exists(Folder) AndAlso IO.Directory.GetFiles(Folder).Length > 0 Then
            Console.WriteLine("[错误] 目录已存在且不为空：" & Folder)
            Return 1
        End If
        IO.Directory.CreateDirectory(Folder)
        Dim SafeName As String = Name
        IO.File.WriteAllText(IO.Path.Combine(Folder, "plugin.json"), Template.Manifest(SafeName), Text.Encoding.UTF8)
        IO.File.WriteAllText(IO.Path.Combine(Folder, "Plugin.vb"), Template.PluginSource(), New Text.UTF8Encoding(False))
        IO.File.WriteAllText(IO.Path.Combine(Folder, "README.md"), Template.ProjectReadme(SafeName), Text.Encoding.UTF8)
        Console.WriteLine("已创建插件工程：" & Folder)
        Console.WriteLine()
        Console.WriteLine("接下来：")
        Console.WriteLine("  1. 编辑 Plugin.vb 与 plugin.json")
        Console.WriteLine("  2. grand build   """ & Folder & """")
        Console.WriteLine("  3. grand install """ & Folder & """   （直接安装到启动器）")
        Return 0
    End Function

    Private Function CommandBuild(Options As CommandOptions, Install As Boolean) As Integer
        Dim Project As GrandProject = GrandProject.Load(Options.ProjectFolder)
        Dim PclExe As String = GrandProject.FindPclExe(Project.Folder, Options.GetValue("pcl", ""))
        Console.WriteLine("工程目录：" & Project.Folder)
        Console.WriteLine("启动器  ：" & If(PclExe, "（未找到，请用 --pcl 指定）"))
        If Not Project.Build(PclExe) Then Return 1
        Console.WriteLine("[完成] 已生成 " & Project.OutputDll)
        If Not Install Then Return 0
        Dim Package As String = Project.Pack()
        Console.WriteLine("[完成] 已打包 " & Package)
        Dim TargetFolder As String = IO.Path.Combine(IO.Path.GetDirectoryName(PclExe), "PCL\Plugins")
        IO.Directory.CreateDirectory(TargetFolder)
        Dim TargetFile As String = IO.Path.Combine(TargetFolder, IO.Path.GetFileName(Package))
        IO.File.Copy(Package, TargetFile, True)
        Console.WriteLine("[完成] 已安装到 " & TargetFile)
        Console.WriteLine("       重启启动器，或到 设置 → 插件 点「重新加载」即可生效。")
        Return 0
    End Function

    Private Function CommandPack(Options As CommandOptions) As Integer
        Dim Project As GrandProject = GrandProject.Load(Options.ProjectFolder)
        Dim Package As String = Project.Pack()
        Console.WriteLine("[完成] 已打包 " & Package)
        Return 0
    End Function

#End Region

    Private Sub ShowHelp()
        Console.WriteLine(ToolName & " " & ToolVersion & " —— PCLFE 插件构建工具（插件与启动器同为 VB.NET）")
        Console.WriteLine()
        Console.WriteLine("用法：")
        Console.WriteLine("  grand new <名称> [--dir 输出目录]")
        Console.WriteLine("      创建插件工程（plugin.json + Plugin.vb + README.md）")
        Console.WriteLine("  grand build [工程目录] [--pcl 启动器exe路径]")
        Console.WriteLine("      用 vbc 编译插件，输出 bin\\plugin.dll")
        Console.WriteLine("  grand pack [工程目录] [--pcl 启动器exe路径]")
        Console.WriteLine("      打包为 <名称>.pclfePlugin（内含 plugin.json 与 plugin.dll）")
        Console.WriteLine("  grand install [工程目录] [--pcl 启动器exe路径]")
        Console.WriteLine("      编译 + 打包 + 复制到启动器的 PCL\\Plugins\\")
        Console.WriteLine()
        Console.WriteLine("说明：")
        Console.WriteLine("  · 未给工程目录时使用当前目录；未给 --pcl 时自动在同级目录中查找启动器。")
        Console.WriteLine("  · 插件工程只引用 启动器 exe，不需要额外的 SDK 或 NuGet 包。")
    End Sub

End Module

''' <summary>
''' 极简命令行参数解析：位置参数 + --key value。
''' </summary>
Friend Class CommandOptions

    Private ReadOnly Values As New Dictionary(Of String, String)
    Private ReadOnly Positionals As New List(Of String)

    Public Sub New(Args As String())
        Dim i As Integer = 1 '跳过命令本身
        While i < Args.Length
            Dim Item As String = Args(i)
            If Item.StartsWith("--") Then
                Dim Key As String = Item.Substring(2).ToLower()
                If i + 1 < Args.Length AndAlso Not Args(i + 1).StartsWith("--") Then
                    Values(Key) = Args(i + 1)
                    i += 2
                Else
                    Values(Key) = ""
                    i += 1
                End If
            Else
                Positionals.Add(Item)
                i += 1
            End If
        End While
    End Sub

    Public ReadOnly Property First As String
        Get
            Return If(Positionals.Count > 0, Positionals(0), "")
        End Get
    End Property

    Public ReadOnly Property ProjectFolder As String
        Get
            Dim Folder As String = First
            If String.IsNullOrWhiteSpace(Folder) Then Folder = Environment.CurrentDirectory
            Return IO.Path.GetFullPath(Folder)
        End Get
    End Property

    Public Function GetValue(Key As String, DefaultValue As String) As String
        Dim Value As String = Nothing
        If Values.TryGetValue(Key.ToLower(), Value) AndAlso Not String.IsNullOrWhiteSpace(Value) Then Return Value
        Return DefaultValue
    End Function

End Class
