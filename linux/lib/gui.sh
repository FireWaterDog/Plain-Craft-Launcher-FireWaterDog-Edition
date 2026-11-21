#!/usr/bin/env sh
# 桌面环境下的提示与终端拉起。
# 从应用菜单启动时没有终端，错误信息必须弹窗或落盘，否则表现为“双击没反应”。

gui_tool() {
    first_of zenity kdialog xmessage
}

# 有图形会话且有可用对话框工具
gui_available() {
    [ -n "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ] || return 1
    gui_tool >/dev/null 2>&1
}

gui_log() {
    mkdir -p "$PCLFE_LOG_DIR" 2>/dev/null || return 0
    printf '[%s] %s\n%s\n\n' "$(timestamp)" "$1" "$2" >> "$PCLFE_LOG_DIR/gui.log" 2>/dev/null || true
}

# gui_error <标题> <正文>
gui_error() {
    _title=$1
    _body=$2
    gui_log "$_title" "$_body"
    if gui_available; then
        case "$(gui_tool)" in
            zenity)
                zenity --error --title="$_title" --text="$_body" --width=440 --no-markup 2>/dev/null && return 0
                ;;
            kdialog)
                kdialog --title "$_title" --error "$_body" 2>/dev/null && return 0
                ;;
            xmessage)
                xmessage -center -title "$_title" "$_body" 2>/dev/null && return 0
                ;;
        esac
    fi
    # 只装了 libnotify 的桌面：退化成通知（没有按钮，但至少有反馈）
    if [ -n "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ] && have notify-send; then
        notify-send -u critical -a PCLFE "$_title" "$_body" 2>/dev/null && return 0
    fi
    # 没有对话框工具：直接开终端把话说完，别让用户对着图标发呆
    if [ ! -t 1 ] && gui_show_in_terminal "$_title" "$_body" ""; then
        return 0
    fi
    # 没有图形工具时至少写到 stderr，并提示日志位置
    err "$_title：$_body"
    dim "详情已写入：$PCLFE_LOG_DIR/gui.log"
}

# gui_confirm <标题> <正文>：用户点“是”返回 0
gui_confirm() {
    _title=$1
    _body=$2
    gui_available || return 1
    case "$(gui_tool)" in
        zenity) zenity --question --title="$_title" --text="$_body" --width=440 --no-markup 2>/dev/null ;;
        kdialog) kdialog --title "$_title" --yesno "$_body" 2>/dev/null ;;
        *) return 1 ;;
    esac
}

# gui_open_terminal <要执行的命令>：在终端里跑，结束后等回车再关窗
gui_open_terminal() {
    _cmd=$1
    _hold="$_cmd; printf '\\n按回车键关闭此窗口…'; read _"
    for _t in x-terminal-emulator gnome-terminal konsole xfce4-terminal mate-terminal tilix deepin-terminal alacritty kitty xterm; do
        have "$_t" || continue
        case "$_t" in
            gnome-terminal)
                ( "$_t" -- bash -lc "$_hold" >/dev/null 2>&1 & ) ;;
            *)
                ( "$_t" -e bash -lc "$_hold" >/dev/null 2>&1 & ) ;;
        esac
        return 0
    done
    return 1
}

# 没有对话框工具时，把说明写进文件并在终端里显示，保证“双击”至少有反馈。
gui_show_in_terminal() {
    _title=$1
    _body=$2
    _action=${3:-}
    _msgfile="$PCLFE_LOG_DIR/last-message.txt"
    mkdir -p "$PCLFE_LOG_DIR" 2>/dev/null || true
    printf '%s\n\n%s\n' "$_title" "$_body" > "$_msgfile" 2>/dev/null || true
    if [ -n "$_action" ]; then
        gui_open_terminal "cat '$_msgfile'; echo; $_action"
    else
        gui_open_terminal "cat '$_msgfile'"
    fi
}

# 双击图标时的统一入口：未就绪就提示，并询问是否打开终端完成初始化
gui_first_run_help() {
    _reason=$1
    case "$_reason" in
        *启动器本体*)
            _body="$_reason

请先获取启动器本体，二选一：
  · 联网下载：pclfe update
  · 使用自带的安装包（-offline 压缩包 / 内置 exe 的 .deb）

也可以在终端里执行：
    pclfe update && pclfe run"
            _action="pclfe update && pclfe run"
            _title="PCLFE 缺少启动器本体"
            ;;
        *Wine*)
            _body="$_reason

Wine 是运行本启动器的前提，请先安装：
    sudo apt install wine winetricks cabextract unzip

装完执行：
    pclfe setup && pclfe run"
            _action="pclfe setup && pclfe run"
            _title="PCLFE 缺少 Wine"
            ;;
        *)
            _body="$_reason

首次运行需要初始化运行环境：
  · 创建 Wine 前缀
  · 安装 .NET Framework 4.8（约 10–30 分钟，需要联网）

也可以在终端里手动执行：
    pclfe setup
然后重新双击图标，或执行：
    pclfe run"
            _action="pclfe setup && pclfe run"
            _title="PCLFE 尚未就绪"
            ;;
    esac

    if gui_confirm "$_title" "$_body

现在打开终端继续吗？"; then
        if gui_open_terminal "$_action"; then
            return 0
        fi
        gui_error "无法打开终端" "请手动在终端执行：$_action"
        return 0
    fi
    # 没有对话框工具（连“是/否”都问不了）：直接开终端，把说明和后续命令一起给出来
    if ! gui_available; then
        if gui_show_in_terminal "$_title" "$_body" "$_action"; then
            return 0
        fi
    fi
    gui_error "$_title" "$_body"
    return 0
}
