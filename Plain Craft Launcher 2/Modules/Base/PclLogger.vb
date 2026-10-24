Public Class PclLogger
    Inherits FileLogger

    ''' <inheritdoc/>
    Public Overrides Function Format(Text As String, Level As LogLevel, FilePath As String, Ex As Exception) As String
        Text = MyBase.Format(Text, Level, FilePath, Ex)
        Text = FilterUserName(FilterAccessToken(Text, "*"c), "*"c)
        Return Text
    End Function

    ''' <inheritdoc/>
    Public Overrides Sub HandleBehavior(RawMessage As String, FormattedMessage As String, Behavior As LogBehavior, Ex As Exception)
        If IsProgramEnding Then Return
        MyBase.HandleBehavior(RawMessage, FormattedMessage, Behavior, Ex)
        Dim BriefText = If(Ex Is Nothing, RawMessage, If(RawMessage Is Nothing, "", $"{RawMessage}：") & Ex.GetDisplay(False))
        BriefText = FilterUserName(FilterAccessToken(BriefText, "*"c), "*"c)
        Dim DetailText = If(Ex Is Nothing, RawMessage, If(RawMessage Is Nothing, "", $"{RawMessage}：") & Ex.GetDisplay(True))
        DetailText = FilterUserName(FilterAccessToken(DetailText, "*"c), "*"c)
        Select Case Behavior
            Case LogBehavior.None
                '啥也不干
            Case LogBehavior.ToastIfDebug
                If BuildType = BuildTypes.Debug OrElse ModeDebug Then Hint("[调试模式] " & BriefText, HintType.Blue, False)
            Case LogBehavior.Toast
                Hint(BriefText, HintType.Red, False)
            Case LogBehavior.Alert
                MyMsgBox(DetailText, "错误", IsWarn:=True)
            Case LogBehavior.AlertThenFeedback
                If PageSetupSystem.IsLauncherNewest Then
                    If MyMsgBox(DetailText & vbCrLf & vbCrLf & "是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！", "错误", "反馈", "取消", IsWarn:=True) = 1 Then Feedback(False, True)
                Else
                    MyMsgBox(DetailText & vbCrLf & vbCrLf & "将 PCL 更新至最新版或许可以解决这个问题……", "错误", IsWarn:=True)
                End If
            Case LogBehavior.AlertThenCrash
                Static FirstTrigger As Boolean = True
                If FirstTrigger Then
                    '首次触发
                    FirstTrigger = False
                    If PageSetupSystem.IsLauncherNewest Then
                        If MsgBox(DetailText & vbCrLf & vbCrLf & "是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！", MsgBoxStyle.Critical + MsgBoxStyle.YesNo, "错误") = MsgBoxResult.Yes Then Feedback(False, True)
                    Else
                        MsgBox(DetailText & vbCrLf & vbCrLf & "将 PCL 更新至最新版或许可以解决这个问题……", MsgBoxStyle.Critical, "错误")
                    End If
                Else
                    '多次触发，直接使程序崩溃（这通常代表着在其他线程循环触发严重异常）
                    Thread.Sleep(2000)
                End If
                FormMain.EndProgramForce(ProcessReturnValues.Exception)
        End Select
        '遥测
        If Behavior >= LogBehavior.Toast Then Telemetry("错误日志", "Exception", DetailText)
    End Sub

End Class

' PCLFE：可选的详细日志。
' 在“设置 → 调试选项 → 记录详细日志”开启时，把全部日志（Trace 级别）额外写入
' PCL\PCLFEDetail1.txt，方便用户反馈问题时附上完整运行记录。默认关闭，不影响性能。
Public Class PclfeLogger
    Inherits PclLogger

    ' 是否开启详情日志（由设置项的 OnChanged 维护，避免热路径每次读注册表）
    Private Shared _detailEnabled As Boolean = False
    ' 详情日志的文件写入器（懒初始化）
    Private _detailWriter As System.IO.StreamWriter = Nothing
    Private ReadOnly _detailLock As New Object()

    ''' <summary>由设置项 SystemLogDetail 的 OnChanged 调用，控制是否写入详情日志。</summary>
    Public Shared Sub SetDetailEnabled(value As Boolean)
        _detailEnabled = value
    End Sub

    ''' <summary>设置变更时应用“记录详细日志”开关：同步开关状态并刷新日志级别。</summary>
    Public Shared Sub ApplyDetailSetting()
        Dim Enabled = Settings.Get(Of Boolean)("SystemLogDetail")
        _detailEnabled = Enabled
        Logger.Instance.MinLevel = If(ModeDebug OrElse Enabled, LogLevel.Trace, LogLevel.Info)
    End Sub

    Public Overrides Sub Output(entry As BaseLogger.LogEntry)
        ' 原有行为：写入 Log1.txt 与调试输出
        MyBase.Output(entry)
        ' 详情日志：开启时额外写入 PCLFEDetail1.txt
        If _detailEnabled Then
            Try
                SyncLock _detailLock
                    If _detailWriter Is Nothing Then
                        Dim Folder = Paths.BaseThenName
                        DirectoryUtils.Create(Folder)
                        Dim LogPath = System.IO.Path.Combine(Folder, "PCLFEDetail1.txt")
                        ' 超过 10MB 时清空，避免无限增长
                        If System.IO.File.Exists(LogPath) AndAlso New System.IO.FileInfo(LogPath).Length > 10L * 1024L * 1024L Then
                            Try : System.IO.File.Delete(LogPath) : Catch : End Try
                        End If
                        _detailWriter = New System.IO.StreamWriter(LogPath, append:=True) With {.AutoFlush = True}
                    End If
                    _detailWriter.WriteLine(entry.message)
                End SyncLock
            Catch
                ' 详情日志写入失败绝不影响主程序
            End Try
        Else
            SyncLock _detailLock
                If _detailWriter IsNot Nothing Then
                    Try : _detailWriter.Dispose() : Catch : End Try
                    _detailWriter = Nothing
                End If
            End SyncLock
        End If
    End Sub

End Class
