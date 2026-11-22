#!/usr/bin/env sh
# 公共库：路径约定、日志、下载、路径转换、发行版探测。
# 只定义函数与常量，不产生副作用；由 bin/pclfe 与 install.sh 共同 source。
# 目标 shell：POSIX sh（dash / bash / busybox ash 均可）。

# ============================== 常量 ==============================

PCLFE_NAME="PCLFE Linux Edition"
PCLFE_EXE_NAME="Plain Craft Launcher 2.exe"
PCLFE_RELEASE_EXE_NAME="Plain.Craft.Launcher.2.exe"

# 数据目录：程序、Wine 前缀、JRE、日志全部放在这里，卸载时整目录删除即可。
PCLFE_HOME="${PCLFE_HOME:-${XDG_DATA_HOME:-$HOME/.local/share}/pclfe-linux}"
# 配置目录：仅放 config.env，方便用户手改。
PCLFE_CONFIG_DIR="${PCLFE_CONFIG_DIR:-${XDG_CONFIG_HOME:-$HOME/.config}/pclfe-linux}"
PCLFE_CONFIG="$PCLFE_CONFIG_DIR/config.env"

PCLFE_APP_DIR="$PCLFE_HOME/app"
PCLFE_PREFIX_DIR="$PCLFE_HOME/prefix"
PCLFE_JRE_DIR="$PCLFE_HOME/jre"
PCLFE_LOG_DIR="$PCLFE_HOME/logs"
PCLFE_CACHE_DIR="$PCLFE_HOME/cache"
PCLFE_STATE_DIR="$PCLFE_HOME/state"

# 默认值，可被 config.env 与环境变量覆盖。
# 记住哪些配置项来自环境变量：环境变量优先于 config.env。
pclfe_env_set() {
    if eval "[ -n \"\${$1+x}\" ]"; then printf '1'; else printf '0'; fi
}

PCLFE_DPI_SET=$(pclfe_env_set PCLFE_DPI)
PCLFE_DXVK_SET=$(pclfe_env_set PCLFE_DXVK)
PCLFE_ESYNC_SET=$(pclfe_env_set PCLFE_ESYNC)
PCLFE_RENDERER_SET=$(pclfe_env_set PCLFE_RENDERER)
PCLFE_WINEDEBUG_SET=$(pclfe_env_set PCLFE_WINEDEBUG)
PCLFE_UPDATE_MANIFEST_SET=$(pclfe_env_set PCLFE_UPDATE_MANIFEST)

PCLFE_DPI="${PCLFE_DPI:-96}"
PCLFE_DXVK="${PCLFE_DXVK:-0}"
PCLFE_ESYNC="${PCLFE_ESYNC:-1}"
PCLFE_WINEDEBUG="${PCLFE_WINEDEBUG:--all}"
PCLFE_RENDERER="${PCLFE_RENDERER:-auto}"
# 更新清单默认来源：按顺序回退（raw 在国内常被重置，jsDelivr 作为备用）。
PCLFE_DEFAULT_MANIFEST="https://raw.githubusercontent.com/FireWaterDog/Plain-Craft-Launcher-FireWaterDog-Edition/main/version.json https://cdn.jsdelivr.net/gh/FireWaterDog/Plain-Craft-Launcher-FireWaterDog-Edition@main/version.json"
PCLFE_UPDATE_MANIFEST="${PCLFE_UPDATE_MANIFEST:-$PCLFE_DEFAULT_MANIFEST}"

# ============================== 输出 ==============================

if [ -t 1 ] && [ -z "${NO_COLOR:-}" ]; then
    C_RESET=$(printf '\033[0m')
    C_DIM=$(printf '\033[2m')
    C_RED=$(printf '\033[31m')
    C_GREEN=$(printf '\033[32m')
    C_YELLOW=$(printf '\033[33m')
    C_CYAN=$(printf '\033[36m')
else
    C_RESET='' C_DIM='' C_RED='' C_GREEN='' C_YELLOW='' C_CYAN=''
fi

log()  { printf '%s\n' "$*"; }
info() { printf '%s[pclfe]%s %s\n' "$C_CYAN" "$C_RESET" "$*"; }
ok()   { printf '%s[pclfe]%s %s\n' "$C_GREEN" "$C_RESET" "$*"; }
warn() { printf '%s[pclfe] 警告:%s %s\n' "$C_YELLOW" "$C_RESET" "$*" >&2; }
err()  { printf '%s[pclfe] 错误:%s %s\n' "$C_RED" "$C_RESET" "$*" >&2; }
die()  { err "$*"; exit 1; }

