''' <summary>
''' 插件接口层（ABI v1）。
''' 插件的编写语言与启动器一致，均为 VB.NET：编译为 plugin.dll 后放进 PCL\Plugins\ 即可被加载。
''' 本文件中的所有签名只使用 System.* 类型，插件工程只需引用 启动器 exe 就能编译，无需引用 WPF 等其它程序集。
'''
''' 最小插件示例：
''' <code>
''' Imports PCL.PluginApi
'''
''' Public Class HelloPlugin
'''     Inherits PluginBase
'''
'''     Public Overrides ReadOnly Property Name As String
'''         Get
'''             Return "示例插件"
'''         End Get
'''     End Property
'''
'''     Public Overrides ReadOnly Property Description As String
'''         Get
'''             Return "演示用插件"
'''         End Get
'''     End Property
'''
'''     Public Overrides Function GetActions() As IEnumerable(Of PluginAction)
'''         Return New PluginAction() {New PluginAction("hello", "打个招呼")}
'''     End Function
'''
'''     Public Overrides Sub Execute(ActionId As String)
'''         Host.Hint("你好，世界！")
'''     End Sub
''' End Class
''' </code>
''' </summary>
Namespace PluginApi

    ''' <summary>接口层版本等信息。</summary>
    Public NotInheritable Class PluginInfo
        ''' <summary>接口层版本。插件可在 Load 时检查，做兼容处理。</summary>
        Public Const ApiVersion As Integer = 1
        Private Sub New()
        End Sub
    End Class

    ''' <summary>日志级别。</summary>
    Public Enum PluginLogLevel
        ''' <summary>普通信息。</summary>
        Info = 0
        ''' <summary>警告。</summary>
        Warning = 1
        ''' <summary>错误。</summary>
        [Error] = 2
    End Enum

    ''' <summary>
    ''' 插件提供的按钮。
    ''' </summary>
    Public Class PluginAction

        ''' <summary>动作标识，点击按钮时会原样传给 <see cref="IPlugin.Execute"/>。</summary>
        Public Property Id As String = ""
        ''' <summary>按钮文字。</summary>
        Public Property Text As String = ""
        ''' <summary>鼠标悬停提示，可留空。</summary>
        Public Property ToolTip As String = ""
        ''' <summary>按钮样式：danger / highlight，可留空。</summary>
        Public Property Style As String = ""

        Public Sub New()
        End Sub

        Public Sub New(Id As String, Text As String, Optional ToolTip As String = "", Optional Style As String = "")
            Me.Id = Id
            Me.Text = Text
            Me.ToolTip = ToolTip
            Me.Style = Style
        End Sub

    End Class

    ''' <summary>
    ''' 启动器提供给插件的能力。所有方法都可以在后台线程调用，内部会自行切回 UI 线程。
    ''' </summary>
    Public Interface IPluginHost

        ''' <summary>当前插件名称。</summary>
        ReadOnly Property PluginName As String
        ''' <summary>当前插件所在目录（末尾带反斜杠）。</summary>
        ReadOnly Property PluginFolder As String
        ''' <summary>当前插件的数据目录，用于存放配置等文件，会自动创建；插件更新后不会丢失。</summary>
        ReadOnly Property DataFolder As String
        ''' <summary>启动器版本号，如 2.11.0。</summary>
        ReadOnly Property LauncherVersion As String
        ''' <summary>接口层版本，见 <see cref="PluginInfo.ApiVersion"/>。</summary>
        ReadOnly Property ApiVersion As Integer

        ''' <summary>写入启动器日志（前缀为插件名）。</summary>
        Sub Log(Text As String, Optional Level As PluginLogLevel = PluginLogLevel.Info)
        ''' <summary>在启动器右下角弹出浅色提示。</summary>
        Sub Hint(Text As String)
        ''' <summary>弹出对话框提示。</summary>
        Sub Alert(Text As String)
        ''' <summary>用默认浏览器打开网址。</summary>
        Sub OpenWebsite(Url As String)
        ''' <summary>在资源管理器中打开目录（不存在则创建）。</summary>
        Sub OpenFolder(Folder As String)

    End Interface

    ''' <summary>
    ''' 插件需要实现的接口。
    ''' 一般建议继承 <see cref="PluginBase"/>，而不是直接实现本接口。
    ''' </summary>
    Public Interface IPlugin

        ''' <summary>插件显示名称，需唯一。</summary>
        ReadOnly Property Name As String
        ''' <summary>版本号，如 1.0.0。</summary>
        ReadOnly Property Version As String
        ''' <summary>作者。</summary>
        ReadOnly Property Author As String
        ''' <summary>一句话说明，显示在插件卡片上。</summary>
        ReadOnly Property Description As String

        ''' <summary>返回插件提供的按钮，没有则返回空集合。</summary>
        Function GetActions() As IEnumerable(Of PluginAction)

        ''' <summary>插件被加载时调用一次（插件被禁用时不会调用）。</summary>
        Sub Load(Host As IPluginHost)

        ''' <summary>按钮被点击时调用，参数为 <see cref="PluginAction.Id"/>。运行在后台线程。</summary>
        Sub Execute(ActionId As String)

    End Interface

    ''' <summary>
    ''' 可选接口：实现后可以收到游戏启动/退出事件，均在后台线程触发。
    ''' </summary>
    Public Interface IPluginGameEvent

        ''' <summary>游戏已启动（窗口已出现或启动完成）。</summary>
        Sub OnGameStarted(InstanceName As String)
        ''' <summary>游戏已退出（含崩溃）。</summary>
        Sub OnGameExited(InstanceName As String)

    End Interface

    ''' <summary>
    ''' 插件基类，提供默认实现，推荐继承后按需重写。
    ''' </summary>
    Public Class PluginBase
        Implements IPlugin

        ''' <summary>宿主对象，Load 之后可用。</summary>
        Protected Property Host As IPluginHost

        ''' <summary>插件显示名称，默认取类名。</summary>
        Public Overridable ReadOnly Property Name As String Implements IPlugin.Name
            Get
                Return Me.GetType().Name
            End Get
        End Property

        ''' <summary>版本号，默认 1.0.0。</summary>
        Public Overridable ReadOnly Property Version As String Implements IPlugin.Version
            Get
                Return "1.0.0"
            End Get
        End Property

        ''' <summary>作者。</summary>
        Public Overridable ReadOnly Property Author As String Implements IPlugin.Author
            Get
                Return ""
            End Get
        End Property

        ''' <summary>一句话说明。</summary>
        Public Overridable ReadOnly Property Description As String Implements IPlugin.Description
            Get
                Return ""
            End Get
        End Property

        ''' <summary>返回插件按钮，默认没有。</summary>
        Public Overridable Function GetActions() As IEnumerable(Of PluginAction) Implements IPlugin.GetActions
            Return New PluginAction() {}
        End Function

        ''' <summary>加载时调用，默认只保存宿主。</summary>
        Public Overridable Sub Load(Host As IPluginHost) Implements IPlugin.Load
            Me.Host = Host
        End Sub

        ''' <summary>按钮点击时调用，默认不做任何事。</summary>
        Public Overridable Sub Execute(ActionId As String) Implements IPlugin.Execute
        End Sub

        ''' <summary>写入日志的快捷方式。</summary>
        Protected Sub Log(Text As String, Optional Level As PluginLogLevel = PluginLogLevel.Info)
            If Host IsNot Nothing Then Host.Log(Text, Level)
        End Sub

    End Class

End Namespace
