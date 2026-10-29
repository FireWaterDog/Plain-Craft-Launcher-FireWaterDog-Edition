Imports System
Imports System.Collections.Generic
Imports PCL.PluginApi

''' <summary>
''' 示例插件：演示按钮、宿主能力与游戏事件的用法。
''' 构建：grand install grand\samples\HelloPlugin --pcl "路径\Plain Craft Launcher 2.exe"
''' </summary>
Public Class HelloPlugin
    Inherits PluginBase
    Implements IPluginGameEvent

    Public Overrides ReadOnly Property Name As String
        Get
            Return "示例插件"
        End Get
    End Property

    Public Overrides ReadOnly Property Version As String
        Get
            Return "1.0.0"
        End Get
    End Property

    Public Overrides ReadOnly Property Author As String
        Get
            Return "PCLFE"
        End Get
    End Property

    Public Overrides ReadOnly Property Description As String
        Get
            Return "演示 VB.NET 插件的基本写法：按钮、宿主能力与游戏事件。"
        End Get
    End Property

    Public Overrides Function GetActions() As IEnumerable(Of PluginAction)
        Return New PluginAction() {
            New PluginAction("hello", "打个招呼", "弹出提示并写入日志"),
            New PluginAction("data", "打开数据目录", "插件自己的数据目录，更新插件不会丢失"),
            New PluginAction("count", "读取点击次数", "把点击次数存在数据目录里"),
            New PluginAction("home", "打开启动器官网")
        }
    End Function

    Public Overrides Sub Execute(ActionId As String)
        Select Case ActionId
            Case "hello"
                Host.Hint("你好，这是来自 VB.NET 插件的问候！")
                Host.Log("已执行 hello")
            Case "data"
                Host.OpenFolder(Host.DataFolder)
            Case "count"
                Dim CounterFile As String = IO.Path.Combine(Host.DataFolder, "count.txt")
                Dim Count As Integer = 0
                Try
                    If IO.File.Exists(CounterFile) Then Integer.TryParse(IO.File.ReadAllText(CounterFile).Trim(), Count)
                Catch
                End Try
                Count += 1
                Try
                    IO.File.WriteAllText(CounterFile, Count.ToString())
                Catch ex As Exception
                    Host.Log("写入计数失败：" & ex.Message, PluginLogLevel.Warning)
                End Try
                Host.Alert("这个按钮已经被点击了 " & Count & " 次。")
            Case "home"
                Host.OpenWebsite("https://github.com/FireWaterDog/Plain-Craft-Launcher-FireWaterDog-Edition")
            Case Else
                Host.Log("未知动作：" & ActionId, PluginLogLevel.Warning)
        End Select
    End Sub

    Public Sub OnGameStarted(InstanceName As String) Implements IPluginGameEvent.OnGameStarted
        Host.Log("游戏已启动：" & InstanceName)
    End Sub

    Public Sub OnGameExited(InstanceName As String) Implements IPluginGameEvent.OnGameExited
        Host.Log("游戏已退出：" & InstanceName)
    End Sub

End Class
