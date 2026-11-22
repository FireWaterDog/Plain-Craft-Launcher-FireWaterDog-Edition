#!/usr/bin/env sh
# 环境自检：pclfe doctor。
# 输出每一项的结论与可操作的修复建议；致命问题返回 1。

DIAG_FATAL=0
DIAG_WARN=0

_d_ok()   { printf '  %s✓%s %s\n' "$C_GREEN" "$C_RESET" "$*"; }
_d_warn() { printf '  %s!%s %s\n' "$C_YELLOW" "$C_RESET" "$*"; DIAG_WARN=$((DIAG_WARN + 1)); }
_d_bad()  { printf '  %s✗%s %s\n' "$C_RED" "$C_RESET" "$*"; DIAG_FATAL=$((DIAG_FATAL + 1)); }
_d_hint() { printf '      %s→ %s%s\n' "$C_DIM" "$*" "$C_RESET"; }

diag_run() {
    DIAG_FATAL=0
    DIAG_WARN=0

    log "${C_CYAN}PCLFE Linux Edition 环境自检${C_RESET}"
    log "$(date '+%Y-%m-%d %H:%M:%S')"
    log ""

    # ---------- 系统 ----------
    log "${C_CYAN}[系统]${C_RESET}"
    _os=$(os_id)
    _arch=$(wine_arch)
    _desk=${XDG_CURRENT_DESKTOP:-${DESKTOP_SESSION:-未知}}
    _d_ok "发行版 $_os，架构 $_arch，桌面 $_desk"
    case "$_arch" in
        x86_64) ;;
        aarch64) _d_bad "ARM64 平台：PCLFE 依赖的 Wine 与 .NET Framework 4.8 均为 x86_64，无法运行";;
        *) _d_warn "非 x86_64 架构，可能无法运行";;
    esac
    if [ -n "${WAYLAND_DISPLAY:-}" ] && [ -z "${DISPLAY:-}" ]; then
        _d_warn "当前是纯 Wayland 会话，Wine 需要 XWayland"
        _d_hint "安装 xwayland，或改用 X11 会话登录"
    fi

    # ---------- Wine ----------
    log ""
    log "${C_CYAN}[Wine]${C_RESET}"
    if _wine=$(wine_bin); then
        _wver=$("$_wine" --version 2>/dev/null | tr -d '\r')
        _d_ok "wine：${_wver:-未知}"
        _wmaj=$(wine_major 2>/dev/null || echo 0)
        case "$_wmaj" in ''|*[!0-9]*) _wmaj=0 ;; esac
        if [ "$_wmaj" -lt 6 ]; then
            _d_warn "Wine 版本偏旧，.NET Framework 4.8 可能装不上"
            _d_hint "建议 Wine ≥ 7：https://wiki.winehq.org/ 添加官方源升级"
        fi
    else
        _d_bad "未安装 wine"
        _d_hint "参考 'pclfe setup' 输出的安装命令"
    fi

    if _wb=$(winetricks_bin 2>/dev/null); then
        _d_ok "winetricks：$("$_wb" --version 2>/dev/null | head -n 1)"
        if winetricks_has_dotnet48; then
            _d_ok "winetricks 支持 dotnet48"
        else
            _d_warn "当前 winetricks 没有 dotnet48（版本过旧），setup 时会自动下载新版"
        fi
    else
        _d_bad "未安装 winetricks（无法安装 .NET Framework 4.8）"
        _d_hint "参考 'pclfe setup' 输出的安装命令"
    fi

    if [ "$(uname -m)" = "x86_64" ]; then
        _w32=''
        for _d in /usr/lib/wine/i386-unix /usr/lib/i386-linux-gnu/wine /usr/lib32/wine; do
            [ -d "$_d" ] && { _w32=1; break; }
        done
        if [ -n "$_w32" ]; then
            _d_ok "32 位 Wine：可用（安装 .NET 4.8 必需）"
        else
            _d_warn "未检测到 32 位 Wine，.NET 4.8 很可能装不上"
            _d_hint "sudo dpkg --add-architecture i386 && sudo apt update && sudo apt install wine32:i386"
        fi
    fi

    if prefix_exists; then
        _d_ok "Wine 前缀：$PCLFE_PREFIX_DIR"
        if dotnet48_installed; then
            _d_ok ".NET Framework 4.8：已安装"
        else
            _d_bad ".NET Framework 4.8：未安装（启动器无法运行）"
            _d_hint "pclfe setup"
        fi
        if webview2_installed; then
            _d_ok "WebView2 运行时：$(webview2_version)"
        elif [ -n "${PCLFE_WEBVIEW2_FOLDER:-}" ]; then
            _d_warn "WebView2：使用外部目录 $PCLFE_WEBVIEW2_FOLDER"
        else
            _d_warn "WebView2：未安装（微软正版登录不可用，离线登录正常）"
            _d_hint "pclfe webview2 install"
        fi
    else
        _d_bad "Wine 前缀未创建"
        _d_hint "pclfe setup"
    fi

    # ---------- 启动器 ----------
    log ""
    log "${C_CYAN}[启动器]${C_RESET}"
    if _exe=$(runtime_exe); then
        _d_ok "程序：$_exe（$(human_size "$_exe")）"
        _man=$(app_manifest 2>/dev/null || true)
        if [ -n "$_man" ]; then
            _want=$(json_str "$_man" sha256 2>/dev/null || true)
            _have=$(sha256_of "$_exe" 2>/dev/null || true)
            if [ -n "$_want" ] && [ -n "$_have" ]; then
                if [ "$_want" = "$_have" ]; then
                    _d_ok "SHA256 与版本清单一致（$(app_current_version)）"
                else
                    _d_warn "SHA256 与版本清单不一致（本地构建或已修改）"
                    _d_hint "需要官方版本时执行：pclfe update"
                fi
            fi
        else
            _d_warn "缺少 version.json，无法校验版本"
        fi
    else
        _d_bad "未安装启动器程序"
        _d_hint "pclfe update   或   pclfe install --from <文件>"
    fi

    # ---------- Java ----------
    log ""
    log "${C_CYAN}[Java]${C_RESET}"
    _jlist=$(java_installed_list)
    if [ -n "$_jlist" ]; then
        printf '%s\n' "$_jlist" | while IFS= read -r _j; do
            _d_ok "已安装：$_j"
        done
    else
        _d_warn "未通过 pclfe 安装 Windows 版 Java"
        _d_hint "pclfe java install 21（PCL 也会自行下载，可忽略本项）"
    fi
    if [ -n "${JAVA_HOME:-}" ]; then
        _d_ok "JAVA_HOME=$JAVA_HOME"
    fi
    _pclrt="$PCLFE_PREFIX_DIR/drive_c/users/$(whoami)/AppData/Roaming/.minecraft/runtime"
    if [ -d "$_pclrt" ]; then
        _d_ok "PCL 自带的 Java 目录存在：.minecraft\\runtime"
    fi

    # ---------- 字体 ----------
    log ""
    log "${C_CYAN}[字体]${C_RESET}"
    if have fc-list; then
        _cjk=$(fc-list :lang=zh 2>/dev/null | wc -l | tr -d ' ')
        if [ "${_cjk:-0}" -gt 0 ]; then
            _d_ok "中文字体：${_cjk} 个"
        else
            _d_warn "未检测到中文字体，界面可能显示为方块"
            _d_hint "安装 fonts-noto-cjk（Debian/Ubuntu）或 noto-fonts-cjk（Arch）"
        fi
    else
        _d_warn "缺少 fc-list（fontconfig），无法检查字体"
    fi

    # ---------- 图形 ----------
    log ""
    log "${C_CYAN}[图形]${C_RESET}"
    if have glxinfo; then
        _gl=$(glxinfo -B 2>/dev/null | sed -n 's/.*OpenGL renderer string:[[:space:]]*//p' | head -n 1)
        if [ -n "$_gl" ]; then
            _d_ok "OpenGL：$_gl"
        else
            _d_warn "无法读取 OpenGL 信息"
        fi
    else
        _d_warn "未安装 glxinfo，跳过 OpenGL 检查"
        _d_hint "mesa-utils（Debian/Ubuntu）"
    fi
    if dxvk_enabled; then
        _dxvk="$PCLFE_PREFIX_DIR/drive_c/windows/system32/d3d11.dll"
        if [ -f "$_dxvk" ]; then
            _d_ok "DXVK：已启用"
        else
            _d_warn "渲染器已指向 DXVK，但前缀内没有 DXVK"
            _d_hint "pclfe prefix winetricks 然后安装 dxvk"
        fi
    fi

    # ---------- 音频 ----------
    log ""
    log "${C_CYAN}[音频]${C_RESET}"
    if have pactl; then
        if pactl info >/dev/null 2>&1; then
            _d_ok "PulseAudio/PipeWire 可用"
        else
            _d_warn "PulseAudio 未响应，游戏可能没有声音"
        fi
    else
        _d_warn "未安装 pactl（pulseaudio-utils），无法检查音频"
    fi

    # ---------- 桌面项 ----------
    log ""
    log "${C_CYAN}[桌面项]${C_RESET}"
    _desktop_found=''
    for _df in "$HOME/.local/share/applications/pclfe.desktop" /usr/share/applications/pclfe.desktop; do
        [ -f "$_df" ] || continue
        _desktop_found=1
        # Exec 可能是 `"带 空格 的路径" run` 或 `/path/pclfe run`，都取可执行文件本身
        _line=$(sed -n 's/^Exec=//p' "$_df" | head -n 1)
        case "$_line" in
            \"*) _target=$(printf '%s' "$_line" | sed 's/^"\([^"]*\)".*/\1/') ;;
            *)   _target=$(printf '%s' "$_line" | awk '{print $1}') ;;
        esac
        if [ -n "$_target" ] && [ -x "$_target" ]; then
            _d_ok "菜单项：$_df → $_target"
        else
            _d_warn "菜单项指向的程序不存在：${_target:-（未解析出路径）}"
            _d_hint "pclfe desktop    # 重写菜单项"
        fi
    done
    if [ -z "$_desktop_found" ]; then
        _d_warn "没有找到 PCLFE 的应用菜单项"
        _d_hint "pclfe desktop    # 生成菜单项"
    fi

    # ---------- 其他 ----------
    log ""
    log "${C_CYAN}[其他]${C_RESET}"
    if have xdg-open; then
        _d_ok "xdg-open 可用（登录页与帮助链接会调用系统浏览器）"
    else
        _d_warn "缺少 xdg-open，内置浏览器打不开外链"
    fi
    _free=$(df -Pk "$PCLFE_HOME" 2>/dev/null | awk 'NR==2 {print int($4/1024/1024)}')
    if [ -n "${_free:-}" ]; then
        if [ "$_free" -lt 2 ]; then
            _d_warn "数据目录剩余空间 ${_free}GB，安装游戏可能不够"
        else
            _d_ok "数据目录剩余空间 ${_free}GB"
        fi
    fi
    log ""

    # ---------- 汇总 ----------
    if [ "$DIAG_FATAL" -gt 0 ]; then
        err "发现 $DIAG_FATAL 个致命问题、$DIAG_WARN 个警告"
        dim "致命问题修复后重新运行：pclfe doctor"
        return 1
    elif [ "$DIAG_WARN" -gt 0 ]; then
        warn "未发现致命问题，有 $DIAG_WARN 个警告"
        return 0
    else
        ok "环境检查全部通过"
        return 0
    fi
}

