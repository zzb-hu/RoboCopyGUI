using RoboCopyGUI.Models;

namespace RoboCopyGUI.Services;

/// <summary>
/// 应用设置持久化抽象。把"场景/窗口尺寸/高级参数"等用户偏好
/// 保存到磁盘，下次启动自动恢复。
/// </summary>
public interface IConfigurationService
{
    /// <summary>加载设置。文件不存在时返回默认值。</summary>
    AppSettings Load();

    /// <summary>保存设置到磁盘。</summary>
    void Save(AppSettings settings);
}
