Public Class PageOtherTest

#Region "内存优化"

    '把当前进程暂不使用的物理内存（工作集）归还操作系统。
    '仅调用 GC.Collect 只会回收托管对象，CLR 不会主动把内存还给系统，任务管理器里就看不到下降。
    Private Declare Function EmptyWorkingSet Lib "psapi.dll" (hProcess As IntPtr) As Boolean
    Private Declare Function SetProcessWorkingSetSize Lib "kernel32" (hProcess As IntPtr, dwMinimumWorkingSetSize As IntPtr, dwMaximumWorkingSetSize As IntPtr) As Boolean

    ''' <summary>
    ''' 内存优化。强制回收托管内存，并把暂不使用的物理内存归还操作系统。
    ''' </summary>
    ''' <param name="ShowHint">是否显示提示。</param>
    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        Dim Before As Long = 0
        Dim After As Long = 0
        Try
            '记录优化前的物理内存（工作集）
            Try
                Using P As System.Diagnostics.Process = System.Diagnostics.Process.GetCurrentProcess()
                    Before = P.WorkingSet64
                End Using
            Catch
            End Try

            '让下一次 GC 顺便压缩大对象堆（LOH），减少内存碎片（.NET Framework 4.5.1+）
            Try
                System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce
            Catch
            End Try

            '完整的强制回收：阻塞式 + 压缩全部代，跑两轮尽量清空可回收对象
            For i As Integer = 1 To 2
                GC.Collect(2, GCCollectionMode.Forced, blocking:=True, compacting:=True)
                GC.WaitForPendingFinalizers()
            Next

            '关键一步：把工作集归还操作系统。不做这一步时，内存几乎不会减少
            Try
                Using P As System.Diagnostics.Process = System.Diagnostics.Process.GetCurrentProcess()
                    Dim Handle As IntPtr = P.Handle
                    If Not EmptyWorkingSet(Handle) Then
                        '个别系统上 psapi 调用失败时，退回到 kernel32 的等价方式
                        SetProcessWorkingSetSize(Handle, New IntPtr(-1), New IntPtr(-1))
                    End If
                    P.Refresh()
                    After = P.WorkingSet64
                End Using
            Catch
            End Try

            '组装提示与日志
            Dim Msg As String
            If Before > 0 AndAlso After > 0 Then
                Dim BeforeMb As Long = Before \ 1024 \ 1024
                Dim AfterMb As Long = After \ 1024 \ 1024
                Dim FreedMb As Long = BeforeMb - AfterMb
                If FreedMb < 0 Then FreedMb = 0
                Logger.Info($"内存优化完成：工作集 {BeforeMb} MB → {AfterMb} MB，释放约 {FreedMb} MB")
                Msg = $"内存优化完成！已回收托管内存并把暂不使用的物理内存归还系统，释放约 {FreedMb} MB。"
            Else
                Logger.Info("内存优化完成（未能读取工作集数据）")
                Msg = "内存优化完成！已回收托管内存并把暂不使用的物理内存归还系统。"
            End If
            If ShowHint Then RunInUi(Sub() Hint(Msg, HintType.Green))
        Catch ex As Exception
            Logger.Error(ex, "内存优化失败")
            If ShowHint Then RunInUi(Sub() Hint("内存优化失败：" & ex.Message, HintType.Red))
        End Try
    End Sub
    ''' <summary>
    ''' 内存优化（供 --memory 独立进程调用，逻辑与 MemoryOptimize 一致）。
    ''' </summary>
    Public Shared Sub MemoryOptimizeInternal(ShowHint As Boolean)
        MemoryOptimize(ShowHint)
    End Sub

#End Region

#Region "垃圾清理"

    ''' <summary>
    ''' 清理启动器的临时文件与缓存（不会删除游戏与设置）。
    ''' </summary>
    Public Shared Sub RubbishClear()
        Dim Confirm As Boolean = False
        RunInUiWait(Sub() Confirm = (MyMsgBox("是否清理 PCLFE 的临时文件与缓存？" & vbCrLf & "这只会删除启动器自身的临时数据，不会删除任何游戏存档或设置。",
                            "垃圾清理", "清理", "取消") <> 2))
        If Not Confirm Then Return
        RunInThread(Sub()
            Try
                Dim Count As Integer = 0
                If Directory.Exists(PathTemp) Then
                    '删除临时文件
                    For Each File In DirectoryUtils.EnumerateFiles(PathTemp, True)
                        Try
                            FileUtils.Delete(File)
                            Count += 1
                        Catch
                            '跳过正在使用的文件
                        End Try
                    Next
                    '删除空的临时子目录
                    For Each Folder In Directory.GetDirectories(PathTemp)
                        Try
                            DirectoryUtils.Delete(Folder)
                        Catch
                        End Try
                    Next
                End If
                RunInUi(Sub() Hint($"垃圾清理完成！共清理 {Count} 个文件。", HintType.Green))
            Catch ex As Exception
                Logger.Error(ex, "垃圾清理失败")
                RunInUi(Sub() Hint("垃圾清理失败：" & ex.Message, HintType.Red))
            End Try
        End Sub)
    End Sub

