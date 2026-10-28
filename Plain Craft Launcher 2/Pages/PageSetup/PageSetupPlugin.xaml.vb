''' <summary>
''' 设置 → 插件：查看、启用/禁用、删除已安装插件。
''' </summary>
Public Class PageSetupPlugin

    Private Sub PageSetupPlugin_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        RefreshList()
    End Sub

    ''' <summary>
    ''' 重新加载插件并刷新列表。
    ''' </summary>
    Private Sub RefreshList()
        Try
            PanPluginList.Children.Clear()
            Dim AllPlugins As List(Of ModPlugin.PluginDefinition) = ModPlugin.LoadAll()
            If Not AllPlugins.Any() Then
                Dim EmptyHint As New TextBlock
                EmptyHint.Text = "还没有安装任何插件。把插件文件放进插件目录后点「重新加载」即可。"
                EmptyHint.Opacity = 0.7
                EmptyHint.TextWrapping = TextWrapping.Wrap
                PanPluginList.Children.Add(EmptyHint)
                Return
            End If
            For Each Plugin In AllPlugins
                Dim Row As New Grid With {.Margin = New Thickness(0, 8, 0, 0)}
                Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = New GridLength(1, GridUnitType.Star)})
                Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
                Row.ColumnDefinitions.Add(New ColumnDefinition With {.Width = GridLength.Auto})
                '名称与类型
                Dim InfoText As New TextBlock
                InfoText.Text = Plugin.Name & "  v" & Plugin.Version & If(Plugin.IsBuiltIn, "（内置）", If(Plugin.IsPython, "（Python）", "（声明式）"))
                InfoText.TextTrimming = TextTrimming.CharacterEllipsis
                InfoText.VerticalAlignment = VerticalAlignment.Center
                Grid.SetColumn(InfoText, 0)
                Row.Children.Add(InfoText)
                '启用开关
                Dim EnabledCheck As New MyCheckBox With {.Text = "启用", .Checked = Not ModPlugin.IsDisabled(Plugin.Name), .VerticalAlignment = VerticalAlignment.Center, .Margin = New Thickness(12, 0, 12, 0)}
                Grid.SetColumn(EnabledCheck, 1)
                Row.Children.Add(EnabledCheck)
                '删除按钮（内置插件不可删除）
                Dim BtnRemove As MyButton = Nothing
                If Not Plugin.IsBuiltIn Then
                    BtnRemove = New MyButton With {.Text = "删除", .Height = 26, .VerticalAlignment = VerticalAlignment.Center}
                    Grid.SetColumn(BtnRemove, 2)
                    Row.Children.Add(BtnRemove)
                End If
                '事件
                Dim CapturedName As String = Plugin.Name
                Dim CapturedCheck As MyCheckBox = EnabledCheck
                AddHandler EnabledCheck.Change, Sub(s, user)
                    If Not user Then Return
                    ModPlugin.SetDisabled(CapturedName, Not CapturedCheck.Checked)
                    Hint(If(CapturedCheck.Checked, "已启用插件：" & CapturedName, "已禁用插件：" & CapturedName), HintType.Blue)
                End Sub
                If BtnRemove IsNot Nothing Then
                    Dim CapturedPlugin = Plugin
                    AddHandler BtnRemove.Click, Sub(s, e) DeletePlugin(CapturedPlugin)
                End If
                PanPluginList.Children.Add(Row)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "刷新插件列表失败")
        End Try
    End Sub

    ''' <summary>
    ''' 删除插件及其解压目录。
    ''' </summary>
    Private Sub DeletePlugin(Plugin As ModPlugin.PluginDefinition)
        If Plugin Is Nothing OrElse Plugin.IsBuiltIn Then Return
        If MyMsgBox($"确定要删除插件「{Plugin.Name}」吗？{vbCrLf}插件文件以及该插件解压出的目录都会被删除。",
                    "删除插件", "删除", "取消", IsWarn:=True) <> 1 Then Return
        Try
            If Not String.IsNullOrEmpty(Plugin.SourcePath) AndAlso FileUtils.Exists(Plugin.SourcePath) Then
                FileUtils.Delete(Plugin.SourcePath)
            End If
            If Not String.IsNullOrEmpty(Plugin.Folder) AndAlso DirectoryUtils.Exists(Plugin.Folder) Then
                DirectoryUtils.Delete(Plugin.Folder)
            End If
            ModPlugin.SetDisabled(Plugin.Name, False)
            Hint("已删除插件：" & Plugin.Name, HintType.Green)
            RefreshList()
        Catch ex As Exception
            Logger.Error(ex, "删除插件失败", LogBehavior.Alert)
        End Try
    End Sub

    ''' <summary>重新加载。</summary>
    Private Sub BtnReload_Click(sender As Object, e As EventArgs) Handles BtnReload.Click
        RefreshList()
        Hint("插件列表已刷新", HintType.Green)
    End Sub

    ''' <summary>打开插件目录。</summary>
    Private Sub BtnOpenFolder_Click(sender As Object, e As EventArgs) Handles BtnOpenFolder.Click
        Try
            DirectoryUtils.Create(ModPlugin.PluginFolder)
            OpenExplorer(ModPlugin.PluginFolder)
        Catch ex As Exception
            Logger.Warn(ex, "打开插件目录失败")
        End Try
    End Sub

End Class
