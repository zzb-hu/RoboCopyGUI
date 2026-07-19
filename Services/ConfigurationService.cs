using System.IO;
using System.Text;
using System.Text.Json;
using RoboCopyGUI.Models;

namespace RoboCopyGUI.Services;

/// <summary>
/// IConfigurationService 的 JSON 文件实现。
/// 持久化到 %AppData%\RoboCopyGUI\settings.json。
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RoboCopyGUI");

    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return new AppSettings();

            var json = File.ReadAllText(SettingsFile, Encoding.UTF8);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            // 任何读取/反序列化失败都返回默认值，不让程序因为设置文件坏了就启动不了
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFile, json, Encoding.UTF8);
        }
        catch
        {
            // 保存失败静默吞掉：设置持久化是锦上添花，不应影响主流程
        }
    }
}
