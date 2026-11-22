#!/usr/bin/env sh
# 依赖探测与安装建议。不主动调用 sudo，只打印可直接复制的命令。

# 运行时必需命令（缺一不可）。
PCLFE_REQUIRED_CMDS="cabextract unzip"

# 返回缺失的必要依赖，每行一个。wine / winetricks 单独处理（名称随发行版不同）。
deps_missing() {
    _missing=''
    _bundled_dotnet=''
    dotnet_installer_bundled >/dev/null 2>&1 && _bundled_dotnet=1

    # 内置了便携 Wine 时不再要求系统装 wine
    if ! wine_bin >/dev/null 2>&1 && ! wine_vendor_has_archive; then
        _missing="$_missing wine"
    fi
    # 内置了 .NET 离线包时可以不用 winetricks（直装模式）
    if ! have winetricks && [ -z "$_bundled_dotnet" ]; then
        _missing="$_missing winetricks"
    fi
    if have winetricks || [ -z "$_bundled_dotnet" ]; then
        for _c in $PCLFE_REQUIRED_CMDS; do
            have "$_c" || _missing="$_missing $_c"
        done
    fi
    if ! first_of curl wget >/dev/null 2>&1; then
        _missing="$_missing curl"
    fi
    if ! first_of sha256sum shasum openssl >/dev/null 2>&1; then
        _missing="$_missing coreutils"
    fi

    for _m in $_missing; do
        printf '%s\n' "$_m"
    done
}

# 打印当前发行版对应的安装命令。
deps_hint() {
    _pm=$(pkg_manager || true)
    case "$_pm" in
        apt-get)
            printf 'sudo apt-get update\n'
            printf 'sudo apt-get install -y wine winetricks cabextract unzip curl fonts-noto-cjk\n'
            ;;
        dnf|yum)
            printf 'sudo %s install -y wine winetricks cabextract unzip curl google-noto-sans-cjk-fonts\n' "$_pm"
            ;;
        pacman)
            printf 'sudo pacman -S --needed wine winetricks cabextract unzip curl noto-fonts-cjk\n'
            ;;
        zypper)
            printf 'sudo zypper install -y wine winetricks cabextract unzip curl noto-sans-cjk-fonts\n'
            ;;
        apk)
            printf 'sudo apk add wine winetricks cabextract unzip curl font-noto-cjk\n'
            ;;
        xbps-install)
            printf 'sudo xbps-install -Sy wine winetricks cabextract unzip curl noto-fonts-cjk\n'
            ;;
        *)
            printf '# 未识别的发行版，请手动安装：wine、winetricks、cabextract、unzip、curl、中日韩字体\n'
            ;;
    esac
    printf '# winetricks 若软件源中没有，可手动安装：\n'
    printf '#   curl -fsSL https://raw.githubusercontent.com/Winetricks/winetricks/master/src/winetricks -o ~/.local/bin/winetricks\n'
    printf '#   chmod +x ~/.local/bin/winetricks\n'
}

deps_print_report() {
    _miss=$(deps_missing)
    if [ -z "$_miss" ]; then
        ok "依赖齐全"
        return 0
    fi
    warn "缺少依赖：$(printf '%s' "$_miss" | tr '\n' ' ')"
    printf '\n建议执行：\n'
    deps_hint | sed 's/^/    /'
    printf '\n'
    return 1
}

# 检查依赖；缺失时给出安装建议并退出。
deps_require() {
    _miss=$(deps_missing)
    if [ -z "$_miss" ]; then
        return 0
    fi
    err "缺少依赖：$(printf '%s' "$_miss" | tr '\n' ' ')"
    printf '\n建议执行：\n'
    deps_hint | sed 's/^/    /'
    printf '\n安装完成后重新运行本命令。\n'
    exit 1
}

# 只检查与启动直接相关的部分（update / java 子命令不需要 wine）。
deps_require_net() {
    if ! first_of curl wget >/dev/null 2>&1; then
        err "需要 curl 或 wget 才能下载文件"
        exit 1
    fi
}
