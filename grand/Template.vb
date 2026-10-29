''' <summary>
''' grand new 使用的工程模板。
''' </summary>
Friend Module Template

    ''' <summary>插件清单。</summary>
    Public Function Manifest(Name As String) As String
        Return String.Join(vbCrLf, New String() {
            "{",
            "  ""name"": """ & Name & """,",
            "  ""version"": ""1.0.0"",",
            "  ""author"": ""你的名字"",",
            "  ""description"": ""一句话说明这个插件做什么。"",",
            "  ""entry"": ""plugin.dll""",
            "}",
            ""
        })
    End Function

    ''' <summary>插件源码模板。</summary>
    Public Function PluginSource() As String
        Return String.Join(vbCrLf, New String() {
            "Imports System",
            "Imports System.Collections.Generic",
            "Imports PCL.PluginApi",
            "",
            "''' <summary>",
            "''' 插件入口。类名随意，但需要有公共无参构造函数。",
            "''' 继承 PluginBase 后，按需重写 Name / Description / GetActions / Execute 即可。",
            "''' </summary>",
            "Public Class MyPlugin",
            "    Inherits PluginBase",
            "",
            "    Public Overrides ReadOnly Property Name As String",
            "        Get",
            "            Return ""我的插件""",
            "        End Get",
            "    End Property",
            "",
            "    Public Overrides ReadOnly Property Version As String",
            "        Get",
            "            Return ""1.0.0""",
            "        End Get",
            "    End Property",
            "",
            "    Public Overrides ReadOnly Property Author As String",
            "        Get",
            "            Return ""你的名字""",
            "        End Get",
            "    End Property",
            "",
            "    Public Overrides ReadOnly Property Description As String",
            "        Get",
            "            Return ""一句话说明这个插件做什么。""",
            "        End Get",
            "    End Property",
            "",
            "    ''' <summary>返回显示在插件卡片上的按钮。</summary>",
            "    Public Overrides Function GetActions() As IEnumerable(Of PluginAction)",
            "        Return New PluginAction() {",
            "            New PluginAction(""hello"", ""打个招呼"", ""点击后弹出提示""),",
            "            New PluginAction(""openData"", ""打开数据目录"")",
            "        }",
            "    End Function",
            "",
            "    ''' <summary>按钮点击时调用，运行在后台线程。</summary>",
            "    Public Overrides Sub Execute(ActionId As String)",
            "        Select Case ActionId",
            "            Case ""hello""",
            "                Host.Hint(""你好，这是来自 VB.NET 插件的问候！"")",
            "                Host.Log(""已执行 hello"")",
            "            Case ""openData""",
            "                Host.OpenFolder(Host.DataFolder)",
            "        End Select",
            "    End Sub",
            "",
            "End Class",
            ""
        })
    End Function

    ''' <summary>工程内的说明文件。</summary>
    Public Function ProjectReadme(Name As String) As String
        Return String.Join(vbCrLf, New String() {
            "# " & Name,
            "",
            "PCLFE 插件工程（VB.NET）。",
            "",
            "## 构建",
            "",
            "```",
            "grand build .      # 编译为 bin\plugin.dll",
            "grand pack .       # 打包为 " & Name & ".pclfePlugin",
            "grand install .    # 编译 + 打包 + 安装到启动器",
            "```",
            "",
            "若找不到启动器，可加 `--pcl ""路径\Plain Craft Launcher 2.exe""`，",
            "或在工程目录放置 `grand.json`：",
            "",
            "```json",
            "{ ""pcl"": ""..\\Plain Craft Launcher 2\\bin\\Debug\\Plain Craft Launcher 2.exe"" }",
            "```",
            "",
            "## 编写",
            "",
            "- 插件类继承 `PCL.PluginApi.PluginBase`，按需重写属性与方法。",
            "- `GetActions` 返回卡片按钮，`Execute(ActionId)` 处理点击。",
            "- 通过 `Host` 使用宿主能力：`Hint` / `Alert` / `Log` / `OpenWebsite` / `OpenFolder` / `DataFolder`。",
            "- 想接收游戏启动/退出事件，另外实现 `PCL.PluginApi.IPluginGameEvent`。",
            "- 插件运行在后台线程，操作界面请通过 `Host` 提供的方法。",
            ""
        })
    End Function

End Module
