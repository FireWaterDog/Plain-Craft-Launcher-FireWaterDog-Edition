Imports Microsoft.Web.WebView2.Core

Public Class MyLoginBrowser

    Private Const RedirectPrefix As String = "https://login.live.com/oauth20_desktop.srf"
    Private AuthUrl As String
    Private Completed As Boolean = False
    Public AuthorizationCode As String = Nothing

    Public Sub New(AuthUrl As String)
        InitializeComponent()
        Me.AuthUrl = AuthUrl
    End Sub

    Private Async Sub Window_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Try
            Await Web.EnsureCoreWebView2Async()
            AddHandler Web.CoreWebView2.NavigationStarting, AddressOf OnNavigationStarting
            Web.CoreWebView2.Navigate(AuthUrl)
            LoadingMask.Visibility = Visibility.Collapsed
        Catch ex As Exception
            Logger.Error(ex, "内置浏览器（WebView2）初始化失败")
            MessageBox.Show("无法初始化内置浏览器：" & ex.Message & vbCrLf & vbCrLf &
                            "请安装 Microsoft Edge WebView2 运行时后再试。" & vbCrLf &
                            "下载地址：https://developer.microsoft.com/microsoft-edge/webview2/",
                            "微软登录", MessageBoxButton.OK, MessageBoxImage.Warning)
            DialogResult = False
        End Try
    End Sub

    Private Sub OnNavigationStarting(sender As Object, e As CoreWebView2NavigationStartingEventArgs)
        Dim Code As String = Nothing
        If TryExtractCode(e.Uri, Code) Then
            e.Cancel = True
            AuthorizationCode = Code
            Completed = True
            Dispatcher.BeginInvoke(Sub() DialogResult = True)
        End If
    End Sub

    '从回调链接中提取授权码（链接须以 oauth20_desktop.srf 开头，code 可能带 # 片段）。
    Private Shared Function TryExtractCode(Url As String, ByRef Code As String) As Boolean
        Code = Nothing
        If String.IsNullOrWhiteSpace(Url) Then Return False
        If Not Url.StartsWith(RedirectPrefix, StringComparison.OrdinalIgnoreCase) Then Return False
        Dim QueryIndex As Integer = Url.IndexOf("?")
        If QueryIndex < 0 Then Return False
        Dim Query As String = Url.Substring(QueryIndex + 1)
        For Each Pair As String In Query.Split("&"c)
            Dim Eq As Integer = Pair.IndexOf("=")
            If Eq > 0 AndAlso Pair.Substring(0, Eq) = "code" Then
                Dim Value As String = Pair.Substring(Eq + 1)
                Dim Hash As Integer = Value.IndexOf("#")
                If Hash >= 0 Then Value = Value.Substring(0, Hash)
                Code = Uri.UnescapeDataString(Value)
                Return Not String.IsNullOrEmpty(Code)
            End If
        Next
        Return False
    End Function

End Class
