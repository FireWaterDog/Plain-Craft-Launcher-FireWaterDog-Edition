# grand —— PCLFE 插件构建工具

插件与启动器使用同一种语言：**VB.NET**。插件编译为 `plugin.dll`，放进启动器的 `PCL\Plugins\` 即可被加载，无需 Python 运行时。

## 编译工具本身

双击 `build.bat`，或命令行执行：

```
build.bat
```

生成 `grand.exe`（只依赖 .NET Framework 4.x，Windows 自带）。

## 用法

```
grand new <名称> [--dir 输出目录]                 创建插件工程
grand build [工程目录] [--pcl 启动器exe路径]       编译为 bin\plugin.dll
grand pack [工程目录] [--pcl 启动器exe路径]        打包为 <名称>.pclfePlugin
grand install [工程目录] [--pcl 启动器exe路径]     编译 + 打包 + 安装到 PCL\Plugins\
```

示例：

```
grand new HelloPlugin --dir D:\Plugins
grand install D:\Plugins\HelloPlugin --pcl "C:\PCL\Plain Craft Launcher 2.exe"
```

找不到启动器时，也可以在工程目录放一个 `grand.json`：

```json
{ "pcl": "..\\Plain Craft Launcher 2\\bin\\Debug\\Plain Craft Launcher 2.exe" }
```

## 编写插件

插件类继承 `PCL.PluginApi.PluginBase`：

```vb
Imports System.Collections.Generic
Imports PCL.PluginApi

Public Class HelloPlugin
    Inherits PluginBase

    Public Overrides ReadOnly Property Name As String
        Get
            Return "示例插件"
        End Get
    End Property

    Public Overrides Function GetActions() As IEnumerable(Of PluginAction)
        Return New PluginAction() {New PluginAction("hello", "打个招呼")}
    End Function

    Public Overrides Sub Execute(ActionId As String)
        If ActionId = "hello" Then Host.Hint("你好，世界！")
    End Sub
End Class
```

宿主能力（`Host`）：

| 成员 | 说明 |
| --- | --- |
| `Hint(Text)` | 右下角提示 |
| `Alert(Text)` | 弹窗提示 |
| `Log(Text, Level)` | 写入启动器日志，自动加插件名前缀 |
| `OpenWebsite(Url)` | 打开网址 |
| `OpenFolder(Folder)` | 打开目录（不存在会创建） |
| `DataFolder` | 插件数据目录，插件更新后仍保留 |
| `LauncherVersion` / `ApiVersion` | 启动器版本 / 接口层版本 |

想响应游戏启动与退出，再实现 `PCL.PluginApi.IPluginGameEvent`：

```vb
Public Class MyPlugin
    Inherits PluginBase
    Implements IPluginGameEvent

    Public Sub OnGameStarted(InstanceName As String) Implements IPluginGameEvent.OnGameStarted
        Host.Log("游戏已启动：" & InstanceName)
    End Sub

    Public Sub OnGameExited(InstanceName As String) Implements IPluginGameEvent.OnGameExited
        Host.Log("游戏已退出：" & InstanceName)
    End Sub
End Class
```

插件运行在后台线程，界面操作请通过 `Host` 提供的方法（内部会自动切回 UI 线程）。

## 插件包结构

`.pclfePlugin` 本质是 zip：

```
plugin.json     清单：name / version / author / description / entry
plugin.dll      编译产物
```

启动器解压到 `PCL\Plugins\<包名>\` 后加载；`plugin.dll` 以字节方式读取，不会占用文件，因此可以直接覆盖更新或删除。

## 兼容性

启动器同时保留对旧格式的支持：

- `.pclfePlugin` 内含 `main.py` → 旧版 Python 插件（需本机装 Python）
- 直接放在 `PCL\Plugins\` 的 `.json` → 声明式按钮插件（url / folder / command / file / hint）

设置 → 插件 中会标注每个插件的类型。
