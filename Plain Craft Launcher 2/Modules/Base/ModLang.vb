Imports System.Globalization
Imports System.Runtime.CompilerServices
Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows.Threading

''' <summary>
''' 多语言支持：语言选择、跟随 Windows 系统语言、界面文本翻译。
''' 
''' 实现方式是「按原文查表」：以简体中文原文为键，命中则替换为目标语言文案，
''' 未命中的文本保持原文，因此可以随时向词典里补充条目，而不会影响任何已有功能。
''' 翻译对象既包括控件自身的文本属性（MyCard.Title、MyButton.Text 等，折叠内容也能覆盖），
''' 也包括可视化树中已经渲染出来的 TextBlock，无需逐处修改 XAML 与代码里的原始文案。
''' </summary>
Public Module ModLang

#Region "语言定义"

    ''' <summary>
    ''' 界面语言。
    ''' 数值与设置项 Language 直接对应，也与设置页下拉框的选项顺序对应。
    ''' </summary>
    Public Enum LanguageType
        ''' <summary>跟随 Windows 系统语言。</summary>
        FollowSystem = 0
        ''' <summary>简体中文（原文）。</summary>
        SimplifiedChinese = 1
        ''' <summary>繁體中文。</summary>
        TraditionalChinese = 2
        ''' <summary>English。</summary>
        English = 3
        ''' <summary>日本語。</summary>
        Japanese = 4
        ''' <summary>한국어。</summary>
        Korean = 5
    End Enum

    ''' <summary>
    ''' 设置项中保存的语言选择。
    ''' </summary>
    Public ReadOnly Property LanguageSelected As LanguageType
        Get
            Try
                Dim Raw As Integer = Settings.Get(Of Integer)("Language")
                If Raw < 0 OrElse Raw > CInt(LanguageType.Korean) Then Return LanguageType.FollowSystem
                Return CType(Raw, LanguageType)
            Catch
                Return LanguageType.FollowSystem
            End Try
        End Get
    End Property

    ''' <summary>
    ''' 实际生效的语言（已解析「跟随系统」）。
    ''' </summary>
    Public ReadOnly Property LanguageEffective As LanguageType
        Get
            If LanguageSelected = LanguageType.FollowSystem Then Return LanguageFromSystem()
            Return LanguageSelected
        End Get
    End Property

    ''' <summary>
    ''' 根据 Windows 系统语言推断界面语言。
    ''' zh-Hans → 简体中文；zh-Hant / zh-TW / zh-HK / zh-MO → 繁體中文；ja → 日本語；ko → 한국어；
    ''' en 及其他语言 → English（对非中文用户更友好，需要中文可手动切换）。
    ''' </summary>
    Public Function LanguageFromSystem() As LanguageType
        Try
            Dim Name As String = If(CultureInfo.CurrentUICulture.Name, "").ToLowerInvariant()
            If Name.StartsWith("zh") Then
                If Name.Contains("hant") OrElse Name.Contains("tw") OrElse Name.Contains("hk") OrElse Name.Contains("mo") Then
                    Return LanguageType.TraditionalChinese
                End If
                Return LanguageType.SimplifiedChinese
            End If
            If Name.StartsWith("ja") Then Return LanguageType.Japanese
            If Name.StartsWith("ko") Then Return LanguageType.Korean
            If Name.StartsWith("en") Then Return LanguageType.English
            '简体中文是原文，因此其他语言一律回退英文，避免出现看不懂的界面
            Return LanguageType.English
        Catch
            Return LanguageType.SimplifiedChinese
        End Try
    End Function

    ''' <summary>
    ''' 当前生效语言的显示名称（用该语言自身书写）。
    ''' </summary>
    Public ReadOnly Property LanguageDisplayName As String
        Get
            Select Case LanguageEffective
                Case LanguageType.TraditionalChinese : Return "繁體中文"
                Case LanguageType.English : Return "English"
                Case LanguageType.Japanese : Return "日本語"
                Case LanguageType.Korean : Return "한국어"
                Case Else : Return "简体中文"
            End Select
        End Get
    End Property

    ''' <summary>
    ''' 当前语言的 BCP-47 标记，如 zh-CN。
    ''' </summary>
    Public ReadOnly Property LanguageCode As String
        Get
            Select Case LanguageEffective
                Case LanguageType.TraditionalChinese : Return "zh-TW"
                Case LanguageType.English : Return "en-US"
                Case LanguageType.Japanese : Return "ja-JP"
                Case LanguageType.Korean : Return "ko-KR"
                Case Else : Return "zh-CN"
            End Select
        End Get
    End Property

#End Region

#Region "翻译查表"

    Private ReadOnly TranslationLock As New Object()
    Private _TranslationTable As Dictionary(Of String, String) = Nothing
    Private _TranslationTableLanguage As LanguageType = LanguageType.SimplifiedChinese

    ''' <summary>
    ''' 当前语言对应的翻译词典；简体中文返回 Nothing（原文无需翻译）。
    ''' </summary>
    Private ReadOnly Property TranslationTable As Dictionary(Of String, String)
        Get
            Dim Target As LanguageType = LanguageEffective
            If Target = LanguageType.SimplifiedChinese Then Return Nothing
            SyncLock TranslationLock
                If _TranslationTable IsNot Nothing AndAlso _TranslationTableLanguage = Target Then Return _TranslationTable
                _TranslationTable = LangGetTranslationTable(Target)
                _TranslationTableLanguage = Target
                Return _TranslationTable
            End SyncLock
        End Get
    End Property

    ''' <summary>
    ''' 翻译单条文本：命中词典则返回译文，否则原样返回。
    ''' 会尝试去掉首尾空白后再匹配，并保留原有空白。
    ''' </summary>
    Public Function L(Text As String) As String
        If String.IsNullOrEmpty(Text) Then Return Text
        Dim Table As Dictionary(Of String, String) = TranslationTable
        If Table Is Nothing Then Return Text
        Dim Result As String = Nothing
        If Table.TryGetValue(Text, Result) Then Return Result
        '容错：忽略首尾空白
        Dim Trimmed As String = Text.Trim()
        If Trimmed.Length = 0 OrElse Trimmed = Text Then Return Text
        If Table.TryGetValue(Trimmed, Result) Then
            Dim StartIndex As Integer = Text.IndexOf(Trimmed, StringComparison.Ordinal)
            If StartIndex >= 0 Then
                Return Text.Substring(0, StartIndex) & Result & Text.Substring(StartIndex + Trimmed.Length)
            End If
        End If
        Return Text
    End Function

