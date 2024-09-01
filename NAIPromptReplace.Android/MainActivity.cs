using System;
using System.Collections.Generic;
using Android;
using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Avalonia;
using Avalonia.Android;
using Avalonia.ReactiveUI;

namespace NAIPromptReplace.Android;

[Activity(
    Label = "NovelAI Prompt Replace",
    Theme = "@style/Theme.AppCompat.NoActionBar",
    Icon = "@drawable/Icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<AndroidApp>
{
    public static MainActivity Instance = null!;
    public event EventHandler? Stopped;
    public event EventHandler<Orientation>? OrientationChanged;

    public MainActivity()
    {
        Instance = this;
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .UseReactiveUI()
            .LogToTrace()
            .WithInterFont();
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        new AlertDialog.Builder(this).SetMessage(@"接下来将申请存储权限，如果有弹窗请同意
由于安卓存储权限的限制每个任务必须选择一个输出目录
输出目录留空会报错，暂不支持手动填写路径。
第一次写安卓程序，所以你可能会遇到包括但不限于：排版错乱，莫名报错/闪退，配置不保存，请谅解").SetNeutralButton("OK", (_, _) =>
        {
            List<string> permissions = [Manifest.Permission.ReadExternalStorage, Manifest.Permission.WriteExternalStorage];

            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                permissions.Add(Manifest.Permission.ManageExternalStorage);
            }

            requestPermissions(permissions);
        }).Show();
    }

    private void requestPermissions(List<string> permissions)
    {
        foreach (string permission in permissions)
        {
            if (ContextCompat.CheckSelfPermission(ApplicationContext, permission) == Permission.Denied)
            {
                ActivityCompat.RequestPermissions(Instance, [permission], 1337);
            }
        }
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        OrientationChanged?.Invoke(this, newConfig.Orientation);
    }

    protected override void OnStop()
    {
        Stopped?.Invoke(this, null);
        OrientationChanged = null;
        base.OnStop();
    }
}