step() { printf '\n%s==>%s %s\n' "$C_CYAN" "$C_RESET" "$*"; }
dim()  { printf '%s%s%s\n' "$C_DIM" "$*" "$C_RESET"; }

# ============================== 探测 ==============================

have() { command -v "$1" >/dev/null 2>&1; }

# 打印第一个可用的命令名；都不可用时返回 1。
first_of() {
    for _cmd in "$@"; do
        if have "$_cmd"; then
            printf '%s\n' "$_cmd"
            return 0
        fi
    done
    return 1
}

wine_bin() {
    # 优先用安装包内置的便携版 Wine（发行版自带的常常过旧）
    for _p in "$PCLFE_HOME/vendor/wine/bin/wine" \
              "${PCLFE_PROGRAM_DIR:-}/vendor/wine/bin/wine"; do
        [ -n "$_p" ] && [ -x "$_p" ] && { printf '%s\n' "$_p"; return 0; }
    done
    first_of wine wine64
}

# 内置 Wine 的 bin 目录（用于 PATH 前置），没有则输出空
wine_vendor_bin() {
    for _d in "$PCLFE_HOME/vendor/wine/bin" "${PCLFE_PROGRAM_DIR:-}/vendor/wine/bin"; do
        [ -n "$_d" ] && [ -x "$_d/wine" ] && { printf '%s\n' "$_d"; return 0; }
    done
    return 1
}

# ---- .NET Framework 4.8 离线安装包（供打包内置） ----
# 官方地址会 302 到 download.microsoft.com，文件名 NDP48-x86-x64-AllOS-ENU.exe
PCLFE_DOTNET_URL="https://go.microsoft.com/fwlink/?linkid=2088631"
PCLFE_DOTNET_FILE="ndp48-x86-x64-allos-enu.exe"

# 缓存里的离线安装包路径（不存在时返回 1）
dotnet_installer_cached() {
    _p="$PCLFE_CACHE_DIR/$PCLFE_DOTNET_FILE"
    [ -s "$_p" ] || return 1
    printf '%s\n' "$_p"
}

# 下载到缓存（已存在则直接返回路径）
dotnet_installer_fetch() {
    _p=$(dotnet_installer_cached) && { printf '%s\n' "$_p"; return 0; }
    deps_require_net
    ensure_dirs
    info "下载 .NET Framework 4.8 离线安装包（约 116 MB）"
    download "$PCLFE_DOTNET_URL" "$PCLFE_CACHE_DIR/$PCLFE_DOTNET_FILE" 900 >&2 || return 1
    printf '%s\n' "$PCLFE_CACHE_DIR/$PCLFE_DOTNET_FILE"
}

# ---- 便携版 Wine（Kron4ek 构建，wow64 模式可跑 32 位程序） ----
PCLFE_WINE_VERSION="${PCLFE_WINE_VERSION:-11.22}"
PCLFE_WINE_ASSET="wine-$PCLFE_WINE_VERSION-amd64-wow64"
PCLFE_WINE_URL="https://github.com/Kron4ek/Wine-Builds/releases/download/$PCLFE_WINE_VERSION/$PCLFE_WINE_ASSET.tar.xz"

wine_vendor_cached() {
    _p="$PCLFE_CACHE_DIR/vendor/$PCLFE_WINE_ASSET.tar.xz"
    [ -s "$_p" ] || return 1
    printf '%s\n' "$_p"
}

wine_vendor_fetch() {
    _p=$(wine_vendor_cached) && { printf '%s\n' "$_p"; return 0; }
    deps_require_net
    mkdir -p "$PCLFE_CACHE_DIR/vendor" 2>/dev/null || true
    info "下载便携版 Wine $PCLFE_WINE_VERSION（约 94 MB）"
    download "$PCLFE_WINE_URL" "$PCLFE_CACHE_DIR/vendor/$PCLFE_WINE_ASSET.tar.xz" 900 >&2 || return 1
    printf '%s\n' "$PCLFE_CACHE_DIR/vendor/$PCLFE_WINE_ASSET.tar.xz"
}

