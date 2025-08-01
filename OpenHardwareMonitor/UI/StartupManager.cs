﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OpenHardwareMonitor.UI
{
    public class StartupManager
    {

        private readonly TaskSchedulerClass _scheduler;
        private bool _startup;
        private const string REGISTRY_RUN = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public StartupManager()
        {
            if (OperatingSystemHelper.IsUnix)
            {
                _scheduler = null;
                IsAvailable = false;
                return;
            }

            if (OperatingSystemHelper.IsAdministrator())
            {
                try
                {
                    _scheduler = new TaskSchedulerClass();
                    _scheduler.Connect(null, null, null, null);
                }
                catch
                {
                    _scheduler = null;
                }

                if (_scheduler != null)
                {
                    try
                    {
                        try
                        {
                            // check if the taskscheduler is running
                            IRunningTaskCollection collection = _scheduler.GetRunningTasks(0);
                        }
                        catch (ArgumentException) { }

                        ITaskFolder folder = _scheduler.GetFolder("\\" + Updater.ApplicationTitle);
                        IRegisteredTask task = folder.GetTask("Startup");
                        _startup = (task != null) &&
                          (task.Definition.Triggers.Count > 0) &&
                          (task.Definition.Triggers[1].Type ==
                            TASK_TRIGGER_TYPE2.TASK_TRIGGER_LOGON) &&
                          (task.Definition.Actions.Count > 0) &&
                          (task.Definition.Actions[1].Type ==
                            TASK_ACTION_TYPE.TASK_ACTION_EXEC) &&
                          (task.Definition.Actions[1] as IExecAction != null) &&
                          ((task.Definition.Actions[1] as IExecAction).Path ==
                            Application.ExecutablePath);

                    }
                    catch (IOException)
                    {
                        _startup = false;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        _scheduler = null;
                    }
                    catch (COMException)
                    {
                        _scheduler = null;
                    }
                    catch (NotImplementedException)
                    {
                        _scheduler = null;
                    }
                }
            }
            else
            {
                _scheduler = null;
            }

            if (_scheduler == null)
            {
                try
                {
                    using (RegistryKey key =
                      Registry.CurrentUser.OpenSubKey(REGISTRY_RUN))
                    {
                        _startup = false;
                        if (key != null)
                        {
                            string value = (string)key.GetValue("OpenHardwareMonitor");
                            if (value != null)
                                _startup = value == Application.ExecutablePath;
                        }
                    }
                    IsAvailable = true;
                }
                catch (SecurityException)
                {
                    IsAvailable = false;
                }
            }
            else
            {
                IsAvailable = true;
            }
        }

        private void CreateSchedulerTask()
        {
            ITaskDefinition definition = _scheduler.NewTask(0);
            definition.RegistrationInfo.Description = $"This task starts the {Updater.ApplicationTitle} on Windows startup.";
            definition.Principal.RunLevel = TASK_RUNLEVEL.TASK_RUNLEVEL_HIGHEST;
            definition.Settings.DisallowStartIfOnBatteries = false;
            definition.Settings.StopIfGoingOnBatteries = false;
            definition.Settings.ExecutionTimeLimit = "PT0S";
            var trigger = (ILogonTrigger)definition.Triggers.Create(TASK_TRIGGER_TYPE2.TASK_TRIGGER_LOGON);
            trigger.UserId = WindowsIdentity.GetCurrent().Name;
            IExecAction action = (IExecAction)definition.Actions.Create(TASK_ACTION_TYPE.TASK_ACTION_EXEC);
            action.Path = Application.ExecutablePath;
            action.WorkingDirectory = Path.GetDirectoryName(Application.ExecutablePath);

            ITaskFolder root = _scheduler.GetFolder("\\");
            ITaskFolder folder;
            try
            {
                folder = root.GetFolder(Updater.ApplicationTitle);
            }
            catch (IOException)
            {
                folder = root.CreateFolder(Updater.ApplicationTitle, "");
            }
            folder.RegisterTaskDefinition("Startup", definition,
              (int)TASK_CREATION.TASK_CREATE_OR_UPDATE, null, null,
              TASK_LOGON_TYPE.TASK_LOGON_INTERACTIVE_TOKEN, "");
        }

        private void DeleteSchedulerTask()
        {
            ITaskFolder root = _scheduler.GetFolder("\\");
            try
            {
                ITaskFolder folder = root.GetFolder(Updater.ApplicationTitle);
                folder.DeleteTask("Startup", 0);
            }
            catch (IOException) { }
            try
            {
                root.DeleteFolder(Updater.ApplicationTitle, 0);
            }
            catch (IOException) { }
        }

        private void CreateRegistryRun()
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_RUN);
            key.SetValue("OpenHardwareMonitor", Application.ExecutablePath);
        }

        private void DeleteRegistryRun()
        {
            RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_RUN);
            key.DeleteValue("OpenHardwareMonitor");
        }

        public bool IsAvailable { get; }

        public bool Startup
        {
            get
            {
                return _startup;
            }
            set
            {
                if (_startup != value)
                {
                    if (IsAvailable)
                    {
                        if (_scheduler != null)
                        {
                            if (value)
                                CreateSchedulerTask();
                            else
                                DeleteSchedulerTask();
                            _startup = value;
                        }
                        else
                        {
                            try
                            {
                                if (value)
                                    CreateRegistryRun();
                                else
                                    DeleteRegistryRun();
                                _startup = value;
                            }
                            catch (UnauthorizedAccessException)
                            {
                                throw new InvalidOperationException();
                            }
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
        }
    }
}
