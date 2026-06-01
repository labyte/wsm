using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using WSM.Core.Interfaces;
using WSM.Core.Models;

namespace WSM.Core.Services;

/// <summary>
/// 将 <see cref="ManagedService"/> 生成为 WinSW XML 配置（对齐 WinSW 3 文档）。
/// </summary>
public sealed class WinSwXmlGenerator : IWinSwConfigGenerator
{
    private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public string Generate(ManagedService service)
    {
        return Utf8NoBom.GetString(GenerateUtf8Bytes(service));
    }

    public byte[] GenerateUtf8Bytes(ManagedService service)
    {
        if (service == null)
        {
            throw new ArgumentNullException(nameof(service));
        }

        var settings = new XmlWriterSettings
        {
            Indent = true,
            // 使用无 BOM UTF-8，避免不同 WinSW 版本的兼容性问题
            OmitXmlDeclaration = true,
            Encoding = Utf8NoBom
        };

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteStartElement("service");

            WriteElement(writer, "id", service.Id);
            WriteElement(writer, "name", service.DisplayName);

            if (!string.IsNullOrWhiteSpace(service.Description))
            {
                WriteElement(writer, "description", service.Description);
            }

            foreach (var env in service.EnvironmentVariables)
            {
                if (string.IsNullOrWhiteSpace(env.Name))
                {
                    continue;
                }

                writer.WriteStartElement("env");
                writer.WriteAttributeString("name", env.Name);
                writer.WriteAttributeString("value", env.Value ?? string.Empty);
                writer.WriteEndElement();
            }

            WriteElement(writer, "executable", service.ExecutablePath);

            if (!string.IsNullOrWhiteSpace(service.Arguments))
            {
                WriteElement(writer, "arguments", service.Arguments);
            }

            if (!string.IsNullOrWhiteSpace(service.WorkingDirectory))
            {
                WriteElement(writer, "workingdirectory", service.WorkingDirectory);
            }

            WriteElement(writer, "startmode", MapStartMode(service.StartMode));

            if (service.DelayedAutoStart && service.StartMode == ManagedServiceStartMode.Automatic)
            {
                // 依据 WinSW 文档，布尔配置项应显式写入 true/false 文本值
                WriteElement(writer, "delayedAutoStart", "true");
            }

            // 依据 WinSW 文档，显式写入 autoRefresh，避免跨版本默认行为差异。
            WriteElement(writer, "autoRefresh", ToLowerBoolean(service.AutoRefresh));

            // hidewindow 为可选项，仅在启用时写出。
            if (service.HideWindow)
            {
                WriteElement(writer, "hidewindow", "true");
            }

            foreach (var dependency in service.Dependencies)
            {
                if (!string.IsNullOrWhiteSpace(dependency))
                {
                    WriteElement(writer, "depend", dependency);
                }
            }

            WriteElement(writer, "logpath", WsmConstants.ServiceWinSwLogPath);
            WriteLogSection(writer, service.LogPolicy);
            WriteFailureSection(writer, service.FailurePolicy);

            if (service.StopTimeoutSeconds > 0)
            {
                WriteElement(writer, "stoptimeout", service.StopTimeoutSeconds.ToString(CultureInfo.InvariantCulture) + "sec");
            }

            writer.WriteEndElement();
            writer.Flush();
        }

        return stream.ToArray();
    }

    private static void WriteLogSection(XmlWriter writer, LogPolicy policy)
    {
        writer.WriteStartElement("log");
        var mode = MapLogMode(policy.Mode);
        writer.WriteAttributeString("mode", mode);

        // none 模式下不应写入轮转相关参数，避免产生无效配置。
        if (!string.Equals(mode, "none", StringComparison.OrdinalIgnoreCase))
        {
            WriteElement(writer, "sizeThreshold", policy.SizeThresholdKb.ToString(CultureInfo.InvariantCulture));
            WriteElement(writer, "keepFiles", policy.KeepFiles.ToString(CultureInfo.InvariantCulture));
        }

        writer.WriteEndElement();
    }

    private static void WriteFailureSection(XmlWriter writer, FailurePolicy policy)
    {
        if (policy?.Actions != null)
        {
            foreach (var action in policy.Actions)
            {
                writer.WriteStartElement("onfailure");
                writer.WriteAttributeString("action", MapFailureAction(action.Action));

                if (ShouldWriteDelay(action))
                {
                    writer.WriteAttributeString("delay", NormalizeDelay(action.Delay));
                }

                writer.WriteEndElement();
            }
        }

        if (policy != null && !string.IsNullOrWhiteSpace(policy.ResetFailurePeriod))
        {
            WriteElement(writer, "resetfailure", policy.ResetFailurePeriod);
        }
    }

    private static bool ShouldWriteDelay(FailureActionEntry action)
    {
        if (action.Action == FailureActionType.None)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(action.Delay)
            && !string.Equals(action.Delay.Trim(), "0 sec", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(action.Delay.Trim(), "0sec", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDelay(string? delay)
    {
        if (string.IsNullOrWhiteSpace(delay))
        {
            return "0 sec";
        }

        return delay!.Trim();
    }

    private static void WriteElement(XmlWriter writer, string name, string value)
    {
        writer.WriteStartElement(name);
        writer.WriteString(value);
        writer.WriteEndElement();
    }

    private static string ToLowerBoolean(bool value)
    {
        return value ? "true" : "false";
    }

    internal static string MapStartMode(ManagedServiceStartMode mode)
    {
        switch (mode)
        {
            case ManagedServiceStartMode.Manual:
                return "Manual";
            case ManagedServiceStartMode.Disabled:
                return "Manual";
            default:
                return "Automatic";
        }
    }

    internal static string MapLogMode(LogMode mode)
    {
        switch (mode)
        {
            case LogMode.Reset:
                return "reset";
            case LogMode.Ignore:
                return "none";
            case LogMode.Roll:
                return "roll";
            case LogMode.RollByTime:
                return "roll-by-time";
            case LogMode.RollBySizeTime:
                return "roll-by-size-time";
            case LogMode.Append:
                return "append";
            default:
                return "roll-by-size";
        }
    }

    internal static string MapFailureAction(FailureActionType action)
    {
        switch (action)
        {
            case FailureActionType.Reboot:
                return "reboot";
            case FailureActionType.None:
                return "none";
            default:
                return "restart";
        }
    }
}
