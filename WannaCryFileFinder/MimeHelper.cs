using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace WannaCryFileFinder
{
    public static class MimeHelper
    {
        private const string UNKNOWN_EXTENSION = ".txt";
        private const string EMPTY_EXTENSION = ".empty";
        private const string MSOFFICE_EXTENSION = ".officeDocument";

        private static readonly Regex docRegex = new Regex("576F72642E446F63756D656E742E", RegexOptions.Compiled);
        private static readonly Regex xlsRegex = new Regex("4D6963726F736F667420457863656C00", RegexOptions.Compiled);
        private static readonly Regex pptRegex = new Regex("4D5320506F776572506F696E74", RegexOptions.Compiled);

        private static Dictionary<string, Regex> knownHeaders = new Dictionary<string, Regex>()
        {
            { ".7z",new Regex("^377ABCAF271C", RegexOptions.Compiled) },
            { MSOFFICE_EXTENSION,new Regex("^D0CF11E0A1B11AE1", RegexOptions.Compiled) },
            { ".dpx",new Regex("^53445058" , RegexOptions.Compiled)},
            { ".jpg",new Regex("^4A464946", RegexOptions.Compiled) },
            { ".pdf",new Regex("^25504446" , RegexOptions.Compiled)},
            { ".png",new Regex("^89504E470D0A1A0A" , RegexOptions.Compiled)},
            { ".ps",new Regex("^25215053", RegexOptions.Compiled) },
            { ".psd",new Regex("^38425053", RegexOptions.Compiled) },
            { ".rar",new Regex("^526172211A0700" , RegexOptions.Compiled)},
            {".tif",new Regex("^49492A00", RegexOptions.Compiled)},
            {".vsdx",new Regex("^504B0708", RegexOptions.Compiled)},
            {".wav",new Regex("^52494646", RegexOptions.Compiled)},
            {".wma",new Regex("^A6D900AA0062CE6C", RegexOptions.Compiled)},
            {".epub",new Regex("^504B03040A000200", RegexOptions.Compiled)},
            {".zip",new Regex("^504B0304", RegexOptions.Compiled)},
            {".rtf",new Regex("^7B5C72746631", RegexOptions.Compiled)},
            {".bmp",new Regex("^424D", RegexOptions.Compiled)}
        };

        [DllImport(@"urlmon.dll", CharSet = CharSet.Auto)]
        private extern static System.UInt32 FindMimeFromData(
            System.UInt32 pBC,
            [MarshalAs(UnmanagedType.LPStr)] System.String pwzUrl,
            [MarshalAs(UnmanagedType.LPArray)] byte[] pBuffer,
            System.UInt32 cbSize,
            [MarshalAs(UnmanagedType.LPStr)] System.String pwzMimeProposed,
            System.UInt32 dwMimeFlags,
            out System.UInt32 ppwzMimeOut,
            System.UInt32 dwReserverd
        );

        private static string GetMimeFromFileBinary(byte[] fileBinary)
        {
            try
            {
                System.UInt32 mimetype;
                FindMimeFromData(0, null, fileBinary, (uint)fileBinary.Length, null, 0, out mimetype, 0);
                System.IntPtr mimeTypePtr = new IntPtr(mimetype);
                string mime = Marshal.PtrToStringUni(mimeTypePtr);
                Marshal.FreeCoTaskMem(mimeTypePtr);
                return mime;
            }
            catch (Exception)
            {
                return "unknown/unknown";
            }
        }

        public static string GetExtensionByContent(string filePath)
        {
            using (FileStream file = File.OpenRead(filePath))
            {
                byte[] fileHeader = new byte[256];
                int readedBytes = file.Read(fileHeader, 0, 256);
                if (readedBytes > 0)
                {
                    Array.Resize(ref fileHeader, readedBytes);
                    string mimeType = GetMimeFromFileBinary(fileHeader).ToLowerInvariant();
                    switch (mimeType)
                    {
                        case "application/x-zip-compressed":
                            return GetExtensionFromZip(file);
                        case "application/octet-stream":
                            string ext = GetExtensionByKnownHeader(fileHeader);
                            if (ext.Equals(MSOFFICE_EXTENSION))
                            {
                                ext = GetExtensionFromMSOffice(file);
                            }
                            return ext;
                        default:
                            return GetExtensionByMime(mimeType);
                    }
                }
                else
                {
                    return EMPTY_EXTENSION;
                }
            }
        }

        private static string GetExtensionByMime(string mimeType)
        {
            RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"MIME\Database\Content Type\" + mimeType, false);
            object value = key != null ? key.GetValue("Extension", null) : null;
            string result = value != null ? value.ToString() : UNKNOWN_EXTENSION;
            return result;
        }

        private static string GetExtensionByKnownHeader(byte[] fileHeader)
        {
            foreach (var header in knownHeaders)
            {
                if (header.Value.IsMatch(BitConverter.ToString(fileHeader).Replace("-", "")))
                {
                    return header.Key;
                }
            }
            return UNKNOWN_EXTENSION;
        }

        private static string GetExtensionFromMSOffice(FileStream file)
        {
            // string content = BitConverter.ToString(ReadAllBytes(file)).Replace("-", "");
            string content = String.Empty;
            using (MemoryStream ms = new MemoryStream())
            {
                file.CopyTo(ms);
                content = BitConverter.ToString(ms.ToArray()).Replace("-", "");
            }
            if (docRegex.IsMatch(content))
            {
                return ".doc";
            }
            if (xlsRegex.IsMatch(content))
            {
                return ".xls";
            }
            else if (pptRegex.IsMatch(content))
            {
                return ".ppt";
            }
            else
            {
                return MSOFFICE_EXTENSION;
            }
        }

        private static string GetExtensionFromZip(FileStream file)
        {
            using (System.IO.Compression.ZipArchive zip = new System.IO.Compression.ZipArchive(file))
            {
                if (zip.Entries.Any(p => p.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase)))
                {
                    return ".docx";
                }
                else if (zip.Entries.Any(p => p.FullName.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)))
                {
                    return ".xlsx";
                }
                else if (zip.Entries.Any(p => p.FullName.StartsWith("ppt/", StringComparison.OrdinalIgnoreCase)))
                {
                    return ".pptx";
                }
                else if (zip.Entries.Any(p => p.FullName.EndsWith("content.opf", StringComparison.OrdinalIgnoreCase)))
                {
                    return ".epub";
                }
                else if (zip.Entries.Any(p => p.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase)))
                {
                    return ".jar";
                }
                else
                {
                    return ".zip";
                }
            }
        }
    }
}
