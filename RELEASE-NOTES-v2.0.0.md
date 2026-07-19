# RoboCopyGUI v2.0.0 发行说明

> 发布日期：2026-07-19

---

## v2.0.0 概述

从 v1.0.0 到 v2.0.0 是一次**全面的架构升级**，代码规模从约 1542 行增长到约 4117 行（净增 2575 行），新增 18 个文件，重构 9 个现有文件。核心变化是将单体 code-behind 拆分为清晰的 **MVVM 架构**，并引入服务抽象层、路径校验、Robocopy 输出解析器和单元测试项目。

---

## 对比 v1.0.0 的提升

### 架构（最大变化）

| 维度 | v1.0.0 | v2.0.0 |
|------|--------|--------|
| 代码组织 | 单体 WPF code-behind（MainWindow.xaml.cs ~1400 行） | MVVM 分层：View（薄 code-behind ~300 行）+ ViewModel + Service + Validation + Parsing |
| 依赖注入 | 内部 `new()` 硬编码 | 构造函数注入 + 手动 DI 容器（零外部依赖） |
| 可测试性 | 无法编写单元测试 | 通过接口隔离，附带 3 组单元测试（共 579 行） |
| 服务抽象 | RobocopyService 硬耦合 | `IRobocopyService` / `IDialogService` / `IConfigurationService` 接口分离 |

### 新增功能

| 功能 | 说明 |
|------|------|
| 设置持久化 | 自动保存最近路径、选中的场景、高级参数到 `%AppData%\RoboCopyGUI\settings.json`，下次打开自动恢复 |
| 窗口状态记忆 | 记住窗口位置、大小，下次启动在相同位置打开 |
| 路径预校验 | 开始复制前校验源目录是否存在、目标是否合法、是否陷入自身循环，提前给出明确错误 |
| 双重确认保护 | 危险操作（如镜像模式覆盖目标）弹两次确认框，默认 No 防误按回车 |
| Robocopy 输出实时解析 | 解析 robocopy stdout，兼容中英文输出格式 |
| 高级参数面板 | 多线程 MT / 镜像模式 / 包含子目录 / 重启模式等参数开关 |
| 剪贴板/拖拽粘贴校验 | 从剪贴板粘贴路径或拖拽文件夹时自动校验合法性 |
| 全局异常兜底 | UI 线程 + 非 UI 线程 + Task 未观察异常三层捕获，崩溃时弹友好提示 |

### 现有功能增强

| 功能 | v1.0.0 | v2.0.0 |
|------|--------|--------|
| RobocopyService | 直接启动进程，日志拼接在 code-behind | 抽象为接口，支持 CancellationToken 取消，日志用 StringBuilder 带节流（100ms） |
| 进度显示 | 刷新无节流 | 100ms 节流 + 200K 字符日志上限（防内存膨胀） |
| 数据绑定 | 硬编码属性赋值 | 全部通过 `INotifyPropertyChanged` + `RelayCommand` 绑定 |
| 参数构造 | 字符串拼接 | 通过 `BuildRobocopyArgs()` 方法统一构造，逻辑可测试 |

### 安全性与健壮性

| 方面 | v1.0.0 | v2.0.0 |
|------|--------|--------|
| 路径校验 | 无 | `PathValidator` 静态类，含路径规范化 / 存在性检查 / 死循环检测 |
| 容错 | 无 | 设置文件损坏后自动恢复默认值；拷贝异常弹友好提示 |
| 取消支持 | 无 | 通过 `CancellationTokenSource` 支持用户中途取消 |

### 质量保障（新增）

不再是手动测试——现在有**自动化的单元测试**：

| 测试文件 | 行数 | 测试范围 |
|---------|------|---------|
| `MainViewModelTests.cs` | 407 | ViewModel 状态机、场景切换、路径设置、启动取消流程 |
| `PathValidatorTests.cs` | 84 | 合法/非法路径、边界案例 |
| `RobocopyOutputParserTests.cs` | 88 | 中英文 robocopy 输出行解析 |

---

## 文件清单

### 新增（18 个文件）

```
Converters/BrushKeyConverter.cs          # 画笔键值转换器
Models/AppSettings.cs                     # 设置持久化模型
Parsing/RobocopyOutputParser.cs           # Robocopy 输出解析器
Validation/PathValidator.cs               # 路径校验器
ViewModels/MainViewModel.cs               # 主窗口 ViewModel（784 行）
ViewModels/RelayCommand.cs               # ICommand 实现
ViewModels/ViewModelBase.cs              # INotifyPropertyChanged 基类
Services/ConfigurationService.cs          # JSON 设置持久化
Services/IConfigurationService.cs         # 设置服务接口
Services/IDialogService.cs               # 对话框服务接口
Services/IRobocopyService.cs             # Robocopy 服务接口
Services/DialogService.cs                # 对话框 WPF 实现
RoboCopyGUI.Tests/MainViewModelTests.cs  # ViewModel 单元测试
RoboCopyGUI.Tests/PathValidatorTests.cs   # 路径校验测试
RoboCopyGUI.Tests/RobocopyOutputParserTests.cs  # 输出解析测试
RoboCopyGUI.Tests/RoboCopyGUI.Tests.csproj  # 测试项目文件
RoboCopyGUI.sln                          # 解决方案文件
```

### 重构（9 个文件）

| 文件 | 变化 |
|------|------|
| `.gitignore` | 更新匹配新项目结构 |
| `App.xaml` | 新增全局资源 |
| `App.xaml.cs` | 加入 DI 容器 + 三层异常兜底 |
| `MainWindow.xaml` | 绑定全部切换到 ViewModel，新增高级参数面板 UI |
| `MainWindow.xaml.cs` | ~1400 行→~300 行，仅保留 UI 事件转发和深色标题栏 |
| `Models/CopyResult.cs` | 新增字段 |
| `Models/PresetProfile.cs` | 微调 |
| `Services/RobocopyService.cs` | 实现 `IRobocopyService`，加取消支持 |
| `RoboCopyGUI.csproj` | 更新 TargetFramework 和包引用 |

---

## 行为变更说明

| 场景 | v1.0.0 行为 | v2.0.0 行为 |
|------|------------|------------|
| 首次启动 | 空窗口 | 窗口居中，默认选中场景 0，加载默认参数 |
| 关闭再打开 | 不记忆任何东西 | 恢复上次的路径、场景、窗口位置和大小 |
| 覆盖已有目标 | 直接执行 | 弹双重确认（黄→红，默认 No） |
| 源路径不存在 | 开始后报 robocopy 错误 | 开始前即校验并弹提示 |
| 复制中途取消 | 只能关窗口 | 点击取消按钮，优雅终止（等待当前文件完成） |
| 程序崩溃 | Windows 标准错误弹窗 | 友好提示 + 允许复制错误详情 |

---

## 已知问题

- 深色标题栏要求 Windows 10 1809+ 或 Windows 11
- 日志剪裁上限为 200K 字符，极大规模拷贝可能截断早期输出
- 设置文件保存在 `%AppData%\RoboCopyGUI\settings.json`，多实例共享

---

## 下载

从 [GitHub Releases](https://github.com/zzb-hu/RoboCopyGUI/releases/tag/v2.0.0) 下载最新编译版本。
