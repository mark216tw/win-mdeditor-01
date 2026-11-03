using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using VibeCode.MdEditor.Core.Helpers;
using VibeCode.MdEditor.Core.Models;

namespace VibeCode.MdEditor.Services;

public sealed class FileAssociationService : IFileAssociationService
{
    private readonly ILogger<FileAssociationService> _logger;

    public FileAssociationService(ILogger<FileAssociationService> logger)
    {
        _logger = logger;
        ExecutablePath = ResolveExecutablePath();
    }

    public string ExecutablePath { get; }

    public bool IsRegistered
    {
        get
        {
            try
            {
                var expectedCommand = FileAssociationRegistration.BuildOpenCommand(ExecutablePath);

                using var commandKey = Registry.CurrentUser.OpenSubKey(FileAssociationRegistration.CommandPath);
                if (commandKey?.GetValue(null) is not string actualCommand ||
                    !string.Equals(actualCommand, expectedCommand, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                using var registeredKey = Registry.CurrentUser.OpenSubKey(
                    FileAssociationRegistration.RegisteredApplicationsPath);
                var capabilities = registeredKey?.GetValue(FileAssociationRegistration.RegisteredAppName) as string;

                return string.Equals(
                    capabilities,
                    FileAssociationRegistration.CapabilitiesPath,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
            {
                _logger.LogWarning(ex, "讀取檔案關聯狀態失敗。");
                return false;
            }
        }
    }

    public void Register()
    {
        foreach (var entry in FileAssociationRegistration.CreateRegistrationPlan(ExecutablePath))
        {
            using var key = Registry.CurrentUser.CreateSubKey(entry.KeyPath, writable: true)
                ?? throw new InvalidOperationException($"無法建立登錄機碼：{entry.KeyPath}");

            key.SetValue(entry.ValueName, entry.Value, RegistryValueKind.String);
        }

        _logger.LogInformation("已登錄 Markdown 檔案關聯：{Executable}", ExecutablePath);
    }

    public void Unregister()
    {
        foreach (var reference in FileAssociationRegistration.CreateValueRemovalPlan())
        {
            RemoveValue(reference);
        }

        foreach (var keyPath in FileAssociationRegistration.CreateKeyRemovalPlan())
        {
            Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
        }

        _logger.LogInformation("已解除 Markdown 檔案關聯。");
    }

    public void OpenDefaultAppsSettings()
    {
        try
        {
            var clsid = new Guid("1968106D-F3B5-44CF-890E-116FCB9ECEF1");
            var comType = Type.GetTypeFromCLSID(clsid)
                ?? throw new InvalidOperationException("找不到預設應用程式設定介面。");
            var instance = Activator.CreateInstance(comType)
                ?? throw new InvalidOperationException("無法建立預設應用程式設定介面。");
            var registrationUi = (IApplicationAssociationRegistrationUI)instance;
            registrationUi.LaunchAdvancedAssociationUI(FileAssociationRegistration.RegisteredAppName);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "無法透過 COM 開啟預設應用程式設定，改用 ms-settings。");
        }

        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:defaultapps")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "開啟 Windows 預設應用程式設定失敗。");
            throw;
        }
    }

    private static void RemoveValue(RegistryValueReference reference)
    {
        bool removeKeyIfEmpty;

        using (var key = Registry.CurrentUser.OpenSubKey(reference.KeyPath, writable: true))
        {
            if (key is null)
            {
                return;
            }

            key.DeleteValue(reference.ValueName, throwOnMissingValue: false);
            removeKeyIfEmpty = reference.RemoveKeyIfEmpty && key.SubKeyCount == 0 && key.ValueCount == 0;
        }

        if (removeKeyIfEmpty)
        {
            Registry.CurrentUser.DeleteSubKey(reference.KeyPath, throwOnMissingSubKey: false);
        }
    }

    private static string ResolveExecutablePath()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Path.Combine(AppContext.BaseDirectory, FileAssociationRegistration.ExecutableName);
        }

        return Path.GetFullPath(path);
    }

    [ComImport]
    [Guid("1F76A169-F994-40AC-8FC8-0959E8874710")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationAssociationRegistrationUI
    {
        void LaunchAdvancedAssociationUI([MarshalAs(UnmanagedType.LPWStr)] string applicationRegistrationName);
    }
}
