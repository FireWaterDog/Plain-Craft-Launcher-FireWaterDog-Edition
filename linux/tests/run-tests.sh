#!/usr/bin/env sh
# 冒烟测试：语法、命令分发、配置读写、路径转换。
# 不依赖 Wine，可在 Windows 的 Git Bash 或任意 Linux 上运行：
#   sh linux/tests/run-tests.sh

set -u

TESTS_DIR=$(cd "$(dirname "$0")" && pwd -P)
ROOT_DIR=$(cd "$TESTS_DIR/.." && pwd -P)
PCLFE="$ROOT_DIR/bin/pclfe"

PASS=0
FAIL=0

if [ -t 1 ]; then
    G=$(printf '\033[32m'); R=$(printf '\033[31m'); N=$(printf '\033[0m')
else
    G='' R='' N=''
fi

_pass() { PASS=$((PASS + 1)); printf '  %s✓%s %s\n' "$G" "$N" "$1"; }
_fail() { FAIL=$((FAIL + 1)); printf '  %s✗%s %s\n' "$R" "$N" "$1"; }

TMP_ROOT=$(mktemp -d 2>/dev/null || printf '/tmp/pclfe-test-%s' "$$")
mkdir -p "$TMP_ROOT/data" "$TMP_ROOT/config" "$TMP_ROOT/home"

# 隔离的测试环境，避免污染真实 HOME。
# 注意：这里用子 shell + export，不用 env（某些环境里 PATH 上的 env 不是 GNU coreutils 版本）。
run_iso() {
    (
        HOME="$TMP_ROOT/home"
        XDG_DATA_HOME="$TMP_ROOT/data"
        XDG_CONFIG_HOME="$TMP_ROOT/config"
        PCLFE_HOME="$TMP_ROOT/data/pclfe-linux"
        export HOME XDG_DATA_HOME XDG_CONFIG_HOME PCLFE_HOME
        sh "$@"
    )
}

cleanup() { rm -rf "$TMP_ROOT"; }
trap cleanup EXIT INT TERM

