#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace BrickDefense.Infrastructure
{
    // Native modal-owner and cancellation mechanism compared with UnityStandaloneFileBrowser.
    // The Unicode ABI and flags follow Microsoft's OPENFILENAMEW contract.
    internal static class WindowsPhotoFilePicker
    {
        private const int FileCapacity=32768;
        private const uint Explorer=0x00080000,FileMustExist=0x00001000,PathMustExist=0x00000800,
            NoChangeDirectory=0x00000008,HideReadOnly=0x00000004;

        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
        private struct OpenFileName
        {
            public uint Size;
            public IntPtr Owner,Instance;
            public string Filter;
            public IntPtr CustomFilter;
            public uint MaxCustomFilter,FilterIndex;
            public IntPtr File;
            public uint MaxFile;
            public IntPtr FileTitle;
            public uint MaxFileTitle;
            public string InitialDirectory,Title;
            public uint Flags;
            public ushort FileOffset,FileExtension;
            public string DefaultExtension;
            public IntPtr CustomData,Hook,TemplateName,Reserved;
            public uint ReservedValue,ExtendedFlags;
        }

        [DllImport("comdlg32.dll",EntryPoint="GetOpenFileNameW",CharSet=CharSet.Unicode,ExactSpelling=true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetOpenFileName(ref OpenFileName dialog);
        [DllImport("comdlg32.dll",ExactSpelling=true)]
        private static extern uint CommDlgExtendedError();
        [DllImport("user32.dll",ExactSpelling=true)]
        private static extern IntPtr GetActiveWindow();

        public static string Pick(string previousPath)
        {
            IntPtr fileBuffer=Marshal.AllocHGlobal(FileCapacity*2);
            try
            {
                Marshal.WriteInt16(fileBuffer,0);
                IntPtr owner=GetActiveWindow();
                if(owner==IntPtr.Zero)
                    using(var process=Process.GetCurrentProcess()) owner=process.MainWindowHandle;
                var dialog=new OpenFileName {
                    Size=(uint)Marshal.SizeOf<OpenFileName>(),Owner=owner,
                    Filter="积木照片 (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0\0",FilterIndex=1,
                    File=fileBuffer,MaxFile=FileCapacity,InitialDirectory=InitialDirectory(previousPath),
                    Title="选择积木照片",Flags=Explorer|FileMustExist|PathMustExist|NoChangeDirectory|HideReadOnly
                };
                if(GetOpenFileName(ref dialog)) return Marshal.PtrToStringUni(fileBuffer);
                uint error=CommDlgExtendedError();
                if(error!=0) throw new IOException($"无法打开照片选择窗口（错误 0x{error:X}），请重试。");
                return string.Empty;
            }
            finally { Marshal.FreeHGlobal(fileBuffer); }
        }

        private static string InitialDirectory(string previousPath)
        {
            if(!string.IsNullOrEmpty(previousPath))
            {
                string directory=Path.GetDirectoryName(previousPath);
                if(Directory.Exists(directory)) return directory;
            }
            string downloads=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");
            return Directory.Exists(downloads)?downloads:Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        }
    }
}
#endif
