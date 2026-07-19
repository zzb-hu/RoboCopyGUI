# RoboCopyGUI

[![Platform](https://img.shields.io/badge/platform-Windows-blue)](https://github.com/)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)

Windows 内置 `robocopy` 命令的 WPF 图形界面封装。暗色主题，三步完成文件复制。

A WPF desktop frontend for Windows' built-in `robocopy` command. Dark theme, three steps to copy files.

---

## 功能 / Features

| 场景 | 说明 |
|------|------|
| 快速复制 | 多线程 16 路并行，大量小文件最快 |
| 增量备份 | MIR 镜像模式，只复制新增/修改的文件 |
| 断点续传 | Z 重启模式，中断后可恢复 |
| 完全同步 | 目标和源一模一样，保留所有属性/权限/时间戳 |

- 文件夹选择支持拖拽
- 实时进度条 + 文件级进度显示
- 高级参数可调（线程数、重试次数、权限复制等）
- 详细日志展开排查

## 截图 / Screenshot

（待补充）

---

## 下载 / Download

从 [Releases](https://github.com/USER/REPO/releases) 页面下载最新 `RoboCopyGUI.exe`（单文件自包含，无需安装 .NET 运行时）。

Download the latest `RoboCopyGUI.exe` from the [Releases](https://github.com/USER/REPO/releases) page (single-file self-contained, no .NET runtime required).

> **系统要求**：Windows 10 1809+ / Windows 11
>
> **Requires**：Windows 10 1809+ / Windows 11

---

## 自行编译 / Build from Source

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

```powershell
# 方法一：一键打包（单文件 exe）
.\publish.ps1

# 方法二：手动发布
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true
```

产物在 `bin/Release/net8.0-windows/win-x64/publish/RoboCopyGUI.exe`。

---

## 项目结构 / Structure

```
RoboCopyGUI/
├── Assets/              # 应用图标（ico + 生成脚本）
├── Models/              # 数据模型（CopyResult、PresetProfile）
├── Services/            # 核心服务（RobocopyService）
├── App.xaml(.cs)        # 应用入口 + 全局暗色主题
├── MainWindow.xaml(.cs) # 主窗口
├── app.manifest         # DPI 感知
└── publish.ps1          # 发布脚本
```

---

## 协议 / License

MIT
