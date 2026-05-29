# WinSW 可执行文件

本目录存放 WinSW 3.x **.NET Framework** 构建版本（非 .NET 7 自包含版）。

| 文件 | 用途 |
|------|------|
| `WinSW-x64.exe` | x64 系统托管服务 |
| `WinSW-x86.exe` | 托管 32 位目标程序时使用 |

## 获取方式

从 [WinSW Releases](https://github.com/winsw/winsw/releases) 下载 Framework 版，重命名后放入此目录。

或使用脚本（需网络）：

```powershell
$base = "https://github.com/winsw/winsw/releases/download/v2.12.0"
Invoke-WebRequest "$base/WinSW-x64.exe" -OutFile "WinSW-x64.exe"
Invoke-WebRequest "$base/WinSW-x86.exe" -OutFile "WinSW-x86.exe"
```

> P2 阶段安装服务时将复制并重命名为 `{serviceId}.exe`。