#End Region

#Region "自定义下载"

    ''' <summary>
    ''' 自定义下载。由百宝箱按钮调用，会先弹出输入框询问网址与文件名。
    ''' </summary>
    Public Shared Sub StartCustomDownloadInteractive()
        Dim Url As String = RunInUiWait(Function() MyMsgBoxInput("自定义下载", "请输入要下载的文件网址（需以 http:// 或 https:// 开头）：", HintText:="下载网址"))
        If String.IsNullOrEmpty(Url) Then Return
        Dim Name As String = RunInUiWait(Function() MyMsgBoxInput("自定义下载", "请输入保存的文件名（留空则自动从网址获取）：",
                                           DefaultInput:=PathUtils.GetLastPart(Url), HintText:="文件名"))
        If String.IsNullOrEmpty(Name) Then Name = PathUtils.GetLastPart(Url)
        StartCustomDownload(Url, Name)
    End Sub

    ''' <summary>
    ''' 下载指定网址的文件到本地。
    ''' </summary>
    ''' <param name="Url">文件网址。</param>
    ''' <param name="FileName">保存的文件名。</param>
    ''' <param name="Folder">保存的文件夹，留空则保存到临时下载目录。</param>
    Public Shared Sub StartCustomDownload(Url As String, FileName As String, Optional Folder As String = Nothing)
        RunInThread(Sub()
            Try
                If String.IsNullOrEmpty(Url) Then Return
                If Not (Url.StartsWithF("http://", True) OrElse Url.StartsWithF("https://", True)) Then
                    RunInUi(Sub() Hint("下载网址必须以 http:// 或 https:// 开头！", HintType.Red))
                    Return
                End If
                If String.IsNullOrEmpty(FileName) Then FileName = PathUtils.GetLastPart(Url)
                If String.IsNullOrEmpty(Folder) Then Folder = PathTemp & "Download\"
                DirectoryUtils.Create(Folder)
                Dim LocalPath As String = Folder & FileName
                RunInUi(Sub() Hint("开始下载：" & FileName & "……", HintType.Blue))
                NetDownloadByClient(Url, LocalPath)
                RunInUi(Sub()
                            Hint("下载完成：" & FileName, HintType.Green)
                            OpenExplorer(LocalPath)
                        End Sub)
            Catch ex As Exception
                Logger.Error(ex, "自定义下载失败")
                RunInUi(Sub() Hint("下载失败：" & ex.Message, HintType.Red))
            End Try
        End Sub)
    End Sub

#End Region

#Region "今日人品"

    ''' <summary>
    ''' 今日人品：随机给出一个有趣的人品值。
    ''' </summary>
    Public Shared Sub Jrrp()
        RunInUi(Sub()
            Dim Luck = RandomInteger(1, 100)
            Dim Msg As String
            Select Case Luck
                Case Is >= 80 : Msg = "今天你的人品爆棚！欧气满满，抽卡必出金~"
                Case Is >= 50 : Msg = "今天人品不错，平稳顺利的一天。"
                Case Is >= 20 : Msg = "今天人品一般，小心行事别踩坑。"
                Case Else : Msg = "今天人品有点掉线，注意休息别硬刚。"
            End Select
            MyMsgBox($"你今天的人品值：{Luck} / 100{vbCrLf}{Msg}", "今日人品")
        End Sub)
    End Sub

#End Region

#Region "随机提示（保留兼容）"

    Public Shared Function GetRandomCave() As String
        Return GetRandomHint()
    End Function
    Public Shared Function GetRandomHint() As String
        Dim Hints As String() = {
            "遇到 Bug 别慌，先看看是不是 Java 的问题。",
            "内存不够？点一下「内存优化」试试。",
            "启动器卡了？清一清「垃圾清理」也许有用。",
            "联机前记得把防火墙和杀软都放一放。",
            "想听歌？把音乐放进 PCL\Musics 文件夹即可。"
        }
        Return Hints(RandomInteger(0, Hints.Length - 1))
    End Function
    Public Shared Function GetRandomPresetHint() As String
        Return GetRandomHint()
    End Function

