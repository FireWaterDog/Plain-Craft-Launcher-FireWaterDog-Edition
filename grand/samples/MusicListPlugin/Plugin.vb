Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports PCL.PluginApi

''' <summary>
''' 示例插件：扫描 PCL\music 下的 MP3，弹出音乐列表窗口并播放。
''' 构建：grand install grand\samples\MusicListPlugin --pcl "路径\Plain Craft Launcher 2.exe"
''' </summary>
Public Class MusicListPlugin
    Inherits PluginBase

    ''' <summary>扫描到的音乐文件绝对路径。</summary>
    Private ReadOnly MusicFiles As New List(Of String)()
    Private Player As MediaPlayer
    Private MusicWindow As Window
    Private MusicList As ListBox
    Private StatusText As TextBlock
    Private CurrentIndex As Integer = -1
    Private IsPaused As Boolean = False
    Private CachedFolder As String = ""

    Public Overrides ReadOnly Property Name As String
        Get
            Return "音乐列表"
        End Get
    End Property

    Public Overrides ReadOnly Property Version As String
        Get
            Return "1.0.0"
        End Get
    End Property

    Public Overrides ReadOnly Property Author As String
        Get
            Return "PCLFE"
        End Get
    End Property

    Public Overrides ReadOnly Property Description As String
        Get
            Return "扫描 PCL\music 中的 MP3，弹出列表窗口播放。"
        End Get
    End Property

    Public Overrides Function GetActions() As IEnumerable(Of PluginAction)
        Return New PluginAction() {
            New PluginAction("open", "打开音乐列表", "加载 PCL\music 中的 MP3"),
            New PluginAction("folder", "打开音乐目录")
        }
    End Function

    Public Overrides Sub Execute(ActionId As String)
        Select Case ActionId
            Case "open"
                OpenMusicWindow()
            Case "folder"
                Host.OpenFolder(GetMusicFolder())
        End Select
    End Sub

    '==================== 目录 ====================

    ''' <summary>
    ''' 音乐目录：启动器目录下的 PCL\music（不存在会自动创建）。
    ''' Host.PluginFolder 形如 &lt;启动器目录&gt;\PCL\Plugins\&lt;插件名&gt;\，向上两级就是 &lt;启动器目录&gt;\PCL\。
    ''' </summary>
    Private Function GetMusicFolder() As String
        If Not String.IsNullOrEmpty(CachedFolder) Then Return CachedFolder
        Dim pclFolder As String = Path.GetFullPath(Path.Combine(Host.PluginFolder, "..\.."))
        Dim folder As String = Path.Combine(pclFolder, "music")
        Try
            If Not Directory.Exists(folder) Then Directory.CreateDirectory(folder)
        Catch ex As Exception
            Host.Log("创建音乐目录失败：" & ex.Message, PluginLogLevel.Warning)
        End Try
        CachedFolder = folder
        Return folder
    End Function

    ''' <summary>扫描 MP3；PCL\music 为空时退回 PCL 自带的 PCL\Musics。</summary>
    Private Sub Rescan()
        MusicFiles.Clear()
        Dim folder As String = GetMusicFolder()
        Try
            For Each FilePath As String In Directory.GetFiles(folder, "*.mp3", SearchOption.AllDirectories)
                MusicFiles.Add(FilePath)
            Next
        Catch ex As Exception
            Host.Log("扫描音乐失败：" & ex.Message, PluginLogLevel.Warning)
        End Try
        If MusicFiles.Count = 0 Then
            Dim builtIn As String = Path.Combine(Path.GetFullPath(Path.Combine(folder, "..")), "Musics")
            Try
                If Directory.Exists(builtIn) Then
                    For Each FilePath As String In Directory.GetFiles(builtIn, "*.mp3", SearchOption.AllDirectories)
                        MusicFiles.Add(FilePath)
                    Next
                End If
            Catch ex As Exception
                Host.Log("扫描 PCL\Musics 失败：" & ex.Message, PluginLogLevel.Warning)
            End Try
        End If
        MusicFiles.Sort()
        If MusicList IsNot Nothing Then
            MusicList.Items.Clear()
            For Each FilePath As String In MusicFiles
                MusicList.Items.Add(Path.GetFileName(FilePath))
            Next
            If MusicFiles.Count > 0 Then MusicList.SelectedIndex = 0
        End If
        SetStatus("共 " & MusicFiles.Count & " 首（" & folder & "）")
        Host.Log("已扫描到 " & MusicFiles.Count & " 首音乐")
    End Sub

    '==================== 窗口 ====================

    ''' <summary>切到启动器的 UI 线程再建窗口。</summary>
    Private Sub OpenMusicWindow()
        Dim App As Application = Application.Current
        If App Is Nothing Then
            Host.Alert("没有可用的界面线程，无法打开音乐列表。")
            Return
        End If
        App.Dispatcher.BeginInvoke(New Action(AddressOf ShowWindowCore))
    End Sub

    Private Sub ShowWindowCore()
        If MusicWindow IsNot Nothing Then
            MusicWindow.Activate()
            Return
        End If
        If Player Is Nothing Then
            Player = New MediaPlayer()
            AddHandler Player.MediaEnded, AddressOf OnMediaEnded
        End If

        MusicWindow = New Window()
        MusicWindow.Title = "音乐列表"
        MusicWindow.Width = 480
        MusicWindow.Height = 460
        MusicWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen
        AddHandler MusicWindow.Closed, AddressOf OnWindowClosed

        Dim Root As New DockPanel()

        '工具栏
        Dim Bar As New StackPanel()
        Bar.Orientation = Orientation.Horizontal
        Bar.Margin = New Thickness(8)
        DockPanel.SetDock(Bar, Dock.Top)
        Bar.Children.Add(MakeBarButton("播放 / 暂停", AddressOf OnTogglePlay))
        Bar.Children.Add(MakeBarButton("停止", AddressOf OnStop))
        Bar.Children.Add(MakeBarButton("重新扫描", AddressOf OnRescanClick))
        Bar.Children.Add(MakeBarButton("打开目录", AddressOf OnOpenFolderClick))
        Root.Children.Add(Bar)

        '底部状态
        StatusText = New TextBlock()
        StatusText.Margin = New Thickness(10, 4, 10, 8)
        StatusText.TextTrimming = TextTrimming.CharacterEllipsis
        DockPanel.SetDock(StatusText, Dock.Bottom)
        Root.Children.Add(StatusText)

        '列表
        MusicList = New ListBox()
        MusicList.Margin = New Thickness(8, 0, 8, 0)
        AddHandler MusicList.MouseDoubleClick, AddressOf OnListDoubleClick
        Root.Children.Add(MusicList)

        MusicWindow.Content = Root
        Rescan()
        MusicWindow.Show()
    End Sub

    Private Function MakeBarButton(text As String, handler As RoutedEventHandler) As Button
        Dim Button1 As New Button()
        Button1.Content = text
        Button1.Margin = New Thickness(0, 0, 8, 0)
        Button1.Padding = New Thickness(10, 4, 10, 4)
        AddHandler Button1.Click, handler
        Return Button1
    End Function

    '==================== 播放 ====================

    Private Sub PlayIndex(index As Integer)
        If index < 0 OrElse index >= MusicFiles.Count Then Return
        Try
            Player.Open(New Uri(MusicFiles(index), UriKind.Absolute))
            Player.Play()
            CurrentIndex = index
            IsPaused = False
            If MusicList IsNot Nothing Then MusicList.SelectedIndex = index
            SetStatus("正在播放：" & Path.GetFileName(MusicFiles(index)))
        Catch ex As Exception
            SetStatus("播放失败：" & ex.Message)
            Host.Log("播放失败：" & MusicFiles(index) & "：" & ex.Message, PluginLogLevel.Error)
        End Try
    End Sub

    Private Sub PlayNext()
        If MusicFiles.Count = 0 Then Return
        PlayIndex((CurrentIndex + 1) Mod MusicFiles.Count)
    End Sub

    Private Sub OnTogglePlay(sender As Object, e As RoutedEventArgs)
        If MusicFiles.Count = 0 Then Return
        '列表里选了别的歌，直接换歌
        If MusicList IsNot Nothing AndAlso MusicList.SelectedIndex >= 0 AndAlso MusicList.SelectedIndex <> CurrentIndex Then
            PlayIndex(MusicList.SelectedIndex)
            Return
        End If
        If CurrentIndex < 0 Then
            PlayIndex(0)
            Return
        End If
        If IsPaused Then
            Player.Play()
            IsPaused = False
            SetStatus("正在播放：" & Path.GetFileName(MusicFiles(CurrentIndex)))
        Else
            Player.Pause()
            IsPaused = True
            SetStatus("已暂停：" & Path.GetFileName(MusicFiles(CurrentIndex)))
        End If
    End Sub

    Private Sub OnStop(sender As Object, e As RoutedEventArgs)
        Player.Stop()
        IsPaused = False
        CurrentIndex = -1
        SetStatus("已停止")
    End Sub

    Private Sub OnRescanClick(sender As Object, e As RoutedEventArgs)
        Rescan()
        Host.Hint("已重新扫描音乐目录")
    End Sub

    Private Sub OnOpenFolderClick(sender As Object, e As RoutedEventArgs)
        Host.OpenFolder(GetMusicFolder())
    End Sub

    Private Sub OnListDoubleClick(sender As Object, e As MouseButtonEventArgs)
        If MusicList Is Nothing Then Return
        If MusicList.SelectedIndex >= 0 Then PlayIndex(MusicList.SelectedIndex)
    End Sub

    Private Sub OnMediaEnded(sender As Object, e As EventArgs)
        PlayNext()
    End Sub

    Private Sub OnWindowClosed(sender As Object, e As EventArgs)
        Try
            Player.Stop()
            Player.Close()
        Catch
        End Try
        Player = Nothing
        MusicWindow = Nothing
        MusicList = Nothing
        StatusText = Nothing
        CurrentIndex = -1
        IsPaused = False
    End Sub

    Private Sub SetStatus(text As String)
        If StatusText IsNot Nothing Then
            StatusText.Text = text
        Else
            Host.Log(text)
        End If
    End Sub

End Class
