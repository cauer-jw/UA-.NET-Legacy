/*
* Copyright (c) 2005-2026 - OPC Foundation
* 
* All Rights Reserved.
* 
* NOTICE:  All information contained herein is, and remains the property of 
* OPC Foundation. The intellectual and technical concepts contained 
* herein are proprietary to OPC Foundation and may be covered by 
* U.S. and Foreign Patents, patents in process, and are protected by trade secret 
* or copyright law. Dissemination of this information or reproduction of this 
* material is strictly forbidden unless prior written permission is obtained 
* from OPC Foundation.
*/

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Opc.Ua.Com.Client
{
    internal static class ServicePostInstallConfigurator
    {
        private const uint SC_MANAGER_ALL_ACCESS = 0xF003F;
        private const uint SERVICE_ALL_ACCESS = 0xF01FF;
        private const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;
        private const uint SERVICE_AUTO_START = 0x00000002;

        private const int SERVICE_CONFIG_FAILURE_ACTIONS = 2;
        private const int SERVICE_CONFIG_DELAYED_AUTO_START_INFO = 3;
        private const int SERVICE_CONFIG_FAILURE_ACTIONS_FLAG = 4;

        private const int SC_ACTION_RESTART = 1;
        private const uint FailureResetPeriodSeconds = 24 * 60 * 60;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenSCManagerW(string machineName, string databaseName, uint desiredAccess);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr OpenServiceW(IntPtr scManager, string serviceName, uint desiredAccess);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeServiceConfigW(
            IntPtr service,
            uint serviceType,
            uint startType,
            uint errorControl,
            string binaryPathName,
            string loadOrderGroup,
            IntPtr tagId,
            string dependencies,
            string serviceStartName,
            string password,
            string displayName);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeServiceConfig2W(IntPtr service, int infoLevel, IntPtr info);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseServiceHandle(IntPtr serviceHandle);

        [StructLayout(LayoutKind.Sequential)]
        private struct SC_ACTION
        {
            public int Type;
            public uint Delay;
        }

        // lpRebootMsg and lpCommand are always NULL — use IntPtr to avoid string marshaling side effects.
        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_FAILURE_ACTIONS
        {
            public uint dwResetPeriod;
            public IntPtr lpRebootMsg;
            public IntPtr lpCommand;
            public uint cActions;
            public IntPtr lpsaActions;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_FAILURE_ACTIONS_FLAG
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool fFailureActionsOnNonCrashFailures;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SERVICE_DELAYED_AUTO_START_INFO
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool fDelayedAutostart;
        }

        public static void ConfigureForProduction(string serviceName)
        {
            if (String.IsNullOrEmpty(serviceName))
            {
                throw new ArgumentNullException("serviceName");
            }

            // Recovery actions and failure flags require a full administrator token.
            bool isElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                .IsInRole(WindowsBuiltInRole.Administrator);

            if (!isElevated)
            {
                throw new InvalidOperationException(
                    "Configuring service recovery actions requires an elevated administrator process. " +
                    "Run /install from a command prompt or PowerShell opened as Administrator.");
            }

            IntPtr scManager = IntPtr.Zero;
            IntPtr service = IntPtr.Zero;
            IntPtr delayedAutoStartPtr = IntPtr.Zero;
            IntPtr failureActionsPtr = IntPtr.Zero;
            IntPtr actionsPtr = IntPtr.Zero;
            IntPtr failureActionsFlagPtr = IntPtr.Zero;

            try
            {
                scManager = OpenSCManagerW(null, null, SC_MANAGER_ALL_ACCESS);

                if (scManager == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open the service control manager.");
                }

                service = OpenServiceW(scManager, serviceName, SERVICE_ALL_ACCESS);

                if (service == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), String.Format("Could not open service '{0}'.", serviceName));
                }

                if (!ChangeServiceConfigW(service, SERVICE_NO_CHANGE, SERVICE_AUTO_START, SERVICE_NO_CHANGE, null, null, IntPtr.Zero, null, null, null, null))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), String.Format("Could not set the start mode for service '{0}'.", serviceName));
                }

                SERVICE_DELAYED_AUTO_START_INFO delayedAutoStartInfo = new SERVICE_DELAYED_AUTO_START_INFO();
                delayedAutoStartInfo.fDelayedAutostart = true;
                delayedAutoStartPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SERVICE_DELAYED_AUTO_START_INFO)));
                Marshal.StructureToPtr(delayedAutoStartInfo, delayedAutoStartPtr, false);

                if (!ChangeServiceConfig2W(service, SERVICE_CONFIG_DELAYED_AUTO_START_INFO, delayedAutoStartPtr))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), String.Format("Could not enable delayed auto start for service '{0}'.", serviceName));
                }

                SC_ACTION[] actions = new SC_ACTION[]
                {
                    new SC_ACTION { Type = SC_ACTION_RESTART, Delay = 10000 },
                    new SC_ACTION { Type = SC_ACTION_RESTART, Delay = 30000 },
                    new SC_ACTION { Type = SC_ACTION_RESTART, Delay = 60000 }
                };

                int actionSize = Marshal.SizeOf(typeof(SC_ACTION));
                actionsPtr = Marshal.AllocHGlobal(actionSize * actions.Length);

                for (int ii = 0; ii < actions.Length; ii++)
                {
                    Marshal.StructureToPtr(actions[ii], IntPtr.Add(actionsPtr, ii * actionSize), false);
                }

                SERVICE_FAILURE_ACTIONS failureActions = new SERVICE_FAILURE_ACTIONS();
                failureActions.dwResetPeriod = FailureResetPeriodSeconds;
                failureActions.cActions = (uint)actions.Length;
                failureActions.lpsaActions = actionsPtr;

                failureActionsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SERVICE_FAILURE_ACTIONS)));
                Marshal.StructureToPtr(failureActions, failureActionsPtr, false);

                if (!ChangeServiceConfig2W(service, SERVICE_CONFIG_FAILURE_ACTIONS, failureActionsPtr))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, String.Format("Could not configure recovery actions for service '{0}'. Win32 error {1}: {2}", serviceName, err, new Win32Exception(err).Message));
                }

                SERVICE_FAILURE_ACTIONS_FLAG failureActionsFlag = new SERVICE_FAILURE_ACTIONS_FLAG();
                failureActionsFlag.fFailureActionsOnNonCrashFailures = true;
                failureActionsFlagPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SERVICE_FAILURE_ACTIONS_FLAG)));
                Marshal.StructureToPtr(failureActionsFlag, failureActionsFlagPtr, false);

                if (!ChangeServiceConfig2W(service, SERVICE_CONFIG_FAILURE_ACTIONS_FLAG, failureActionsFlagPtr))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new Win32Exception(err, String.Format("Could not enable failure actions for error stops on service '{0}'. Win32 error {1}: {2}", serviceName, err, new Win32Exception(err).Message));
                }
            }
            finally
            {
                FreeStructurePointer<SERVICE_FAILURE_ACTIONS_FLAG>(failureActionsFlagPtr);
                FreeStructurePointer<SERVICE_FAILURE_ACTIONS>(failureActionsPtr);
                FreePointer(actionsPtr);
                FreeStructurePointer<SERVICE_DELAYED_AUTO_START_INFO>(delayedAutoStartPtr);
                CloseHandle(service);
                CloseHandle(scManager);
            }
        }

        private static void CloseHandle(IntPtr handle)
        {
            if (handle != IntPtr.Zero)
            {
                CloseServiceHandle(handle);
            }
        }

        private static void FreePointer(IntPtr pointer)
        {
            if (pointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        private static void FreeStructurePointer<T>(IntPtr pointer)
        {
            if (pointer != IntPtr.Zero)
            {
                Marshal.DestroyStructure(pointer, typeof(T));
                Marshal.FreeHGlobal(pointer);
            }
        }
    }
}