#End Region

#Region "按钮事件"

    Private Sub BtnMemory_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnMemory.Click
        MemoryOptimize(True)
    End Sub
    Private Sub BtnRubbish_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRubbish.Click
        RubbishClear()
    End Sub
    Private Sub BtnDownload_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnDownload.Click
        StartCustomDownloadInteractive()
    End Sub
    Private Sub BtnJrrp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnJrrp.Click
        Jrrp()
    End Sub

#End Region

#Region "插件"

    Private Sub PageOtherTest_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        BuildPluginCards()
    End Sub

    ''' <summary>
    ''' 根据 PCL\Plugins\ 下的插件定义生成卡片与按钮。
    ''' </summary>
    Private Sub BuildPluginCards()
        Try
            PanPlugins.Children.Clear()
            For Each Plugin In ModPlugin.LoadAll()
                Dim Card As New MyCard With {.Title = Plugin.Name, .Margin = New Thickness(0, 0, 0, 15)}
                Dim Stack As New StackPanel With {.Margin = New Thickness(21, 40, 21, 16)}
                Card.Children.Add(Stack)
                '说明文字
                If Not String.IsNullOrWhiteSpace(Plugin.Description) Then
                    Dim Desc As New TextBlock
                    Desc.Text = Plugin.Description
                    Desc.TextWrapping = TextWrapping.Wrap
                    Desc.Opacity = 0.7
                    Desc.Margin = New Thickness(0, 0, 0, 10)
                    Stack.Children.Add(Desc)
                End If
                '插件按钮
                For Each ButtonDef In Plugin.Buttons
                    Dim NewButton As New MyButton With {.Text = ButtonDef.Text, .Height = 35, .Margin = New Thickness(0, 0, 0, 8), .HorizontalAlignment = HorizontalAlignment.Left}
                    If Not String.IsNullOrWhiteSpace(ButtonDef.ToolTip) Then NewButton.ToolTip = ButtonDef.ToolTip
                    Dim CapturedPlugin = Plugin
                    Dim CapturedButton = ButtonDef
                    AddHandler NewButton.Click, Sub(s, args) ModPlugin.ExecuteButton(CapturedPlugin, CapturedButton)
                    Stack.Children.Add(NewButton)
                Next
                PanPlugins.Children.Add(Card)
            Next
            '插件管理（即使没有插件也显示，方便打开目录放入插件）
            Dim ManageCard As New MyCard With {.Title = "插件管理", .Margin = New Thickness(0, 0, 0, 15)}
            Dim ManageInner As New StackPanel With {.Margin = New Thickness(21, 40, 21, 16)}
            ManageCard.Children.Add(ManageInner)
            Dim ManageRow As New StackPanel With {.Orientation = Orientation.Horizontal}
            Dim BtnPluginReload As New MyButton With {.Text = "重新加载", .Height = 35, .Margin = New Thickness(0, 0, 10, 0)}
            AddHandler BtnPluginReload.Click, Sub(s, e) BuildPluginCards()
            Dim BtnPluginFolder As New MyButton With {.Text = "打开插件目录", .Height = 35}
            AddHandler BtnPluginFolder.Click, Sub(s, e) OpenPluginFolder()
            ManageRow.Children.Add(BtnPluginReload)
            ManageRow.Children.Add(BtnPluginFolder)
            ManageInner.Children.Add(ManageRow)
            Dim ManageHint As New TextBlock
            ManageHint.Text = "插件放在 PCL\Plugins\ 下：.pclfePlugin 为 Python 插件包（含 plugin.json 与 main.py），.json 为旧版声明式插件。"
            ManageHint.TextWrapping = TextWrapping.Wrap
            ManageHint.Opacity = 0.7
            ManageHint.Margin = New Thickness(0, 12, 0, 0)
            ManageInner.Children.Add(ManageHint)
            PanPlugins.Children.Add(ManageCard)
        Catch ex As Exception
            Logger.Warn(ex, "构建插件卡片失败")
        End Try
    End Sub

    ''' <summary>
    ''' 打开插件目录（不存在则创建）。
    ''' </summary>
    Private Sub OpenPluginFolder()
        Try
            DirectoryUtils.Create(ModPlugin.PluginFolder)
            OpenExplorer(ModPlugin.PluginFolder)
        Catch ex As Exception
            Logger.Warn(ex, "打开插件目录失败")
        End Try
    End Sub

#End Region

End Class