# 解压到 <dest>/wine
wine_vendor_extract() {
    _dest=$1
    _tar=$2
    have tar || return 1
    have xz || { err "需要 xz 解压 Wine：sudo apt install xz-utils"; return 1; }
    _tmp="$PCLFE_CACHE_DIR/.wine-extract"
    rm -rf "$_tmp"
    mkdir -p "$_tmp" || return 1
    tar xf "$_tar" -C "$_tmp" 2>/dev/null || { rm -rf "$_tmp"; return 1; }
    _inner=$(find "$_tmp" -maxdepth 1 -mindepth 1 -type d | head -n 1)
    [ -n "$_inner" ] || { rm -rf "$_tmp"; return 1; }
    mkdir -p "$_dest" || { rm -rf "$_tmp"; return 1; }
    rm -rf "$_dest/wine"
    mv "$_inner" "$_dest/wine" || { rm -rf "$_tmp"; return 1; }
    rm -rf "$_tmp"
    # 精简：删掉开发头文件与手册，省 ~40 MB
    rm -rf "$_dest/wine/include" "$_dest/wine/share/man" "$_dest/wine/share/doc" 2>/dev/null || true
    return 0
}

# 是否随包带了 Wine 压缩包（尚未解压也算）
wine_vendor_has_archive() {
    for _d in "${PCLFE_PROGRAM_DIR:-}" "$PCLFE_HOME"; do
        [ -n "$_d" ] || continue
        [ -f "$_d/vendor/$PCLFE_WINE_ASSET.tar.xz" ] && return 0
        for _f in "$_d"/vendor/wine-*.tar.xz; do
            [ -f "$_f" ] && return 0
        done
    done
    return 1
}

# 首次使用时把随包的 Wine 解压到数据目录
wine_vendor_ensure() {
    wine_vendor_bin >/dev/null 2>&1 && return 0
    for _d in "${PCLFE_PROGRAM_DIR:-}" "$PCLFE_HOME"; do
        [ -n "$_d" ] || continue
        for _f in "$_d/vendor/$PCLFE_WINE_ASSET.tar.xz" "$_d"/vendor/wine-*.tar.xz; do
            [ -f "$_f" ] || continue
            step "解压内置 Wine（约 946 MB，首次需要 1~2 分钟）"
            if wine_vendor_extract "$PCLFE_HOME/vendor" "$_f"; then
                ok "内置 Wine 就绪：$("$PCLFE_HOME/vendor/wine/bin/wine" --version 2>/dev/null || echo wine)"
                return 0
            fi
            err "解压内置 Wine 失败：$_f"
            return 1
        done
    done
    return 1
}

