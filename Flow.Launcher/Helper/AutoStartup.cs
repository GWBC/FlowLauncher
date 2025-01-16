using System;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;

namespace Flow.Launcher.Helper;

public class AutoStartup
{
    private const string TaskName = @"FlowLauncher";
    private const string StartupPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled
    {
        get
        {
            //try
            //{
            //    using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
            //    var path = key?.GetValue(Constant.FlowLauncher) as string;
            //    return path == Constant.ExecutablePath;
            //}
            //catch (Exception e)
            //{
            //    Log.Error("AutoStartup", $"Ignoring non-critical registry error (querying if enabled): {e}");
            //}

            //return false;

            try
            {
                using (TaskService ts = new TaskService())
                {
                    return ts.GetTask(TaskName) != null;
                }
            }
            catch (Exception e)
            {
                Log.Error("AutoStartup", $"Ignoring non-critical registry error (querying if enabled): {e}");
            }

            return false; 
        }
    }

    public static void Disable()
    {
        //try
        //{
        //    using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
        //    key?.DeleteValue(Constant.FlowLauncher, false);
        //}
        //catch (Exception e)
        //{
        //    Log.Error("AutoStartup", $"Failed to disable auto-startup: {e}");
        //    throw;
        //}

        try
        {
            using (TaskService ts = new TaskService())
            {
                ts.RootFolder.DeleteTask(TaskName);
            }
        }
        catch (Exception e)
        {
            Log.Error("AutoStartup", $"Failed to disable auto-startup: {e}");
            throw;
        }
    }

    internal static void Enable()
    {
        //try
        //{
        //    using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
        //    key?.SetValue(Constant.FlowLauncher, $"\"{Constant.ExecutablePath}\"");
        //}
        //catch (Exception e)
        //{
        //    Log.Error("AutoStartup", $"Failed to enable auto-startup: {e}");
        //    throw;
        //}

        try
        {
            using (TaskService ts = new TaskService())
            {
                TaskDefinition td = ts.NewTask();
                td.RegistrationInfo.Description = "Flow.Launcher Autorun";

                td.Triggers.Add(new LogonTrigger());
                td.Actions.Add(new ExecAction(Constant.ExecutablePath,
                    null, Constant.ProgramDirectory));
                td.Principal.RunLevel = TaskRunLevel.Highest;
                ts.RootFolder.RegisterTaskDefinition(TaskName, td);
            }
        }
        catch (Exception e)
        {
            Log.Error("AutoStartup", $"Failed to enable auto-startup: {e}");
            throw;
        }
    }
}
