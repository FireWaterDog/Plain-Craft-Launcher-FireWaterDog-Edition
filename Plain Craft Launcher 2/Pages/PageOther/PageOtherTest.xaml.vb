Public Class PageOtherTest

#Region "内存优化"

    ''' <summary>
    ''' 内存优化。强制回收内存并释放部分缓存。
    ''' </summary>
    ''' <param name="ShowHint">是否显示提示。</param>
    Public Shared Sub MemoryOptimize(ShowHint As Boolean)
        Try
            '强制回收内存
            GC.Collect()
            GC.WaitForPendingFinalizers()
            GC.Collect()
            If ShowHint Then RunInUi(Sub() Hint("内存优化完成！已释放被占用的内存。", HintType.Green))
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

#Region "通透模式（彩蛋）"

    Private Shared IsTongTouOn As Boolean = False
    Private Shared TongTouPlayer As System.Windows.Media.MediaPlayer = Nothing
    Private Shared TongTouImgBackBackground As Object = Nothing
    Private Shared TongTouImgBackVisibility As System.Windows.Visibility = System.Windows.Visibility.Collapsed
    Private Shared TongTouImgBackEffect As Object = Nothing
    Private Shared TongTouImgBackOpacity As Double = 1
    Private Shared ReadOnly TongTouMusicPath As String = "D:\PCL-FE\新宿决战（雨爱DJ版）.mp3"
    Private Shared ReadOnly TongTouImagePath As String = "D:\PCL-FE\22844304.jpg"

    ''' <summary>
    ''' 切换通透模式：把启动器背景换成指定图片，并循环播放指定音乐。
    ''' </summary>
    Public Shared Sub ToggleTongTouMode()
        RunInUi(Sub()
            If IsTongTouOn Then
                '关闭通透模式：恢复原本的背景设置
                If TongTouPlayer IsNot Nothing Then
                    Try
                        RemoveHandler TongTouPlayer.MediaEnded, AddressOf TongTouMediaEnded
                        TongTouPlayer.Stop()
                        TongTouPlayer.Close()
                    Catch
                    End Try
                    TongTouPlayer = Nothing
                End If
                Try
                    FrmMain.ImgBack.Background = TongTouImgBackBackground
                    FrmMain.ImgBack.Visibility = TongTouImgBackVisibility
                    FrmMain.ImgBack.Effect = TongTouImgBackEffect
                    FrmMain.ImgBack.Opacity = TongTouImgBackOpacity
                Catch
                End Try
                IsTongTouOn = False
                Hint("已退出通透模式。", HintType.Blue)
            Else
                '开启通透模式：循环播放音乐
                Dim MusicPath As String = TongTouMusicPath
                If Not System.IO.File.Exists(MusicPath) Then
                    MusicPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "新宿决战（雨爱DJ版）.mp3")
                End If
                If System.IO.File.Exists(MusicPath) Then
                    Try
                        TongTouPlayer = New System.Windows.Media.MediaPlayer()
                        TongTouPlayer.Open(New Uri(MusicPath))
                        AddHandler TongTouPlayer.MediaEnded, AddressOf TongTouMediaEnded
                        TongTouPlayer.Play()
                    Catch ex As Exception
                        Logger.Warn(ex, "通透模式音乐播放失败")
                    End Try
                Else
                    Logger.Warn("通透模式音乐文件未找到：" & MusicPath)
                End If
                '把背景换成图片（复用启动器背景层 ImgBack，UI 浮于其上、保持可用）
                Dim ImagePath As String = TongTouImagePath
                If Not System.IO.File.Exists(ImagePath) Then
                    ImagePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "22844304.jpg")
                End If
                If System.IO.File.Exists(ImagePath) Then
                    Try
                        TongTouImgBackBackground = FrmMain.ImgBack.Background
                        TongTouImgBackVisibility = FrmMain.ImgBack.Visibility
                        TongTouImgBackEffect = FrmMain.ImgBack.Effect
                        TongTouImgBackOpacity = FrmMain.ImgBack.Opacity
                        Dim Brush As New System.Windows.Media.ImageBrush(New System.Windows.Media.Imaging.BitmapImage(New Uri(ImagePath))) With {
                            .Stretch = System.Windows.Media.Stretch.UniformToFill
                        }
                        FrmMain.ImgBack.Background = Brush
                        FrmMain.ImgBack.Effect = Nothing
                        FrmMain.ImgBack.Opacity = 1
                        FrmMain.ImgBack.Visibility = System.Windows.Visibility.Visible
                    Catch ex As Exception
                        Logger.Warn(ex, "通透模式设置背景失败")
                    End Try
                Else
                    Hint("没找到背景图片，但音乐已开启~", HintType.Blue)
                End If
                IsTongTouOn = True
                Hint("活得通透！", HintType.Green)
            End If
        End Sub)
    End Sub

    Private Shared Sub TongTouMediaEnded(sender As Object, e As EventArgs)
        '循环播放
        If TongTouPlayer IsNot Nothing Then
            Try
                TongTouPlayer.Position = TimeSpan.Zero
                TongTouPlayer.Play()
            Catch
            End Try
        End If
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
    Private Sub BtnTongTou_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnTongTou.Click
        ToggleTongTouMode()
    End Sub
    Private Sub BtnJrrp_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnJrrp.Click
        Jrrp()
    End Sub

#End Region

End Class