# 把内置 Wine 的 bin 放到 PATH 前面，保证 wineserver/wineboot 同源
wine_prepare_path() {
    _wd=$(wine_vendor_bin) || return 1
    PATH="$_wd:$PATH"
    export PATH
    return 0
}
dotnet_installer_bundled() {
    for _d in "$PCLFE_HOME/dotnet" "${PCLFE_PROGRAM_DIR:-}/dotnet" \
              "${PCLFE_PROGRAM_DIR:-}/app/dotnet"; do
        [ -n "$_d" ] && [ -d "$_d" ] || continue
        for _f in "$_d"/*.exe; do
            [ -f "$_f" ] && { printf '%s\n' "$_f"; return 0; }
        done
    done
    return 1
}

# 形如 4.0 / 8.0.1 / 9.0（去掉 "wine-" 前缀）
wine_version() {
    _w=$(wine_bin) || return 1
    _v=$("$_w" --version 2>/dev/null | tr -d '\r' | sed 's/^wine-//' | head -n 1)
    [ -n "$_v" ] || return 1
    printf '%s\n' "$_v"
}

wine_major() {
    _v=$(wine_version) || return 1
    printf '%s\n' "${_v%%.*}"
}

wine_arch() {
    case "$(uname -m)" in
        x86_64|amd64) printf 'x86_64\n' ;;
        aarch64|arm64) printf 'aarch64\n' ;;
        *) uname -m ;;
    esac
}

os_id() {
    if [ -r /etc/os-release ]; then
        # shellcheck disable=SC1091
        . /etc/os-release
        printf '%s\n' "${ID:-unknown}"
    elif have uname; then
        uname -s | tr '[:upper:]' '[:lower:]'
    else
        printf 'unknown\n'
    fi
}

os_like() {
    if [ -r /etc/os-release ]; then
        # shellcheck disable=SC1091
        . /etc/os-release
        printf '%s\n' "${ID_LIKE:-${ID:-unknown}}"
    else
        os_id
    fi
}

# 当前系统的包管理器，用于给出安装建议。
pkg_manager() {
    _id=$(os_id)
    _like=$(os_like)
    for _cand in apt-get dnf yum pacman zypper apk xbps-install eopkg; do
        if have "$_cand"; then
            printf '%s\n' "$_cand"
            return 0
        fi
    done
    case "$_id $_like" in
        *debian*|*ubuntu*) printf 'apt-get\n' ;;
        *fedora*|*rhel*|*centos*) printf 'dnf\n' ;;
        *arch*) printf 'pacman\n' ;;
        *suse*) printf 'zypper\n' ;;
        *alpine*) printf 'apk\n' ;;
        *) return 1 ;;
    esac
}

# ============================== 路径转换 ==============================

# Unix 绝对路径 -> Wine 的 Z: 盘 Windows 路径。
# /home/a b/jdk -> Z:\home\a b\jdk
win_path() {
    _p=$1
    case "$_p" in
        [A-Za-z]:[\\/]*) printf '%s\n' "$_p"; return 0 ;;
    esac
    if [ "${_p#/}" = "$_p" ]; then
        # 相对路径按当前目录展开
        _p=$(cd "$_p" 2>/dev/null && pwd) || return 1
    fi
    printf 'Z:%s\n' "$(printf '%s' "$_p" | tr '/' '\\')"
}

# Windows 路径 -> Unix 路径（仅处理 Z: 盘与 %APPDATA% 常见情形）。
unix_path() {
    _p=$1
    case "$_p" in
        [Zz]:\\*|[Zz]:/*)
            _rest=${_p#??}
            printf '%s\n' "$(printf '%s' "$_rest" | tr '\\' '/')"
            ;;
        *)
            printf '%s\n' "$_p"
            ;;
    esac
}

# ============================== 配置 ==============================

# 读取配置文件。环境变量优先：配置里只补齐尚未由环境变量指定的项。
load_config() {
    [ -r "$PCLFE_CONFIG" ] || return 0
    while IFS='=' read -r _k _v || [ -n "$_k" ]; do
        case "$_k" in
            ''|\#*) continue ;;
        esac
        _v=${_v%\"}; _v=${_v#\"}
        case "$_k" in
            PCLFE_DPI) [ "$PCLFE_DPI_SET" = "1" ] || PCLFE_DPI=$_v ;;
            PCLFE_DXVK) [ "$PCLFE_DXVK_SET" = "1" ] || PCLFE_DXVK=$_v ;;
            PCLFE_ESYNC) [ "$PCLFE_ESYNC_SET" = "1" ] || PCLFE_ESYNC=$_v ;;
            PCLFE_RENDERER) [ "$PCLFE_RENDERER_SET" = "1" ] || PCLFE_RENDERER=$_v ;;
            PCLFE_WINEDEBUG) [ "$PCLFE_WINEDEBUG_SET" = "1" ] || PCLFE_WINEDEBUG=$_v ;;
            PCLFE_UPDATE_MANIFEST) [ "$PCLFE_UPDATE_MANIFEST_SET" = "1" ] || PCLFE_UPDATE_MANIFEST=$_v ;;
            *) continue ;;
        esac
    done < "$PCLFE_CONFIG"
}

ensure_dirs() {
    mkdir -p "$PCLFE_APP_DIR" "$PCLFE_JRE_DIR" "$PCLFE_LOG_DIR" \
             "$PCLFE_CACHE_DIR" "$PCLFE_STATE_DIR" "$PCLFE_CONFIG_DIR"
}

save_config() {
    ensure_dirs
    {
        printf '# PCLFE Linux Edition 配置（由 pclfe 维护，可手工修改）\n'
        printf 'PCLFE_DPI=%s\n' "$PCLFE_DPI"
        printf 'PCLFE_DXVK=%s\n' "$PCLFE_DXVK"
        printf 'PCLFE_ESYNC=%s\n' "$PCLFE_ESYNC"
        printf 'PCLFE_RENDERER=%s\n' "$PCLFE_RENDERER"
        printf 'PCLFE_WINEDEBUG=%s\n' "$PCLFE_WINEDEBUG"
        # 只有用户改过清单来源时才落盘，避免把默认值写死、以后无法跟随程序更新。
        if [ "$PCLFE_UPDATE_MANIFEST" != "$PCLFE_DEFAULT_MANIFEST" ]; then
            printf 'PCLFE_UPDATE_MANIFEST=%s\n' "$PCLFE_UPDATE_MANIFEST"
        fi
    } > "$PCLFE_CONFIG"
}

# ============================== 网络 ==============================

# download <url> <dest> [超时秒]
download() {
    _url=$1
    _dest=$2
    _timeout=${3:-300}
    mkdir -p "$(dirname "$_dest")"
    if have curl; then
        if [ -t 2 ]; then _curl_progress="--progress-bar"; else _curl_progress="-sS"; fi
        # shellcheck disable=SC2086
        curl -fL $_curl_progress --retry 3 --retry-delay 2 --connect-timeout 20 \
             --max-time "$_timeout" -o "$_dest.part" "$_url" || {
            rm -f "$_dest.part"; return 1; }
    elif have wget; then
        wget -q --tries=3 --timeout=20 -O "$_dest.part" "$_url" || {
            rm -f "$_dest.part"; return 1; }
    else
        err "需要 curl 或 wget 才能下载文件"
        return 1
    fi
    mv -f "$_dest.part" "$_dest"
}

sha256_of() {
    if have sha256sum; then
        sha256sum "$1" | awk '{print $1}'
    elif have shasum; then
        shasum -a 256 "$1" | awk '{print $1}'
    elif have openssl; then
        openssl dgst -sha256 "$1" | awk '{print $NF}'
    else
        return 1
    fi
}

# 从 JSON 文本中取一个字符串字段（不依赖 jq；优先 python3，保证中文字段正确）。
json_str() {
    _file=$1
    _key=$2
    if have python3; then
        python3 - "$_file" "$_key" <<'PY'
import json, sys
path, key = sys.argv[1], sys.argv[2]
try:
    with open(path, encoding="utf-8-sig") as fh:
        data = json.load(fh)
except Exception:
    sys.exit(1)
def walk(node, target):
    if isinstance(node, dict):
        if target in node and isinstance(node[target], str):
            print(node[target]); sys.exit(0)
        for value in node.values():
            walk(value, target)
    elif isinstance(node, list):
        for item in node:
            walk(item, target)
walk(data, key)
sys.exit(1)
PY
    else
        sed -n 's/.*"'"$_key"'"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$_file" | head -n 1
    fi
}

# ============================== 交互 ==============================

confirm() {
    _prompt=$1
    _default=${2:-n}
    if [ ! -t 0 ]; then
        [ "$_default" = "y" ]
        return $?
    fi
    if [ "$_default" = "y" ]; then
        printf '%s [Y/n] ' "$_prompt"
    else
        printf '%s [y/N] ' "$_prompt"
    fi
    read -r _answer || _answer=''
    case "$_answer" in
        '') [ "$_default" = "y" ] ;;
        [Yy]*) return 0 ;;
        *) return 1 ;;
    esac
}

# ============================== 杂项 ==============================

timestamp() { date '+%Y%m%d-%H%M%S'; }

human_size() {
    if [ -f "$1" ]; then
        # 文件用 ls 的“表观大小”，du 会把块对齐算进去，显示偏大
        ls -lh "$1" 2>/dev/null | awk '{print $5}'
    elif [ -d "$1" ] && have du; then
        du -sh "$1" 2>/dev/null | awk '{print $1}'
    else
        printf '?\n'
    fi
}

# 版本号来自仓库的 version.json，缺失时回退。
pclfe_version() {
    for _v in "$PCLFE_HOME/version.json" "$PCLFE_APP_DIR/../version.json" \
              "$(dirname "$0")/../version.json" "./version.json"; do
        if [ -r "$_v" ]; then
            _name=$(json_str "$_v" versionName 2>/dev/null || true)
            if [ -n "$_name" ]; then
                printf '%s\n' "$_name"
                return 0
            fi
        fi
    done
    printf 'unknown\n'
}
