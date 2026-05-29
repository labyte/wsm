using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WSM.Infrastructure.Logging;

/// <summary>
/// 日志文本编码检测与读取。
/// </summary>
public static class LogTextEncodingHelper
{
    private static bool _providerRegistered;

    static LogTextEncodingHelper()
    {
        EnsureEncodingProvider();
    }

    public static void EnsureEncodingProvider()
    {
        if (_providerRegistered)
        {
            return;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _providerRegistered = true;
    }

    /// <summary>
    /// 获取子进程 stdout/stderr 解码编码（中文 Windows 通常为 GBK）。
    /// </summary>
    public static Encoding GetProcessOutputEncoding()
    {
        EnsureEncodingProvider();

        try
        {
            return Encoding.GetEncoding(Console.OutputEncoding.CodePage);
        }
        catch
        {
            return Encoding.GetEncoding(936);
        }
    }

    public static string ReadAllText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return DecodeBytes(bytes);
    }

    public static IEnumerable<string> ReadAllLines(string path)
    {
        return ReadAllText(path).Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
    }

    public static string DecodeBytes(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try
        {
            return utf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(936).GetString(bytes);
        }
    }

    public static bool LooksLikeValidUtf8(byte[] bytes)
    {
        try
        {
            var utf8 = new UTF8Encoding(false, true);
            utf8.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
