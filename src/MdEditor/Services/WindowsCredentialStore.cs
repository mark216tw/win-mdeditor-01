using System.Runtime.InteropServices;
using System.Text;
using VibeCode.MdEditor.Core.Services;

namespace VibeCode.MdEditor.Services;

public sealed class WindowsCredentialStore : ITranslationCredentialStore
{
    private const string TargetName = "VibeCode.MdEditor/TranslationApiKey";
    private const string UserName = "ApiKey";
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;

    public Task<string?> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!CredReadW(TargetName, CredentialTypeGeneric, 0, out var credentialPointer))
        {
            return Task.FromResult<string?>(null);
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return Task.FromResult<string?>(null);
            }

            var size = checked((int)credential.CredentialBlobSize);
            var bytes = new byte[size];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, size);
            return Task.FromResult<string?>(Encoding.Unicode.GetString(bytes));
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public Task SetApiKeyAsync(string? apiKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            CredDeleteW(TargetName, CredentialTypeGeneric, 0);
            return Task.CompletedTask;
        }

        var bytes = Encoding.Unicode.GetBytes(apiKey.Trim());
        var blob = Marshal.AllocHGlobal(bytes.Length);

        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);

            var credential = new NativeCredential
            {
                Type = CredentialTypeGeneric,
                TargetName = TargetName,
                Comment = "MiniMdEditor translation API key",
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = CredentialPersistLocalMachine,
                UserName = UserName,
            };

            if (!CredWriteW(ref credential, 0))
            {
                throw new InvalidOperationException(
                    $"無法儲存翻譯 API Key（Win32 錯誤 {Marshal.GetLastWin32Error()}）。");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blob);
        }

        return Task.CompletedTask;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredReadW(
        string target,
        uint type,
        uint reservedFlag,
        out IntPtr credentialPointer);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWriteW(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;

        public uint Type;

        public string? TargetName;

        public string? Comment;

        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;

        public uint CredentialBlobSize;

        public IntPtr CredentialBlob;

        public uint Persist;

        public uint AttributeCount;

        public IntPtr Attributes;

        public string? TargetAlias;

        public string? UserName;
    }
}
