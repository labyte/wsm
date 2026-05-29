using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using WSM.Core.Interfaces;
using WSM.Core.Models;

namespace WSM.Core.Services;

/// <summary>
/// 将 <see cref="ManagedService"/> 生成为 WinSW XML 配置。
/// </summary>
public sealed class WinSwXmlGenerator : IWinSwConfigGenerator
{
    public string Generate(ManagedService service)
    {
        if (service == null)
        {
            throw new ArgumentNullException(nameof(service));
        }

        var settings = new XmlWriterSettings
        {
            Indent = true,
            OmitXmlDeclaration = false,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
        };

        using (var stream = new MemoryStream())
        using (var writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteStartDocument();
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
                WriteElement(writer, "delayedAutoStart", "true");
            }

            foreach (var dependency in service.Dependencies)
            {
                if (!string.IsNullOrWhiteSpace(dependency))
                {
                    WriteElement(writer, "depend", dependency);
                }
            }

            WriteLogSection(writer, service.LogPolicy);
            WriteFailureSection(writer, service.FailurePolicy);

            if (service.StopTimeoutSeconds > 0)
            {
                WriteElement(writer, "stoptimeout", service.StopTimeoutSeconds.ToString(CultureInfo.InvariantCulture) + " sec");
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
            writer.Flush();

            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    private static void WriteLogSection(XmlWriter writer, LogPolicy policy)
    {
        writer.WriteStartElement("log");
        writer.WriteAttributeString("mode", MapLogMode(policy.Mode));
        WriteElement(writer, "sizeThreshold", policy.SizeThresholdKb.ToString(CultureInfo.InvariantCulture));
        WriteElement(writer, "keepFiles", policy.KeepFiles.ToString(CultureInfo.InvariantCulture));
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
                if (!string.IsNullOrWhiteSpace(action.Delay))
                {
                    writer.WriteAttributeString("delay", action.Delay);
                }

                writer.WriteEndElement();
            }
        }

        if (policy != null && !string.IsNullOrWhiteSpace(policy.ResetFailurePeriod))
        {
            WriteElement(writer, "resetfailure", policy.ResetFailurePeriod);
        }
    }

    private static void WriteElement(XmlWriter writer, string name, string value)
    {
        writer.WriteStartElement(name);
        writer.WriteString(value);
        writer.WriteEndElement();
    }

    internal static string MapStartMode(ManagedServiceStartMode mode)
    {
        switch (mode)
        {
            case ManagedServiceStartMode.Manual:
                return "Manual";
            case ManagedServiceStartMode.Disabled:
                return "Disabled";
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
                return "ignore";
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