echo "== 语法检查 =="
_code=0
for _f in "$ROOT_DIR"/bin/pclfe "$ROOT_DIR"/lib/*.sh "$ROOT_DIR"/install.sh \
          "$ROOT_DIR"/uninstall.sh "$ROOT_DIR"/build-release.sh "$0"; do
    if sh -n "$_f" 2>/dev/null; then
        _pass "sh -n $(basename "$_f")"
    else
        _fail "sh -n $(basename "$_f") 语法错误"
        sh -n "$_f"
        _code=1
    fi
done

echo ""
echo "== 行尾检查 =="
_crlf=''
for _f in "$ROOT_DIR"/bin/pclfe "$ROOT_DIR"/lib/*.sh "$ROOT_DIR"/install.sh \
          "$ROOT_DIR"/uninstall.sh "$ROOT_DIR"/build-release.sh "$0"; do
    if grep -q "$(printf '\r')" "$_f" 2>/dev/null; then
        _crlf="$_crlf $(basename "$_f")"
    fi
done
if [ -n "$_crlf" ]; then
    _fail "存在 CRLF 行尾的脚本：$_crlf（Linux 下会报 bad interpreter）"
else
    _pass "脚本均为 LF 行尾"
fi

echo ""
echo "== 命令分发 =="
if run_iso "$PCLFE" --help 2>&1 | grep -q 'setup'; then
    _pass "--help 输出包含命令说明"
else
    _fail "--help 输出异常"
fi

if run_iso "$PCLFE" --help >/dev/null 2>&1; then
    _pass "--help 退出码为 0"
else
    _fail "--help 退出码非 0"
fi

if run_iso "$PCLFE" version 2>&1 | grep -q 'PCLFE Linux Edition'; then
    _pass "version 输出正常"
else
    _fail "version 输出异常"
fi

if run_iso "$PCLFE" 不存在的命令 >/dev/null 2>&1; then
    _fail "未知命令应当返回非 0"
else
    _pass "未知命令返回非 0"
fi

echo ""
echo "== 配置读写 =="
run_iso "$PCLFE" config set PCLFE_DPI 120 >/dev/null 2>&1
if grep -q 'PCLFE_DPI=120' "$TMP_ROOT/config/pclfe-linux/config.env" 2>/dev/null; then
    _pass "config set 写入配置文件"
else
    _fail "config set 未写入配置文件"
fi

if run_iso "$PCLFE" config set 无效项 1 >/dev/null 2>&1; then
    _fail "不支持的配置项应当报错"
else
    _pass "不支持的配置项被拒绝"
fi

echo ""
echo "== 自检与安装器 =="
run_iso "$PCLFE" doctor >/dev/null 2>&1
_doc=$?
if [ "$_doc" -ne 0 ]; then
    _pass "doctor 在缺少 Wine 前缀时返回非 0"
else
    _fail "doctor 在环境不全时仍返回 0"
fi

if sh "$ROOT_DIR/install.sh" --help 2>&1 | grep -q 'prefix'; then
    _pass "install.sh --help 正常"
else
    _fail "install.sh --help 异常"
fi

if sh "$ROOT_DIR/uninstall.sh" --help 2>&1 | grep -q 'purge'; then
    _pass "uninstall.sh --help 正常"
else
    _fail "uninstall.sh --help 异常"
fi

echo ""
echo "== 路径转换 =="
_win=$(run_iso -c ". '$ROOT_DIR/lib/common.sh'; win_path /home/user/jdk")
case "$_win" in
    'Z:\home\user\jdk') _pass "win_path 转换正确" ;;
    *) _fail "win_path 结果异常：$_win" ;;
esac

_uni=$(run_iso -c ". '$ROOT_DIR/lib/common.sh'; unix_path 'Z:\\home\\user\\jdk'")
if [ "$_uni" = "/home/user/jdk" ]; then
    _pass "unix_path 转换正确"
else
    _fail "unix_path 结果异常：$_uni"
fi

# 带空格的路径不应被拆开
_win2=$(run_iso -c ". '$ROOT_DIR/lib/common.sh'; win_path '/home/a b/jdk'")
if [ "$_win2" = 'Z:\home\a b\jdk' ]; then
    _pass "win_path 保留空格"
else
    _fail "win_path 处理空格异常：$_win2"
fi

echo ""
echo "== 安装流程（不含 Wine） =="
run_iso "$ROOT_DIR/install.sh" --prefix "$TMP_ROOT/data/pclfe-linux" --bin "$TMP_ROOT/home/bin" \
    --no-deps --no-setup >/dev/null 2>&1
if [ -x "$TMP_ROOT/home/bin/pclfe" ] || [ -L "$TMP_ROOT/home/bin/pclfe" ]; then
    _pass "install.sh 生成命令链接"
else
    _fail "install.sh 未生成命令链接"
fi
if [ -f "$TMP_ROOT/data/pclfe-linux/lib/common.sh" ]; then
    _pass "install.sh 复制运行库"
else
    _fail "install.sh 未复制运行库"
fi

echo ""
echo "== 自定义安装目录 =="
run_iso "$ROOT_DIR/install.sh" --prefix "$TMP_ROOT/alt-home" --bin "$TMP_ROOT/alt-bin" \
    --no-deps --no-setup >/dev/null 2>&1
_alt_out=$( ( HOME="$TMP_ROOT/home" XDG_DATA_HOME="$TMP_ROOT/data" \
              XDG_CONFIG_HOME="$TMP_ROOT/config"; export HOME XDG_DATA_HOME XDG_CONFIG_HOME; \
              "$TMP_ROOT/alt-bin/pclfe" version ) 2>&1 )
case "$_alt_out" in
    *"$TMP_ROOT/alt-home"*) _pass "--prefix 安装后能反推出自定义数据目录" ;;
    *) _fail "--prefix 安装后指向了错误的数据目录：$(printf '%s' "$_alt_out" | tr '\n' ' ')" ;;
esac

echo ""
echo "== 本地安装启动器 =="
printf 'FAKEEXE' > "$TMP_ROOT/fake.exe"
run_iso "$PCLFE" install --from "$TMP_ROOT/fake.exe" >/dev/null 2>&1
if [ -f "$TMP_ROOT/data/pclfe-linux/app/Plain Craft Launcher 2.exe" ]; then
    _pass "install --from 安装 exe"
else
    _fail "install --from 未安装 exe"
fi

if command -v zip >/dev/null 2>&1; then
    mkdir -p "$TMP_ROOT/src/bin"
    printf 'EXE' > "$TMP_ROOT/src/bin/Demo.exe"
    printf 'DLL' > "$TMP_ROOT/src/bin/Demo.dll"
    ( cd "$TMP_ROOT/src" && zip -qr "$TMP_ROOT/pkg.zip" . )
    run_iso "$PCLFE" install --from "$TMP_ROOT/pkg.zip" >/dev/null 2>&1
    if [ -f "$TMP_ROOT/data/pclfe-linux/app/Demo.dll" ]; then
        _pass "install --from 解包 zip 并保留依赖 DLL"
    else
        _fail "install --from 未正确解包 zip"
    fi
else
    printf '  跳过 zip 测试（未安装 zip）\n'
fi

echo ""
echo "== Java 与日志 =="
if run_iso "$PCLFE" java list 2>&1 | grep -q 'java install'; then
    _pass "java list 在无 Java 时给出提示"
else
    _fail "java list 输出异常"
fi

if run_iso "$PCLFE" logs 2>&1 | grep -q 'logs'; then
    _pass "logs 输出日志目录"
else
    _fail "logs 输出异常"
fi

echo ""
if [ "$FAIL" -gt 0 ]; then
    printf '%s失败 %d 项，通过 %d 项%s\n' "$R" "$FAIL" "$PASS" "$N"
    exit 1
fi
printf '%s全部通过（%d 项）%s\n' "$G" "$PASS" "$N"
exit "$_code"
