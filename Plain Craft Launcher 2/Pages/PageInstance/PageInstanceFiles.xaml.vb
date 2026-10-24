Public Class PageInstanceFiles

    ''' <summary>管理页的种类。</summary>
    Public Enum FileKind
        ResourcePack = 0
        ShaderPack = 1
        World = 2
        Datapack = 3
    End Enum

    ''' <summary>当前种类（由 PageInstanceLeft 在导航前设置）。</summary>
    Public Shared Kind As FileKind = FileKind.ResourcePack
    ''' <summary>数据包模式下当前选中的世界文件夹（空则回退 saves）。</summary>
    Public Shared SelectedWorld As String = ""

    ' 列表图标（简易 SVG 路径）
    Private Const IconPack As String = "M884.4 130.6H140.6c-41.9 0-76 34.1-76 76v613.3c0 41.9 34.1 76 76 76h743.8c41.9 0 76-34.1 76-76V206.6c0-41.9-34.1-76-76-76z m-743.8 72h743.8c2.2 0 4 1.8 4 4v371.3L796 461.7c-21.7-27.3-54.1-43.7-88.8-45-34.7-1.3-68.1 12.7-91.7 38.3L454 630c-2.8 3-7.2 3.5-10.5 1.3l-103-70.3c-31.5-21.5-72.8-22.5-105.3-2.7l-98.6 60.1V206.6c0-2.2 1.8-4 4-4z m743.8 621.3H140.6c-2.2 0-4-1.8-4-4V702.7l136.1-83c8.4-5.1 19.1-4.8 27.2 0.7l103 70.3c15.9 10.8 35.1 15.6 54.2 13.4 19-2.2 36.7-11.2 49.8-25.3l161.6-175c9.5-10.3 22.3-15.7 36.2-15.1 13.9 0.5 26.3 6.8 35.1 17.8l148.7 187v126.3c-0.1 2.3-1.9 4.1-4.1 4.1z M231.8 400.6a69.6 69.6 0 1 0 98.4-98.4 69.6 69.6 0 1 0-98.4 98.4Z"
    Private Const IconShader As String = "M512 0c25 0 42 17 42 42v85c0 25-17 42-42 42s-42-17-42-42V42c0-25 17-42 42-42zM512 853c25 0 42 17 42 42v85c0 25-17 42-42 42s-42-17-42-42v-85c0-25 17-42 42-42zM209 149c-17-17-42-17-59 0-17 17-17 42 0 59l59 59c17 17 42 17 59 0 17-17 17-42 0-59L209 149zM755 755c17-17 42-17 59 0l59 59c17 17 17 42 0 59s-42 17-59 0l-59-59c-17-17-17-46 0-59zM42 469c-25 0-42 17-42 42s17 42 42 42h85c25 0 42-17 42-42s-17-42-42-42H42zM853 512c0-25 17-42 42-42h85c25 0 42 17 42 42s-17 42-42 42h-85c-25 0-42-17-42-42zM149 814c-17 17-17 42 0 59 17 17 42 17 59 0l59-59c17-17 17-42 0-59-17-17-42-17-59 0l-59 59zM755 268c-17-17-17-42 0-59l59-59c17-17 42-17 59 0s17 42 0 59l-59 59c-17 17-46 17-59 0z M512 213c-166 0-298 132-298 298s132 298 298 298 298-132 298-298-132-298-298-298z m0 512c-119 0-213-93-213-213s93-213 213-213 213 93 213 213-93 213-213 213z"
    Private Const IconWorld As String = "M512 96C282 96 96 282 96 512C96 742 282 928 512 928C742 928 928 742 928 512C928 282 742 96 512 96Z M512 96C400 220 340 370 340 512C340 654 400 804 512 928C624 804 684 654 684 512C684 370 624 220 512 96Z"
    Private Const IconDataPack As String = "M445 545c18 0 33 14 33 33v149a182 182 0 1 1-182-182z m282 0a182 182 0 1 1-182 182v-149c0-18 14-33 33-33zM412 611H296a116 116 0 1 0 116 116V611z m315 0H611v116a116 116 0 1 0 116-116zM296 113a182 182 0 0 1 182 182v149a33 33 0 0 1-33 33H296a182 182 0 0 1 0-365z m431 0a182 182 0 0 1 0 365h-149a33 33 0 0 1-33-33V296A182 182 0 0 1 727 113zM296 180a116 116 0 0 0 0 232H412V296c0-62-48-112-109-115z m431 0a116 116 0 0 0-116 116V412h116c62 0 112-48 115-109l0-6a116 116 0 0 0-116-116z"

    ' 当前筛选：0 全部 / 1 启用 / 2 禁用
    Private FilterMode As Integer = 0
    ' 图标缓存（按完整路径），避免搜索/刷新时重复读压缩包
    Private ReadOnly IconCache As New Dictionary(Of String, ImageSource)

    Private Sub PageInstanceFiles_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        ReloadForKind()
    End Sub

    ''' <summary>获取当前种类对应的文件夹路径。</summary>
    Private Function GetFolder() As String
        If PageInstanceLeft.Instance Is Nothing Then Return ""
        Dim Base As String = PageInstanceLeft.Instance.PathIndie
        Select Case Kind
            Case FileKind.ResourcePack
                Return Base & "resourcepacks\"
            Case FileKind.ShaderPack
                Return Base & "shaderpacks\"
            Case FileKind.World
                Return Base & "saves\"
            Case FileKind.Datapack
                If Not String.IsNullOrEmpty(SelectedWorld) Then Return SelectedWorld & "\datapacks\"
                Return Base & "saves\"
            Case Else
                Return Base
        End Select
    End Function

    ''' <summary>按当前种类重建列表。</summary>
    Public Sub ReloadForKind()
        Try
            If PanList Is Nothing Then Return
            PanList.Children.Clear()
            If BtnBack IsNot Nothing Then BtnBack.Visibility = If(Kind = FileKind.Datapack, Visibility.Visible, Visibility.Collapsed)
            Dim Folder As String = GetFolder()
            If String.IsNullOrEmpty(Folder) Then Return
            Try : DirectoryUtils.Create(Folder) : Catch : End Try
            Dim IsWorld As Boolean = (Kind = FileKind.World)
            ' 枚举全部
            Dim AllEntries As New List(Of String)
            If DirectoryUtils.Exists(Folder) Then
                If IsWorld Then
                    For Each Dir As String In DirectoryUtils.EnumerateDirectories(Folder, False)
                        AllEntries.Add(Dir)
                    Next
                Else
                    For Each File As String In DirectoryUtils.EnumerateFiles(Folder, False)
                        AllEntries.Add(File)
                    Next
                End If
            End If
            AllEntries = AllEntries.OrderBy(Function(p) System.IO.Path.GetFileName(p)).ToList()
            ' 统计并更新筛选标签（世界没有启用/禁用概念）
            Dim Total As Integer = AllEntries.Count
            Dim DisabledCount As Integer = AllEntries.Where(Function(p) IsDisabledName(p)).Count()
            Dim EnabledCount As Integer = Total - DisabledCount
            If BtnFilterAll IsNot Nothing Then BtnFilterAll.Text = $"全部 ({Total})"
            If BtnFilterEnabled IsNot Nothing Then
                BtnFilterEnabled.Text = $"启用 ({EnabledCount})"
                BtnFilterEnabled.Visibility = If(IsWorld, Visibility.Collapsed, Visibility.Visible)
            End If
            If BtnFilterDisabled IsNot Nothing Then
                BtnFilterDisabled.Text = $"禁用 ({DisabledCount})"
                BtnFilterDisabled.Visibility = If(IsWorld, Visibility.Collapsed, Visibility.Visible)
            End If
            ' 应用启用/禁用筛选
            Dim Entries As List(Of String) = AllEntries
            If Not IsWorld Then
                If FilterMode = 1 Then
                    Entries = AllEntries.Where(Function(p) Not IsDisabledName(p)).ToList()
                ElseIf FilterMode = 2 Then
                    Entries = AllEntries.Where(Function(p) IsDisabledName(p)).ToList()
                End If
            End If
            ' 应用搜索
            Dim Query As String = If(SearchBox Is Nothing, "", SearchBox.Text.Trim())
            If Not String.IsNullOrEmpty(Query) Then
                Entries = Entries.Where(Function(p) System.IO.Path.GetFileName(p).ContainsIgnoreCase(Query)).ToList()
            End If
            If Not Entries.Any Then
                Dim Tip As New TextBlock
                Tip.Text = If(String.IsNullOrEmpty(Query), "这里还是空的。可以把文件放进对应文件夹，或点上方「从文件安装」。", $"没有匹配「{Query}」的项目。")
                Tip.Margin = New Thickness(10, 8, 10, 8)
                Tip.Opacity = 0.7
                Tip.TextWrapping = TextWrapping.Wrap
                PanList.Children.Add(Tip)
                Return
            End If
            For Each Entry As String In Entries
                PanList.Children.Add(BuildRow(Entry, IsWorld))
            Next
        Catch ex As Exception
            Logger.Warn(ex, "加载管理列表失败")
        End Try
    End Sub

    ''' <summary>构造一行：真实图标（禁用时右下角带禁用小标）+ 名称 + 操作按钮。</summary>
    Private Function BuildRow(Entry As String, IsWorld As Boolean) As FrameworkElement
        Const DisabledSuffix As String = ".disabled"
        Dim RawName As String = System.IO.Path.GetFileName(Entry)
        Dim IsEnabled As Boolean = Not RawName.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase)
        Dim DisplayName As String = If(IsEnabled, RawName, RawName.Substring(0, RawName.Length - DisabledSuffix.Length))
        Dim Row As New Grid With {.Height = 46, .Margin = New Thickness(2, 1, 2, 1), .ToolTip = Entry}
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
        Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
        ' 图标 + 禁用右下角小标
        Dim IconPanel As New Grid With {.Width = 38, .Height = 38, .Margin = New Thickness(0, 0, 10, 0), .VerticalAlignment = VerticalAlignment.Center}
        Dim ImgIcon As New Image With {.Stretch = Stretch.Uniform, .Source = LoadItemIcon(Entry, IsWorld)}
        RenderOptions.SetBitmapScalingMode(ImgIcon, BitmapScalingMode.HighQuality)
        IconPanel.Children.Add(ImgIcon)
        If Not IsEnabled Then
            Dim ImgOff As New Image With {.Width = 16, .Height = 16, .HorizontalAlignment = HorizontalAlignment.Right, .VerticalAlignment = VerticalAlignment.Bottom, .Margin = New Thickness(0, 0, -3, -3), .Source = New MyBitmap(PathImage & "Icons/Disabled.png"), .IsHitTestVisible = False}
            RenderOptions.SetBitmapScalingMode(ImgOff, BitmapScalingMode.HighQuality)
            IconPanel.Children.Add(ImgOff)
        End If
        Grid.SetColumn(IconPanel, 0)
        Row.Children.Add(IconPanel)
        ' 名称（禁用时删除线）
        Dim LabName As New TextBlock With {.Text = DisplayName, .VerticalAlignment = VerticalAlignment.Center, .TextTrimming = TextTrimming.CharacterEllipsis, .Margin = New Thickness(0, 0, 8, 0)}
        If Not IsEnabled Then LabName.TextDecorations = TextDecorations.Strikethrough
        Grid.SetColumn(LabName, 1)
        Row.Children.Add(LabName)
        ' 操作按钮
        Dim BtnPanel As New StackPanel With {.Orientation = Orientation.Horizontal, .VerticalAlignment = VerticalAlignment.Center, .HorizontalAlignment = HorizontalAlignment.Right}
        Dim OpenTarget As String = If(IsWorld, Entry, System.IO.Path.GetDirectoryName(Entry))
        Dim BtnOpen As New MyIconButton With {.Logo = Logo.IconButtonOpen, .ToolTip = "打开位置", .Height = 25, .Width = 25}
        AddHandler BtnOpen.Click, Sub(s As Object, e As EventArgs) OpenExplorer(OpenTarget)
        BtnPanel.Children.Add(BtnOpen)
        If IsWorld Then
            Dim DPEntry As String = Entry
            Dim BtnDP As New MyIconButton With {.Logo = IconDataPack, .ToolTip = "管理世界数据包", .Height = 25, .Width = 25}
            AddHandler BtnDP.Click, Sub(s As Object, e As EventArgs) EnterDatapack(DPEntry)
            BtnPanel.Children.Add(BtnDP)
        Else
            Dim EDEntry As String = Entry
            Dim BtnED As New MyIconButton With {.Logo = If(IsEnabled, Logo.IconButtonStop, Logo.IconButtonCheck), .ToolTip = If(IsEnabled, "禁用", "启用"), .Height = 25, .Width = 25}
            AddHandler BtnED.Click, Sub(s As Object, e As EventArgs) ToggleEntry(EDEntry)
            BtnPanel.Children.Add(BtnED)
        End If
        Dim DelEntry As String = Entry
        Dim DelWorld As Boolean = IsWorld
        Dim BtnDel As New MyIconButton With {.Logo = Logo.IconButtonDelete, .ToolTip = "删除", .Height = 25, .Width = 25}
        AddHandler BtnDel.Click, Sub(s As Object, e As EventArgs) DeleteEntry(DelEntry, DelWorld)
        BtnPanel.Children.Add(BtnDel)
        Grid.SetColumn(BtnPanel, 2)
        Row.Children.Add(BtnPanel)
        Return Row
    End Function

    ''' <summary>读取条目图标：世界读 icon.png；压缩包读 pack.png；否则 NoIcon（带缓存）。</summary>
    Private Function LoadItemIcon(Entry As String, IsWorld As Boolean) As ImageSource
        If IconCache.ContainsKey(Entry) Then Return IconCache(Entry)
        Dim Result As ImageSource = Nothing
        Try
            If IsWorld Then
                Dim IconFile As String = System.IO.Path.Combine(Entry, "icon.png")
                If System.IO.File.Exists(IconFile) Then
                    Result = LoadBitmapFromBytes(System.IO.File.ReadAllBytes(IconFile))
                End If
            Else
                ' ZipArchiveEntry.Open() 返回不可定位流，BitmapImage 直接解码常失败；先读成字节再解码
                Using Archive = FileUtils.OpenZip(PathUtils.ForApi(Entry))
                    Dim PackEntry = Archive.Entries.FirstOrDefault(Function(x) x.FullName.Equals("pack.png", StringComparison.OrdinalIgnoreCase))
                    If PackEntry IsNot Nothing Then
                        Using S = PackEntry.Open()
                            Using Ms As New MemoryStream
                                S.CopyTo(Ms)
                                Result = LoadBitmapFromBytes(Ms.ToArray())
                            End Using
                        End Using
                    End If
                End Using
            End If
        Catch ex As Exception
            Logger.Warn(ex, $"读取图标失败：{Entry}")
        End Try
        If Result Is Nothing Then Result = New MyBitmap(PathImage & "Icons/NoIcon.png")
        IconCache(Entry) = Result
        Return Result
    End Function

    ''' <summary>由字节解码位图（用可定位内存流，避免 zip 流不可定位导致解码失败）。</summary>
    Private Function LoadBitmapFromBytes(Bytes As Byte()) As ImageSource
        If Bytes Is Nothing OrElse Bytes.Length = 0 Then Return Nothing
        Dim Bmp As New System.Windows.Media.Imaging.BitmapImage
        Bmp.BeginInit()
        Bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad
        Bmp.StreamSource = New MemoryStream(Bytes)
        Bmp.EndInit()
        Bmp.Freeze()
        Return Bmp
    End Function

    Private Sub ToggleEntry(Entry As String)
        Try
            Const DisabledSuffix As String = ".disabled"
            If Entry.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase) Then
                FileUtils.Move(Entry, Entry.Substring(0, Entry.Length - DisabledSuffix.Length))
            Else
                FileUtils.Move(Entry, Entry & DisabledSuffix)
            End If
            ReloadForKind()
        Catch ex As Exception
            Logger.Error(ex, "切换启用状态失败", LogBehavior.Toast)
        End Try
    End Sub

    Private Sub DeleteEntry(Entry As String, IsWorld As Boolean)
        Try
            If MyMsgBox($"确定要删除「{System.IO.Path.GetFileName(Entry)}」吗？此操作不可恢复！", "删除确认", "删除", "取消", IsWarn:=True) <> 1 Then Return
            If IsWorld Then
                System.IO.Directory.Delete(Entry, True)
            Else
                FileUtils.Delete(Entry)
            End If
            ReloadForKind()
        Catch ex As Exception
            Logger.Error(ex, "删除失败", LogBehavior.Toast)
        End Try
    End Sub

    Private Sub BtnOpen_Click(sender As Object, e As EventArgs) Handles BtnOpen.Click
        Dim Folder As String = GetFolder()
        If Not String.IsNullOrEmpty(Folder) Then OpenExplorer(Folder)
    End Sub

    Private Sub BtnRefresh_Click(sender As Object, e As EventArgs) Handles BtnRefresh.Click
        ReloadForKind()
    End Sub

    Private Sub BtnInstall_Click(sender As Object, e As EventArgs) Handles BtnInstall.Click
        Try
            Dim Folder As String = GetFolder()
            If String.IsNullOrEmpty(Folder) Then Return
            Dim Files As List(Of String) = Dialogs.SelectFile("选择要安装的文件", True)
            If Files Is Nothing OrElse Not Files.Any() Then Return
            DirectoryUtils.Create(Folder)
            For Each F As String In Files
                FileUtils.Copy(F, System.IO.Path.Combine(Folder, System.IO.Path.GetFileName(F)))
            Next
            ReloadForKind()
            Hint("已安装所选文件", HintType.Green)
        Catch ex As Exception
            Logger.Error(ex, "安装文件失败", LogBehavior.Toast)
        End Try
    End Sub

    ''' <summary>进入某世界的数据包管理。</summary>
    Private Sub EnterDatapack(WorldPath As String)
        SelectedWorld = WorldPath
        Kind = FileKind.Datapack
        ReloadForKind()
    End Sub

    Private Sub BtnBack_Click(sender As Object, e As EventArgs) Handles BtnBack.Click
        Kind = FileKind.World
        SelectedWorld = ""
        ReloadForKind()
    End Sub

    Private Sub SearchBox_TextChanged(sender As Object, e As EventArgs) Handles SearchBox.TextChanged
        ReloadForKind()
    End Sub

    Private Sub BtnFilter_Check(sender As MyRadioButton, raiseByMouse As Boolean) Handles BtnFilterAll.Check, BtnFilterEnabled.Check, BtnFilterDisabled.Check
        If sender Is Nothing OrElse sender.Tag Is Nothing Then Return
        FilterMode = CInt(sender.Tag)
        ReloadForKind()
    End Sub

    ''' <summary>文件名是否带 .disabled 后缀。</summary>
    Private Function IsDisabledName(FilePath As String) As Boolean
        Return System.IO.Path.GetFileName(FilePath).EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
    End Function

End Class
