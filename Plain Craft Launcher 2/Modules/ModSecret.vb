'由于包含加解密等安全信息，本文件中的部分代码已被删除

Friend Module ModSecret

    '标注 PCL 的不同分支，仅用于替换标记
    Public Const VersionBranchMain As String = "PCLFE"
    '在开源版的注册表与常规版的注册表隔离，以防数据冲突
    Public Const RegFolder As String = "PCLDebug"
    '用于微软登录的 ClientId
    Public OAuthClientId As String = If(Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID"), "00000000402b5328")
    'CurseForge API Key
    Public CurseForgeAPIKey As String = If(Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY"), "")
    '用于匿名数据收集的腾讯云日志服务上报 URL，形如 https://{region}.cls.tencentcs.com/track?topic_id={topic_id}
    Public Const ClsBaseUrl As String = ""

#Region "网络鉴权"

    Public Const RestrictMcDownload As Boolean = False
    Friend Function SecretCdnSign(UrlWithMark As String) As String
        If Not UrlWithMark.EndsWithF("{CDN}") Then Return UrlWithMark
        Return UrlWithMark.Replace("{CDN}", "").Replace(" ", "%20")
    End Function
    ''' <summary>
    ''' 设置 Headers 的 UA、Referer。
    ''' </summary>
    Friend Sub SecretHeadersSign(Url As String, ByRef Req As HttpRequestMessage, Optional SimulateBrowserHeaders As Boolean = False)
        If Not Req.Headers.UserAgent.Any Then
            If Url.Contains("baidupcs.com") OrElse Url.Contains("baidu.com") Then
                Req.Headers.Add("User-Agent", "LogStatistic")  '#4951
            ElseIf SimulateBrowserHeaders Then
                Req.Headers.Add("User-Agent", $"PCLFE/{VersionBaseName}.{CInt(BuildType)} Mozilla/5.0 AppleWebKit/537.36 Chrome/63.0.3239.132 Safari/537.36")
            Else
                Req.Headers.Add("User-Agent", $"PCLFE/{VersionBaseName}.{CInt(BuildType)}")
            End If
        End If
        If Not SimulateBrowserHeaders Then Req.Headers.Add("Referer", $"http://{VersionCode}.open.pcl2.server/")
        If Url.Contains("api.curseforge.com") OrElse Url.Contains("forgecdn.net") Then Req.Headers.Add("x-api-key", CurseForgeAPIKey)
    End Sub

#End Region

#Region "主题"

    Public Color1 As New MyColor(52, 61, 74)
    Public Color2 As New MyColor(11, 91, 203)
    Public Color3 As New MyColor(19, 112, 243)
    Public Color4 As New MyColor(72, 144, 245)
    Public Color5 As New MyColor(150, 192, 249)
    Public Color6 As New MyColor(213, 230, 253)
    Public Color7 As New MyColor(222, 236, 253)
    Public Color8 As New MyColor(234, 242, 254)
    Public ColorBg0 As New MyColor(150, 192, 249)
    Public ColorBg1 As New MyColor(190, Color7)
    Public ColorGray1 As New MyColor(64, 64, 64)
    Public ColorGray2 As New MyColor(115, 115, 115)
    Public ColorGray3 As New MyColor(140, 140, 140)
    Public ColorGray4 As New MyColor(166, 166, 166)
    Public ColorGray5 As New MyColor(204, 204, 204)
    Public ColorGray6 As New MyColor(235, 235, 235)
    Public ColorGray7 As New MyColor(240, 240, 240)
    Public ColorGray8 As New MyColor(245, 245, 245)
    Public ColorSemiTransparent As New MyColor(1, Color8)

    Public ThemeNow As Integer = -1
    Public ColorHue As Integer = 0, ColorSat As Integer = 85, ColorLightAdjust As Integer = 0, ColorHueTopbarDelta As OneOf(Of Integer, Integer()) = 0
    Public ThemeDontClick As Integer = 0

    Public Sub ThemeRefresh(Optional NewTheme As Integer = -1)
        Try
            If ThemeNow = NewTheme AndAlso NewTheme >= 0 Then Return
            If NewTheme >= 0 Then ThemeNow = NewTheme

            Color1 = New MyColor().FromHSL2(ColorHue, ColorSat * 0.2, 25 + ColorLightAdjust * 0.3)
            Color2 = New MyColor().FromHSL2(ColorHue, ColorSat, 45 + ColorLightAdjust)
            Color3 = New MyColor().FromHSL2(ColorHue, ColorSat, 55 + ColorLightAdjust)
            Color4 = New MyColor().FromHSL2(ColorHue, ColorSat, 65 + ColorLightAdjust)
            Color5 = New MyColor().FromHSL2(ColorHue, ColorSat, 80 + ColorLightAdjust * 0.4)
            Color6 = New MyColor().FromHSL2(ColorHue, ColorSat, 91 + ColorLightAdjust * 0.1)
            Color7 = New MyColor().FromHSL2(ColorHue, ColorSat, 95)
            Color8 = New MyColor().FromHSL2(ColorHue, ColorSat, 97)
            ColorBg0 = Color4 * 0.4 + Color5 * 0.4 + ColorGray4 * 0.2
            ColorBg1 = New MyColor(190, Color7)

            ColorSemiTransparent = New MyColor(1, Color8)
            Application.Current.Resources("ColorBrush1") = New SolidColorBrush(Color1)
            Application.Current.Resources("ColorBrush2") = New SolidColorBrush(Color2)
            Application.Current.Resources("ColorBrush3") = New SolidColorBrush(Color3)
            Application.Current.Resources("ColorBrush4") = New SolidColorBrush(Color4)
            Application.Current.Resources("ColorBrush5") = New SolidColorBrush(Color5)
            Application.Current.Resources("ColorBrush6") = New SolidColorBrush(Color6)
            Application.Current.Resources("ColorBrush7") = New SolidColorBrush(Color7)
            Application.Current.Resources("ColorBrush8") = New SolidColorBrush(Color8)
            Application.Current.Resources("ColorBrushBg0") = New SolidColorBrush(ColorBg0)
            Application.Current.Resources("ColorBrushBg1") = New SolidColorBrush(ColorBg1)
            Application.Current.Resources("ColorObject1") = CType(Color1, Color)
            Application.Current.Resources("ColorObject2") = CType(Color2, Color)
            Application.Current.Resources("ColorObject3") = CType(Color3, Color)
            Application.Current.Resources("ColorObject4") = CType(Color4, Color)
            Application.Current.Resources("ColorObject5") = CType(Color5, Color)
            Application.Current.Resources("ColorObject6") = CType(Color6, Color)
            Application.Current.Resources("ColorObject7") = CType(Color7, Color)
            Application.Current.Resources("ColorObject8") = CType(Color8, Color)
            Application.Current.Resources("ColorObjectBg0") = CType(ColorBg0, Color)
            Application.Current.Resources("ColorObjectBg1") = CType(ColorBg1, Color)
            ThemeRefreshMain()
        Catch ex As Exception
            Logger.Error(ex, "刷新主题颜色失败", LogBehavior.Toast)
        End Try
    End Sub
    Public Sub ThemeRefreshMain()
        RunInUi(
        Sub()
            If Not FrmMain.IsLoaded Then Return
            '顶部条背景
            Dim Brush = New LinearGradientBrush With {.EndPoint = New Point(1, 0), .StartPoint = New Point(0, 0)}
            Dim Deltas = ColorHueTopbarDelta.Switch(Function(d) New Integer() {-d, 0, d}, Function(d) d)
            Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue + Deltas(0), ColorSat, 48 + ColorLightAdjust)})
            Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = New MyColor().FromHSL2(ColorHue + Deltas(1), ColorSat, 54 + ColorLightAdjust)})
            Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue + Deltas(2), ColorSat, 48 + ColorLightAdjust)})
            FrmMain.PanTitle.Background = Brush
            FrmMain.PanTitle.Background.Freeze()
            '主页面背景
            If Settings.Get(Of Boolean)("UiBackgroundColorful") Then
                Brush = New LinearGradientBrush With {.EndPoint = New Point(0.1, 1), .StartPoint = New Point(0.9, 0)}
                Brush.GradientStops.Add(New GradientStop With {.Offset = -0.1, .Color = New MyColor().FromHSL2(ColorHue - 15, ColorSat * 0.8, 91)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.4, .Color = New MyColor().FromHSL2(ColorHue, ColorSat * 0.8, 91)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 1.1, .Color = New MyColor().FromHSL2(ColorHue + 15, ColorSat * 0.8, 91)})
                FrmMain.PanForm.Background = Brush
            Else
                FrmMain.PanForm.Background = New MyColor(245, 245, 245)
            End If
            FrmMain.PanForm.Background.Freeze()
        End Sub)
    End Sub
    Friend Sub ThemeCheckAll(EffectSetup As Boolean)
    End Sub
    Friend Function ThemeCheckOne(Id As Integer) As Boolean
        Return True
    End Function
    Friend Function ThemeUnlock(Id As Integer, Optional ShowDoubleHint As Boolean = True, Optional UnlockHint As String = Nothing) As Boolean
        Return False
    End Function

#End Region

#Region "更新"

    'PCLFE 自更新配置：版本信息从仓库根目录的 version.json 读取，更新包为 GitHub Release 中的启动器 exe。
    Private Const UpdateVersionUrl As String = "https://raw.githubusercontent.com/FireWaterDog/Plain-Craft-Launcher-FireWaterDog-Edition/main/version.json"

    Friend IsUpdateWaitingRestart As Boolean = False

    ''' <summary>
    ''' 「设置 → 其他 → 检查更新」按钮触发：立即检查并提示。
    ''' </summary>
    Friend Sub UpdateCheckByButton()
        RunInThread(Sub() CheckUpdate(True))
    End Sub

    ''' <summary>
    ''' 启动后后台检查：发现有新版本时弹窗提示，由用户手动确认下载。
    ''' </summary>
    Friend Sub UpdateCheckAuto()
        RunInNewThread(Sub()
            Try
                System.Threading.Thread.Sleep(15000) '启动后稍等，避免打断初始化
            Catch
            End Try
            CheckUpdate(False)
        End Sub, "UpdateCheckAuto", ThreadPriority.BelowNormal)
    End Sub

    ''' <summary>
    ''' 读取 version.json 并与当前版本比较，若有更新则弹窗提示。
    ''' </summary>
    Private Sub CheckUpdate(ShowNoUpdateHint As Boolean)
        Try
            Dim Raw As String = NetRequestByClientRetry(UpdateVersionUrl, RequireJson:=True)
            Dim Info As JObject = Raw.DeserializeJson()
            If Info Is Nothing OrElse Info("versionCode") Is Nothing Then
                If ShowNoUpdateHint Then RunInUi(Sub() Hint("检查更新失败：版本信息格式错误。", HintType.Red, False))
                Return
            End If
            Dim LatestCode As Integer = Info("versionCode").ToObject(Of Integer)
            If LatestCode <= VersionCode Then
                If ShowNoUpdateHint Then RunInUi(Sub() Hint($"已是最新版本（{VersionDisplay}）", HintType.Finish, False))
                Return
            End If
            Dim VersionName As String = If(Info("versionName") IsNot Nothing, Info("versionName").ToString, LatestCode.ToString)
            Dim Changelog As String = If(Info("changelog") IsNot Nothing, Info("changelog").ToString, "")
            RunInUi(Sub()
                Dim Msg As String = $"发现新版本：{VersionName}{vbCrLf}{vbCrLf}{If(Changelog = "", "（未提供更新说明）", Changelog)}"
                If MyMsgBox(Msg, "PCLFE 更新", "下载并更新", "取消", IsWarn:=False) = 1 Then
                    UpdateDownloadAndApply(Info)
                End If
            End Sub)
        Catch ex As Exception
            Logger.Warn(ex, "检查 PCLFE 更新失败")
            If ShowNoUpdateHint Then RunInUi(Sub() Hint("检查更新失败：" & ex.Message, HintType.Red, False))
        End Try
    End Sub

    ''' <summary>
    ''' 下载更新包（启动器 exe）到临时目录，完成后替换自身并重启。
    ''' </summary>
    Private Sub UpdateDownloadAndApply(Info As JObject)
        Dim Url As String = If(Info("downloadUrl") IsNot Nothing, Info("downloadUrl").ToString, "")
        If String.IsNullOrEmpty(Url) Then
            RunInUi(Sub() Hint("更新包下载地址为空，无法更新。", HintType.Red, False))
            Return
        End If
        RunInUi(Sub() Hint("正在下载 PCLFE 更新…", HintType.Info, False))
        RunInThread(Sub()
            Try
                Dim TempExe As String = PathTemp & "Update\PCLFE-New.exe"
                Try : System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(TempExe)) : Catch : End Try
                NetDownloadByClient(Url, TempExe)
                RunInUi(Sub() Hint("更新已下载，正在重启以完成更新…", HintType.Finish, False))
                System.Threading.Thread.Sleep(800)
                ApplyUpdate(TempExe)
            Catch ex As Exception
                Logger.Warn(ex, "下载或应用 PCLFE 更新失败")
                RunInUi(Sub() Hint("更新失败：" & ex.Message, HintType.Red, False))
            End Try
        End Sub)
    End Sub

    ''' <summary>
    ''' 用下载好的新 exe 替换正在运行的启动器并重启。
    ''' Windows 允许重命名正在运行的 exe，因此先重命名旧文件再写入新文件，规避文件占用。
    ''' </summary>
    Private Sub ApplyUpdate(TempExe As String)
        Dim CurrentExe As String = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName
        Dim Backup As String = CurrentExe & ".bak"
        If System.IO.File.Exists(Backup) Then
            Try : System.IO.File.Delete(Backup) : Catch : End Try
        End If
        System.IO.File.Move(CurrentExe, Backup)
        System.IO.File.Copy(TempExe, CurrentExe, True)
        Try : System.Diagnostics.Process.Start(CurrentExe) : Catch : End Try
        Environment.Exit(0)
    End Sub

    Public Sub UpdateRestart(TriggerRestartAndByEnd As Boolean)
        If Not TriggerRestartAndByEnd Then Return
        Dim CurrentExe As String = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName
        Try : System.Diagnostics.Process.Start(CurrentExe) : Catch ex As Exception : Logger.Warn(ex, "更新重启失败") : End Try
        Environment.Exit(0)
    End Sub

    Public Sub UpdateReplace(ProcessId As Integer, OldFileName As String, NewFileName As String, TriggerRestart As Boolean)
        'PCLFE 采用整包 exe 替换方式，无需此增量替换流程，保留接口以兼容引用。
    End Sub

    ''' <summary>
    ''' 确保 PathTemp/Latest.exe 是最新正式版的 PCL，它会被用于整合包打包。
    ''' 如果不是，则下载一个。
    ''' </summary>
    Friend Sub DownloadLatestPCL(Optional LoaderToSyncProgress As LoaderBase = Nothing)
        '注意：如果要自行实现这个功能，请换用另一个文件路径，以免与官方版本冲突
    End Sub

#End Region

#Region "联网配置"

    ''' <summary>
    ''' 联网获取的配置信息。
    ''' 若获取失败或仍在获取中，可能为 Nothing。
    ''' </summary>
    Public ServerConfig As JObject

    Public ServerLoader As New LoaderTask(Of Integer, Integer)("PCL 配置更新",
    Sub()
        ' PCLFE：开源分支无法联网获取配置，改为从本地 PCL\ServerConfig.json 读取（内容取自 PCL 官方版缓存）。
        Try
            Dim LocalConfigPath As String = Paths.Base & "PCL\ServerConfig.json"
            If FileUtils.Exists(LocalConfigPath) Then
                ServerConfig = FileUtils.ReadAsString(LocalConfigPath).DeserializeJson()
                Logger.Info($"已从本地载入联机配置：{LocalConfigPath}")
            Else
                Logger.Info($"未找到本地联机配置文件：{LocalConfigPath}，联机功能需要该文件")
            End If
        Catch ex As Exception
            Logger.Warn(ex, "读取本地 ServerConfig.json 失败")
        End Try
    End Sub, Priority:=ThreadPriority.BelowNormal) With
        {.ReloadTimeout = 1000 * 60 * 60} '超时 1 小时

#End Region

#Region "赞助等级"

    Public ReadOnly Property CurrentRank As DonationRank
        Get
            Return DonationRank.None
        End Get
    End Property

    Public Sub InputPotatoCode(IsUpdating As Boolean)
    End Sub
    Friend Sub GeneratePotatoCode()
    End Sub

    ''' <summary>
    ''' 获取设备识别码。
    ''' </summary>
    Friend Function GetIdentify() As String
        Return "0000-0000-0000-0000"
    End Function

#End Region

End Module
