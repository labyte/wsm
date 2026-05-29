using System;
using System.ServiceProcess;
using WSM.Core.Models;

namespace WSM.Infrastructure.Scm;

/// <summary>
/// Windows 服务控制管理器（SCM）封装。
/// </summary>
public sealed class WindowsScmService
{
    /// <summary>
    /// 查询服务是否已安装。
    /// </summary>
    public bool IsInstalled(string serviceId)
    {
        return TryGetController(serviceId) != null;
    }

    /// <summary>
    /// 获取服务运行时状态。
    /// </summary>
    public ServiceRuntimeStatus GetRuntimeStatus(string serviceId)
    {
        using (var controller = TryGetController(serviceId))
        {
            if (controller == null)
            {
                return ServiceRuntimeStatus.NotInstalled;
            }

            return MapStatus(controller.Status);
        }
    }

    /// <summary>
    /// 等待服务达到目标状态。
    /// </summary>
    public bool WaitForStatus(string serviceId, ServiceControllerStatus targetStatus, TimeSpan timeout)
    {
        using (var controller = TryGetController(serviceId))
        {
            if (controller == null)
            {
                return false;
            }

            try
            {
                controller.WaitForStatus(targetStatus, timeout);
                return controller.Status == targetStatus;
            }
            catch
            {
                return false;
            }
        }
    }

    private static ServiceController? TryGetController(string serviceId)
    {
        try
        {
            return new ServiceController(serviceId);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static ServiceRuntimeStatus MapStatus(ServiceControllerStatus status)
    {
        switch (status)
        {
            case ServiceControllerStatus.Running:
                return ServiceRuntimeStatus.Running;
            case ServiceControllerStatus.Stopped:
                return ServiceRuntimeStatus.Stopped;
            case ServiceControllerStatus.StartPending:
                return ServiceRuntimeStatus.StartPending;
            case ServiceControllerStatus.StopPending:
                return ServiceRuntimeStatus.StopPending;
            default:
                return ServiceRuntimeStatus.Error;
        }
    }
}
