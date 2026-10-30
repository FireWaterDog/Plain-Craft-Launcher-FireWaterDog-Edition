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
    '国内直连 raw.githubusercontent.com 常不可达，故配置了多条加速源与官方源，逐个尝试直到拿到有效版本信息。
    '注意：ZStatic（s4.zstatic.net）只镜像 cdnjs 与 unpkg，不代理 GitHub，也不能存自定义文件，因此不能作为更新源。
    Private Const UpdateRepo As String = "FireWaterDog/Plain-Craft-Launcher-FireWaterDog-Edition"
    Private Const UpdateVersionUrlRaw As String = "https://raw.githubusercontent.com/" & UpdateRepo & "/main/version.json"

    Friend IsUpdateWaitingRestart As Boolean = False

    ''' <summary>
    ''' 更新检查用的版本信息源，按优先级排列：国内反代优先（内容实时），jsDelivr 次之（有缓存，可能滞后），官方源兜底。
    ''' </summary>
    Private Function GetUpdateVersionSources() As List(Of KeyValuePair(Of String, String))
        Return New List(Of KeyValuePair(Of String, String)) From {
            New KeyValuePair(Of String, String)("gh-proxy 加速", "https://gh-proxy.com/" & UpdateVersionUrlRaw),
            New KeyValuePair(Of String, String)("ghfast 加速", "https://ghfast.top/" & UpdateVersionUrlRaw),
            New KeyValuePair(Of String, String)("jsDelivr（Fastly 节点）", "https://fastly.jsdelivr.net/gh/" & UpdateRepo & "@main/version.json"),
            New KeyValuePair(Of String, String)("jsDelivr（Gcore 节点）", "https://gcore.jsdelivr.net/gh/" & UpdateRepo & "@main/version.json"),
            New KeyValuePair(Of String, String)("jsDelivr", "https://cdn.jsdelivr.net/gh/" & UpdateRepo & "@main/version.json"),
            New KeyValuePair(Of String, String)("GitHub 官方源", UpdateVersionUrlRaw)
        }
    End Function

    ''' <summary>
    ''' 为更新包地址生成候选下载源：原始 GitHub 地址优先，失败后逐个尝试国内加速镜像。
    ''' </summary>
    Private Function GetUpdateDownloadUrls(Url As String) As List(Of String)
        Dim Result As New List(Of String) From {Url}
        If Url.Contains("github.com") OrElse Url.Contains("githubusercontent.com") Then
            For Each Mirror As String In {"https://gh-proxy.com/", "https://ghfast.top/", "https://mirror.ghproxy.com/", "https://ghproxy.net/"}
                Dim Candidate As String = Mirror & Url
                If Not Result.Contains(Candidate) Then Result.Add(Candidate)
            Next
        End If
        Return Result
    End Function

    ''' <summary>
    ''' 「设置 → 其他 → 检查更新」按钮触发：立即检查并提示。
    ''' </summary>
    Friend Sub UpdateCheckByButton()
        RunInThread(Sub() CheckUpdate(True))
    End Sub

    ''' <summary>
    ''' 启动后后台检查：发现有新版本时自动从 GitHub 下载，无需用户确认。
    ''' </summary>
    Friend Sub UpdateCheckAuto()
        RunInNewThread(Sub()
                           Try
                               '上次已下载好但尚未安装的更新包：直接提示应用
                               Dim PendingExe As String = PathTemp & "Update\PCLFE-New.exe"
                               If FileUtils.Exists(PendingExe) Then
                                   RunInUi(Sub()
                                               If MyMsgBox("检测到已下载完成的 PCLFE 更新包，是否立即完成更新？", "PCLFE 更新", "立即更新", "取消") = 1 Then
                                                   ApplyUpdate(PendingExe)
                                               Else
                                                   '用户明确拒绝时移除旧更新包，避免每次启动都提示
                                                   Try : FileUtils.Delete(PendingExe) : Catch : End Try
                                               End If
                                           End Sub)
                                   Return
                               End If
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
            '依次尝试多个更新源，取能拿到的最高版本号；任一源报告有更新即可提前结束
            Dim BestInfo As JObject = Nothing
            Dim BestCode As Integer = -1
            Dim SuccessCount As Integer = 0
            Dim LastError As Exception = Nothing
            For Each Source In GetUpdateVersionSources()
                Try
                    Dim Raw As String = NetRequestByClient(Source.Value, RequireJson:=True)
                    If String.IsNullOrWhiteSpace(Raw) Then Continue For
                    Dim Fetched As JObject = Raw.DeserializeJson()
                    If Fetched Is Nothing OrElse Fetched("versionCode") Is Nothing Then Continue For
                    Dim FetchedCode As Integer = Fetched("versionCode").ToObject(Of Integer)
                    SuccessCount += 1
                    'jsDelivr 等 CDN 有缓存，可能返回旧版本；只有在更大时才覆盖，避免被滞后数据影响
                    If FetchedCode > BestCode Then
                        BestCode = FetchedCode
                        BestInfo = Fetched
                    End If
                    If FetchedCode > VersionCode Then Exit For
                Catch FetchEx As Exception
                    LastError = FetchEx
                    Logger.Warn(FetchEx, $"获取版本信息失败（{Source.Key}：{Source.Value}），尝试下一个源")
                End Try
            Next
            If BestInfo Is Nothing Then
                If LastError IsNot Nothing Then Throw LastError
                Throw New Exception("所有更新源均未返回版本信息")
            End If
            Logger.Info($"更新检查完成：{SuccessCount} 个源返回有效数据，最新 versionCode={BestCode}")
            Dim Info As JObject = BestInfo
            If BestCode <= VersionCode Then
                If ShowNoUpdateHint Then RunInUi(Sub() Hint($"已是最新版本（{VersionDisplay}）", HintType.Green, False))
                Return
            End If
            Dim VersionName As String = If(Info("versionName") IsNot Nothing, Info("versionName").ToString, LatestCode.ToString)
            Dim Changelog As String = If(Info("changelog") IsNot Nothing, Info("changelog").ToString, "")
            RunInUi(Sub()
                        If ShowNoUpdateHint Then
                            Dim Msg As String = $"发现新版本：{VersionName}{vbCrLf}{vbCrLf}{If(Changelog = "", "（未提供更新说明）", Changelog)}"
                            If MyMsgBox(Msg, "PCLFE 更新", "下载并更新", "取消", IsWarn:=False) = 1 Then
                                UpdateDownloadAndApply(Info)
                            End If
                        Else
                            '自动更新：发现新版本后直接从 GitHub 下载，无需用户确认
                            Hint($"发现 PCLFE 新版本 {VersionName}，正在自动下载更新…", HintType.Blue, False)
                            UpdateDownloadAndApply(Info, True)
                        End If
                    End Sub)
        Catch ex As Exception
            Logger.Warn(ex, "检查 PCLFE 更新失败")
            If ShowNoUpdateHint Then RunInUi(Sub() Hint("检查更新失败：" & ex.Message, HintType.Red, False))
        End Try
    End Sub

    ''' <summary>
    ''' 下载更新包（启动器 exe）到临时目录，完成后应用更新。
    ''' AutoInstall 为 True 时（启动自动检查）下载完成后询问重启时机；
    ''' 为 False 时（按钮手动触发）下载完成后立即替换并重启。
    ''' </summary>
    Private Sub UpdateDownloadAndApply(Info As JObject, Optional AutoInstall As Boolean = False)
        Dim Url As String = If(Info("downloadUrl") IsNot Nothing, Info("downloadUrl").ToString, "")
        Dim VersionName As String = If(Info("versionName") IsNot Nothing, Info("versionName").ToString, "")
        If String.IsNullOrEmpty(Url) Then
            RunInUi(Sub() Hint("更新包下载地址为空，无法更新。", HintType.Red, False))
            Return
        End If
        If Not AutoInstall Then RunInUi(Sub() Hint("正在下载 PCLFE 更新…", HintType.Blue, False))
        RunInThread(Sub()
                        Try
                            Dim TempExe As String = PathTemp & "Update\PCLFE-New.exe"
                            Try : System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(TempExe)) : Catch : End Try
                            '依次尝试多个下载源（原始地址 → 国内加速镜像），任一成功即停止
                            Dim DownloadUrls As List(Of String) = GetUpdateDownloadUrls(Url)
                            Dim DownloadError As Exception = Nothing
                            Dim Downloaded As Boolean = False
                            For Each DownloadUrl In DownloadUrls
                                Try
                                    NetDownloadByClient(DownloadUrl, TempExe)
                                    Downloaded = True
                                    Exit For
                                Catch DownloadEx As Exception
                                    DownloadError = DownloadEx
                                    Logger.Warn(DownloadEx, $"下载更新失败（{DownloadUrl}），尝试下一个源")
                                End Try
                            Next
                            If Not Downloaded Then
                                If DownloadError IsNot Nothing Then Throw DownloadError
                                Throw New Exception("所有下载源均失败")
                            End If
                            '校验更新包哈希（version.json 提供 sha256 字段时），防止下载损坏或被篡改
                            Dim ExpectedHash As String = If(Info("sha256") IsNot Nothing, Info("sha256").ToString.Trim.Lower, "")
                            If ExpectedHash.Length = 64 Then
                                Dim ActualHash As String = CryptographyUtils.ComputeFileHash(TempExe, CryptographyUtils.HashMethod.Sha256).Lower()
                                If ActualHash <> ExpectedHash Then
                                    Throw New Exception("更新包 SHA256 校验失败，文件可能损坏，请稍后重试")
                                End If
                                Logger.Info("更新包 SHA256 校验通过：" & ActualHash)
                            End If
                            If AutoInstall Then
                                RunInUi(Sub()
                                            If MyMsgBox($"PCLFE {VersionName} 更新已下载完成，是否立即重启以完成更新？",
                                                        "PCLFE 更新", "立即重启", "下次启动时自动更新") = 1 Then
                                                ApplyUpdate(TempExe)
                                            Else
                                                Hint("更新包已就绪，下次启动 PCLFE 时将自动完成更新。", HintType.Green, False)
                                            End If
                                        End Sub)
                            Else
                                RunInUi(Sub() Hint("更新已下载，正在重启以完成更新…", HintType.Green, False))
                                System.Threading.Thread.Sleep(800)
                                ApplyUpdate(TempExe)
                            End If
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

    ''' <summary>
    ''' PCLFE 内置的默认联机配置。
    ''' 开源分支无法联网获取配置，故首次运行时写入 PCL\ServerConfig.json，保证联机功能开箱可用。
    ''' 使用 EasyTier v2.6.4 与若干社区公共节点，可自行替换该文件。
    ''' </summary>
    Private ReadOnly PclfeDefaultServerConfig As String = "{""Link"":{""EasyTierVersion"":264,""Downloads"":[" &
        """https://gh-proxy.com/https://github.com/EasyTier/EasyTier/releases/download/v2.6.4/easytier-windows-{arch}-v2.6.4.zip""," &
        """https://ghproxy.net/https://github.com/EasyTier/EasyTier/releases/download/v2.6.4/easytier-windows-{arch}-v2.6.4.zip""," &
        """https://github.com/EasyTier/EasyTier/releases/download/v2.6.4/easytier-windows-{arch}-v2.6.4.zip""]," &
        """Argument"":""--no-tun"",""DiscoverPeerId"":0,""DiscoverPeer"":""tcp://et.sumnight.top:11010""," &
        """Peers"":[""tcp://et.sumnight.top:11010"",""tcp://easytier.weiai.org.cn:11010"",""tcp://225284.xyz:11010"",""tcp://183.230.36.171:11010""]," &
        """MandatoryPeers"":[],""PeersBlackList"":[],""RandomPeer"":1}}"

    Public ServerLoader As New LoaderTask(Of Integer, Integer)("PCL 配置更新",
    Sub()
        ' PCLFE：开源分支无法联网获取配置，改为从本地 PCL\ServerConfig.json 读取（内容取自 PCL 官方版缓存）。
        Try
            Dim LocalConfigPath As String = Paths.Base & "PCL\ServerConfig.json"
            If Not FileUtils.Exists(LocalConfigPath) Then
                '文件缺失时写入内置默认配置，避免联机功能直接不可用
                DirectoryUtils.Create(Paths.Base & "PCL\")
                FileUtils.Write(LocalConfigPath, PclfeDefaultServerConfig)
                Logger.Info($"已生成默认联机配置：{LocalConfigPath}")
            End If
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
        Dim Code As String = MyMsgBoxInput("输入土豆码", Text:="土豆码正确后会解锁隐藏的插件管理入口。", HintText:="土豆码")
        If Code Is Nothing Then Return
        If String.Equals(Code.Trim(), ModPlugin.PotatoCode, StringComparison.OrdinalIgnoreCase) Then
            Settings.Set("PluginButtonsVisible", True)
            Hint("土豆码正确，插件管理已解锁", HintType.Green)
            Try
                If FrmSetupLeft IsNot Nothing Then FrmSetupLeft.RefreshPluginItem()
            Catch ex As Exception
                Logger.Warn(ex, "刷新插件入口失败")
            End Try
        Else
            Hint("土豆码不正确", HintType.Red)
        End If
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