#End Region

#Region "界面翻译"

    ''' <summary>
    ''' 记录每个控件各文本槽位首次出现的原文，用于切换语言时重新翻译（切回简体中文即可还原）。
    ''' </summary>
    Private NotInheritable Class LangOriginalValue
        Public ReadOnly Originals As New Dictionary(Of String, String)
        Public ReadOnly Applied As New Dictionary(Of String, String)
    End Class

    Private ReadOnly LangOriginals As New ConditionalWeakTable(Of DependencyObject, LangOriginalValue)

    Private Function GetOriginalValue(Target As DependencyObject) As LangOriginalValue
        Dim Value As LangOriginalValue = Nothing
        If LangOriginals.TryGetValue(Target, Value) Then Return Value
        Value = New LangOriginalValue()
        LangOriginals.Add(Target, Value)
        Return Value
    End Function

    ''' <summary>
    ''' 计算某个文本槽位应该显示的内容：以原文为基准翻译，控件被程序改写文本时会自动更新原文。
    ''' </summary>
    Private Function TranslateSlot(Target As DependencyObject, Slot As String, Current As String) As String
        If Current Is Nothing Then Return Nothing
        Dim Store As LangOriginalValue = GetOriginalValue(Target)
        Dim Original As String = Nothing
        Dim Applied As String = Nothing
        If Store.Originals.TryGetValue(Slot, Original) Then
            Store.Applied.TryGetValue(Slot, Applied)
            If Current <> Original AndAlso Current <> Applied Then
                '控件文本被程序动态改写，把新内容当作原文重新记录
                Original = Current
                Store.Originals(Slot) = Current
                Store.Applied.Remove(Slot)
            End If
        Else
            Original = Current
            Store.Originals(Slot) = Current
        End If
        Dim Translated As String = L(Original)
        If Translated <> Current Then Store.Applied(Slot) = Translated
        Return Translated
    End Function

    ''' <summary>
    ''' 翻译指定可视化树中的所有文本。可以重复调用，结果幂等。
    ''' </summary>
    Public Sub LanguageApplyTo(Root As DependencyObject)
        If Root Is Nothing Then Return
        Try
            LanguageApplyInternal(Root, 0)
        Catch ex As Exception
            Logger.Warn(ex, "应用界面语言翻译失败")
        End Try
    End Sub

    Private Sub LanguageApplyInternal(Obj As DependencyObject, Depth As Integer)
        If Obj Is Nothing OrElse Depth > 64 Then Return
        'PCL 自定义控件的文本属性：直接改属性，未渲染与折叠状态下的内容也能覆盖
        Dim Card As MyCard = TryCast(Obj, MyCard)
        If Card IsNot Nothing AndAlso Card.Title IsNot Nothing Then
            Card.Title = TranslateSlot(Card, "Title", Card.Title)
        End If
        Dim ListItem As MyListItem = TryCast(Obj, MyListItem)
        If ListItem IsNot Nothing Then
            If ListItem.Title IsNot Nothing Then ListItem.Title = TranslateSlot(ListItem, "Title", ListItem.Title)
            If ListItem.Info IsNot Nothing Then ListItem.Info = TranslateSlot(ListItem, "Info", ListItem.Info)
        End If
        Dim HintControl As MyHint = TryCast(Obj, MyHint)
        If HintControl IsNot Nothing AndAlso HintControl.Text IsNot Nothing Then
            HintControl.Text = TranslateSlot(HintControl, "Text", HintControl.Text)
        End If
        Dim LoadingControl As MyLoading = TryCast(Obj, MyLoading)
        If LoadingControl IsNot Nothing AndAlso LoadingControl.Text IsNot Nothing Then
            LoadingControl.Text = TranslateSlot(LoadingControl, "Text", LoadingControl.Text)
        End If
        Dim TextBoxControl As MyTextBox = TryCast(Obj, MyTextBox)
        If TextBoxControl IsNot Nothing AndAlso TextBoxControl.HintText IsNot Nothing Then
            TextBoxControl.HintText = TranslateSlot(TextBoxControl, "HintText", TextBoxControl.HintText)
        End If
        Dim ButtonControl As MyButton = TryCast(Obj, MyButton)
        If ButtonControl IsNot Nothing AndAlso ButtonControl.Text IsNot Nothing Then
            ButtonControl.Text = TranslateSlot(ButtonControl, "Text", ButtonControl.Text)
        End If
        Dim CheckBoxControl As MyCheckBox = TryCast(Obj, MyCheckBox)
        If CheckBoxControl IsNot Nothing AndAlso CheckBoxControl.Text IsNot Nothing Then
            CheckBoxControl.Text = TranslateSlot(CheckBoxControl, "Text", CheckBoxControl.Text)
        End If
        Dim RadioControl As MyRadioButton = TryCast(Obj, MyRadioButton)
        If RadioControl IsNot Nothing AndAlso RadioControl.Text IsNot Nothing Then
            RadioControl.Text = TranslateSlot(RadioControl, "Text", RadioControl.Text)
        End If
        Dim ExtraTextControl As MyExtraTextButton = TryCast(Obj, MyExtraTextButton)
        If ExtraTextControl IsNot Nothing AndAlso ExtraTextControl.Text IsNot Nothing Then
            ExtraTextControl.Text = TranslateSlot(ExtraTextControl, "Text", ExtraTextControl.Text)
        End If
        '文本块
        Dim Block As TextBlock = TryCast(Obj, TextBlock)
        If Block IsNot Nothing AndAlso Block.Text IsNot Nothing Then
            Block.Text = TranslateSlot(Block, "Text", Block.Text)
        End If
        '按钮菜单项等把文本放在 Content 里的控件
        Dim Container As ContentControl = TryCast(Obj, ContentControl)
        If Container IsNot Nothing Then
            Dim ContentText As String = TryCast(Container.Content, String)
            If ContentText IsNot Nothing Then
                Container.Content = TranslateSlot(Container, "Content", ContentText)
            End If
        End If
        '控件提示
        Dim Element As FrameworkElement = TryCast(Obj, FrameworkElement)
        If Element IsNot Nothing Then
            Dim TipText As String = TryCast(Element.ToolTip, String)
            If TipText IsNot Nothing Then
                Element.ToolTip = TranslateSlot(Element, "Tip", TipText)
            End If
        End If
        '窗体标题
        Dim Form As Window = TryCast(Obj, Window)
        If Form IsNot Nothing AndAlso Form.Title IsNot Nothing Then
            Form.Title = TranslateSlot(Form, "WindowTitle", Form.Title)
        End If
        '子元素
        If TypeOf Obj Is Visual OrElse TypeOf Obj Is System.Windows.Media.Media3D.Visual3D Then
            Dim Count As Integer = VisualTreeHelper.GetChildrenCount(Obj)
            For i As Integer = 0 To Count - 1
                LanguageApplyInternal(VisualTreeHelper.GetChild(Obj, i), Depth + 1)
            Next
        End If
    End Sub

    Private ReadOnly LanguagePassRoots As New List(Of WeakReference)
    Private LanguagePassTimer As DispatcherTimer
    Private LanguagePassCount As Integer

    ''' <summary>
    ''' 翻译指定界面，并在随后的一段时间内自动重试若干次，
    ''' 以覆盖异步加载完成、稍后才出现的元素（例如版本列表、Mod 卡片内容）。
    ''' </summary>
    Public Sub LanguageApplyDeferred(Root As DependencyObject)
        If Root Is Nothing Then Return
        LanguageApplyTo(Root)
        SyncLock LanguagePassRoots
            LanguagePassRoots.Clear()
            LanguagePassRoots.Add(New WeakReference(Root))
        End SyncLock
        If LanguagePassTimer Is Nothing Then
            LanguagePassTimer = New DispatcherTimer(DispatcherPriority.Background) With {
                .Interval = New TimeSpan(0, 0, 0, 0, 400)
            }
            AddHandler LanguagePassTimer.Tick, AddressOf LanguagePassTick
        End If
        LanguagePassCount = 0
        LanguagePassTimer.Start()
    End Sub

    Private Sub LanguagePassTick(sender As Object, e As EventArgs)
        LanguagePassCount += 1
        SyncLock LanguagePassRoots
            For Each PassRoot As WeakReference In LanguagePassRoots
                If PassRoot.IsAlive Then LanguageApplyTo(TryCast(PassRoot.Target, DependencyObject))
            Next
        End SyncLock
        If LanguagePassCount >= 8 Then LanguagePassTimer.Stop()
    End Sub

    ''' <summary>
    ''' 切换语言后立即刷新整个主窗体。
    ''' </summary>
    Public Sub LanguageRefresh()
        Try
            Dim Root As Window = Application.Current?.MainWindow
            If Root Is Nothing Then Return
            LanguageApplyDeferred(Root)
        Catch ex As Exception
            Logger.Warn(ex, "刷新界面语言失败")
        End Try
    End Sub

    ''' <summary>
    ''' 设置项 Language 变化时的回调。
    ''' </summary>
    Public Sub LanguageOnSettingChanged(Value As Object)
        Try
            SyncLock TranslationLock
                _TranslationTable = Nothing '强制重新载入词典
            End SyncLock
            Logger.Info($"界面语言已切换为 {LanguageSelected}（生效：{LanguageDisplayName}）")
        Catch
        End Try
        RunInUi(Sub()
                    Try
                        LanguageRefresh()
                    Catch ex As Exception
                        Logger.Warn(ex, "应用界面语言失败")
                    End Try
                End Sub)
    End Sub

#End Region

End Module
