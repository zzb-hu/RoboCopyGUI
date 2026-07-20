# RoboCopyGUI

[![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-blue)](https://github.com/)
[![.NET](https://img.shields.io/badge/.NET-8.0-purple)](https://dotnet.microsoft.com/)
[![Tests](https://img.shields.io/badge/tests-37%20passed-brightgreen)](https://github.com/)
[![Dependencies](https://img.shields.io/badge/dependencies-0%20NuGet-success)](https://github.com/)

Windows 内置 `robocopy` 命令的 WPF 图形界面封装。暗色主题、三步复制、MVVM 架构、零 NuGet 依赖、37 个单测全过。

A WPF desktop frontend for Windows' built-in `robocopy`. Dark theme, three-step copy, MVVM architecture, zero NuGet dependencies, 37 unit tests passing.

---

## 功能特性

### 4 个预设场景

| 场景 | 说明 | 关键参数 |
|------|------|----------|
| 🚀 快速复制 | 多线程 16 路并行，大量小文件最快 | `/MT:16` |
| 💾 增量备份 | MIR 镜像模式，只复制新增/修改的文件 | `/MIR` |
| ⏸ 断点续传 | Z 重启模式，中断后可恢复 | `/Z` |
| 🔄 完全同步 | 目标和源一模一样，保留所有属性/权限/时间戳 | `/MIR /COPYALL /DCOPY:T` |

### 易用性

- 文件夹**拖拽**到输入框
- 实时进度：百分比 + **字节进度** + **MB/s 速度** + **剩余时间 ETA**
- 高级参数可调（线程数、重试、权限、目录时间戳、详细日志等）
- 详细日志面板（带时间戳，超 200KB 按行截断）
- 取消按钮**真正终止 robocopy 子进程**（不是只停 UI）
- 复制完成后**"打开目标文件夹"按钮**
- **"目标保留源文件夹名"复选框**：勾选后 `dest\source\...`（跟资源管理器一致），不勾选 `dest\...`（robocopy 原生行为）

### 安全性

- 路径校验：同源同目标 / 目标嵌套在源内 自动拒绝
- MIR 模式删除前二次确认
- **U 盘根目录 + MIR 强警告**：两道确认对话框，默认选 No，防止误删整个磁盘
- 全局异常兜底（Dispatcher / AppDomain / TaskScheduler 三层），不会闪退
- 数字输入框粘贴校验，拒绝非数字内容

### 持久化（自动记忆，保存到 `%AppData%\RoboCopyGUI\settings.json`）

- **场景记忆**：上次选的预设或手动调过的高级参数自动恢复
- **窗口尺寸 + 位置**：关闭时保存，下次启动恢复

### 中文友好

- 自动按系统控制台代码页解码 robocopy 输出（`GetConsoleOutputCP`），中文路径不乱码
- 中英文 robocopy 输出都能正确解析进度

---

## 截图

（待补充）

---

## 下载

从 [Releases](../../releases) 页面下载最新 `RoboCopyGUI.exe`（单文件自包含，约 69 MB，无需安装 .NET 运行时）。
若仓库尚未发布 Release，按下方"自行编译"步骤本地构建。

> **系统要求**：Windows 10 1809+ / Windows 11 (x64)

---

## 自行编译

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

```powershell
# 方法一：一键打包（单文件 exe，约 69 MB）
.\publish.ps1

# 方法二：手动发布
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true
```

产物在 `bin/Release/net8.0-windows/win-x64/publish/RoboCopyGUI.exe`。

### 跑单元测试

```powershell
dotnet test
```

---

## 项目结构

```
RoboCopyGUI/
├── App.xaml(.cs)                # 应用入口 + 全局深色主题 + 手动 DI 配置
├── MainWindow.xaml(.cs)         # 主窗口（薄 code-behind，仅事件转发 + DPI P/Invoke）
├── Assets/                      # 应用图标（ico + 生成脚本）
├── Converters/                  # BrushKeyConverter（string key → Brush 资源）
├── Models/                      # 数据模型
│   ├── CopyResult.cs            #   复制结果 + 进度
│   ├── PresetProfile.cs         #   4 个预设场景
│   └── AppSettings.cs           #   持久化设置
├── Parsing/                     # RobocopyOutputParser（纯逻辑，可单测）
├── Services/                    # 服务接口 + 实现
│   ├── IRobocopyService.cs      #   robocopy 调用抽象
│   ├── RobocopyService.cs       #   实现：启进程 + 读 stdout + 异步预扫描
│   ├── IDialogService.cs        #   弹窗抽象（含 ConfirmDangerTwice）
│   ├── DialogService.cs         #   WPF 实现
│   ├── IConfigurationService.cs #   设置持久化抽象
│   └── ConfigurationService.cs  #   JSON 实现（%AppData%\RoboCopyGUI\）
├── Validation/                  # PathValidator（纯逻辑，可单测）
├── ViewModels/                  # MVVM
│   ├── ViewModelBase.cs         #   INotifyPropertyChanged
│   ├── RelayCommand.cs          #   ICommand + AsyncRelayCommand
│   └── MainViewModel.cs         #   主窗口状态机 + 命令 + 业务逻辑
├── RoboCopyGUI.Tests/           # xUnit 单测项目
│   ├── RobocopyOutputParserTests.cs
│   ├── PathValidatorTests.cs
│   └── MainViewModelTests.cs
├── RoboCopyGUI.sln              # 解决方案（主项目 + 测试项目）
├── RoboCopyGUI.csproj
├── app.manifest                 # Per-Monitor V2 DPI 感知
└── publish.ps1                  # 单文件 exe 发布脚本
```

---

## 架构特性

- **MVVM 模式**：ViewModel 持有所有状态，View 通过 Binding 绑定，code-behind 仅事件转发（~150 行）
- **零 NuGet 依赖**：手写 `ViewModelBase` / `RelayCommand` / 手动 DI，不引 `CommunityToolkit.Mvvm` 或 DI 容器包
- **服务抽象**：`IRobocopyService` / `IDialogService` / `IConfigurationService` 接口分离，便于 mock 测试
- **纯逻辑可测**：`RobocopyOutputParser` 和 `PathValidator` 不依赖 I/O，单测易写
- **状态机**：`CopyState` 枚举（Idle / Running / Cancelling / Completed / Cancelled / Failed）控制所有 UI 派生状态（按钮 IsEnabled / 控件 Visibility）
- **持久化**：JSON 文件存到 `%AppData%\RoboCopyGUI\settings.json`，启动 `Load` + 关闭 `Save`
- **进度节流**：100ms 节流 + `Dispatcher.BeginInvoke`，几十万文件不卡 UI
- **自定义控件模板**：所有控件（TextBox / Button / RadioButton / CheckBox / ProgressBar / Expander / ScrollBar）都有深色 ControlTemplate，跟系统主题解耦

---

## 单元测试

**37 个单测全过**，覆盖：

| 测试类 | 数量 | 覆盖内容 |
|--------|------|----------|
| `RobocopyOutputParserTests` | 13 | 中英文 robocopy 输出解析、横幅/段标题跳过、`*EXTRA File` 前缀、路径含正反斜杠、状态行识别 |
| `PathValidatorTests` | 7 | 空/不存在路径、同源同目标、嵌套检测、盘符根目录识别、路径规范化 |
| `MainViewModelTests` | 17 | 状态机迁移、BrowseSource/Dest、StartCopy 成功/失败/取消、Mirror 触发确认、SelectPreset、OnAdvancedParamChanged、CaptureSettings/ApplySettings、IncludeSourceFolderName |

测试用 mock（手写 `MockDialogService` / `MockRobocopyService`，不引 Moq），不依赖文件系统和真实 robocopy 进程。

---

## 技术栈

| 维度 | 选型 |
|------|------|
| 运行时 | .NET 8.0 (`net8.0-windows`)，自包含单文件 exe 发布 |
| UI 框架 | WPF + WinForms（仅借用 `FolderBrowserDialog`） |
| 语言 | C#（启用 `ImplicitUsings` + `Nullable`） |
| 主题 | 手写深色 Fluent 主题，未使用任何第三方 UI 库 |
| 平台 | Windows 10 1809+ / Windows 11，Per-Monitor V2 DPI 感知 |
| 核心 | 通过 `Process` 调用系统 `robocopy.exe`，重定向 stdout 解析进度 |
| 测试 | xUnit |
| 外部依赖 | **0 个 NuGet 包** |

---

## 协议

MIT
