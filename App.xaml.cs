namespace RoboCopyGUI;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // 全局兜底：UI 线程未捕获异常
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        // 全局兜底：非 UI 线程未捕获异常
        System.AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
        // 全局兜底：Task 中未观察的异常
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        // 手动 DI（不引 Microsoft.Extensions.DependencyInjection 包，保持零 NuGet 依赖）
        var dialogService = new Services.DialogService();
        var configService = new Services.ConfigurationService();
        var robocopyService = new Services.RobocopyService();
        var viewModel = new ViewModels.MainViewModel(robocopyService, dialogService, configService);

        // 恢复持久化设置（最近路径、场景记忆、高级参数）
        var settings = configService.Load();
        viewModel.ApplySettings(settings);

        var mainWindow = new MainWindow(viewModel);
        // 恢复窗口尺寸和位置
        mainWindow.Width = settings.WindowWidth;
        mainWindow.Height = settings.WindowHeight;
        if (!double.IsNaN(settings.WindowLeft)) mainWindow.Left = settings.WindowLeft;
        if (!double.IsNaN(settings.WindowTop)) mainWindow.Top = settings.WindowTop;
        mainWindow.Show();
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        ShowCrash(e.Exception);
        e.Handled = true;
    }

    private void AppDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is System.Exception ex)
            ShowCrash(ex);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        ShowCrash(e.Exception);
        e.SetObserved();
    }

    private static void ShowCrash(System.Exception ex)
    {
        try
        {
            System.Windows.MessageBox.Show(
                $"程序出了点意外，但已经处理。\n\n错误：{ex.Message}\n\n详情：\n{ex.StackTrace}",
                "RoboCopyGUI 异常",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        catch { /* 极端情况下连 MessageBox 都打不开，只能吞掉 */ }
    }
}
