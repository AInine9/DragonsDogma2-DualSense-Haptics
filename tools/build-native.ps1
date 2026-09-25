param([string]$Zig='zig')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    $env:ZIG_GLOBAL_CACHE_DIR=Join-Path $root 'work/zig-cache'
    $env:ZIG_LOCAL_CACHE_DIR=Join-Path $root 'work/zig-local-cache'
    $mh='native/vendor/minhook'
    & $Zig cc -target x86_64-windows-gnu -shared -O2 -Wall -Wextra -Werror -I "$mh/include" native/output_guard.c "$mh/src/buffer.c" "$mh/src/hook.c" "$mh/src/trampoline.c" "$mh/src/hde/hde64.c" -lhid -o distribution/dd2_output_observer.dll
    if($LASTEXITCODE -ne 0){throw 'Native audio guard build failed'}
} finally {Pop-Location}