# 生成一份可直接粘贴的诊断报告：双击/启动失败时用这个定位。
diag_report() {
    _out="$PCLFE_LOG_DIR/diagnose-$(timestamp).txt"
    mkdir -p "$PCLFE_LOG_DIR" 2>/dev/null || true
    {
        printf 'PCLFE 诊断报告\n生成时间：%s\n\n' "$(date '+%Y-%m-%d %H:%M:%S %z' 2>/dev/null)"

        printf '== 系统 ==\n'
        uname -a 2>/dev/null
        grep -E '^(PRETTY_NAME|VERSION_ID)=' /etc/os-release 2>/dev/null
        printf '桌面会话：DISPLAY=%s WAYLAND_DISPLAY=%s XDG_CURRENT_DESKTOP=%s\n' \
            "${DISPLAY:-未设置}" "${WAYLAND_DISPLAY:-未设置}" "${XDG_CURRENT_DESKTOP:-未设置}"
        printf '对话框工具：%s\n' "$(first_of zenity kdialog xmessage 2>/dev/null || printf '无')"
        printf '终端程序：%s\n' "$(first_of x-terminal-emulator gnome-terminal konsole xfce4-terminal deepin-terminal xterm 2>/dev/null || printf '无')"

        printf '\n== 安装位置 ==\n'
        printf '程序目录：%s\n' "${PCLFE_PROGRAM_DIR:-未知}"
        ls -l "${PCLFE_PROGRAM_DIR:-/nonexistent}/bin/pclfe" 2>&1
        printf '数据目录：%s\n' "$PCLFE_HOME"
        du -sh "$PCLFE_HOME" 2>/dev/null
        printf '启动器本体：\n'
        ls -l "$PCLFE_APP_DIR" 2>&1 | head -n 6

        printf '\n== 桌面项 ==\n'
        for _f in /usr/share/applications/pclfe.desktop "$HOME/.local/share/applications/pclfe.desktop"; do
            if [ -f "$_f" ]; then
                printf '%s\n' "$_f"
                grep -E '^(Exec|TryExec|Icon|Terminal|NoDisplay)=' "$_f" 2>/dev/null
            fi
        done

        printf '\n== 自检 ==\n'
        diag_run 2>&1 || true

        printf '\n== 最近提示（gui.log）==\n'
        tail -n 20 "$PCLFE_LOG_DIR/gui.log" 2>/dev/null || printf '（无）\n'

        printf '\n== 最近一次运行日志 ==\n'
        _last=$(ls -t "$PCLFE_LOG_DIR"/run-*.log 2>/dev/null | head -n 1)
        if [ -n "$_last" ]; then
            printf '%s\n' "$_last"
            tail -n 20 "$_last" 2>/dev/null
        else
            printf '（无）\n'
        fi
    } > "$_out" 2>&1

    ok "诊断报告：$_out"
    printf '\n'
    sed -n '1,40p' "$_out"
    printf '\n'
    log "把这份文件（或上面的输出）发给开发者即可定位问题。"
}
