using WSM.Core.Models;

namespace WSM.Core.Interfaces;

/// <summary>
/// WinSW XML 配置生成器。
/// </summary>
public interface IWinSwConfigGenerator
{
    string Generate(ManagedService service);
}
