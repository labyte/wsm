using WSM.Core.Models;

namespace WSM.Core.Interfaces;

/// <summary>
/// WinSW XML 配置生成器。
/// </summary>
public interface IWinSwConfigGenerator
{
    string Generate(ManagedService service);

    /// <summary>
    /// 生成 UTF-8 无 BOM 字节，供 WinSW 2.x 直接读取。
    /// </summary>
    byte[] GenerateUtf8Bytes(ManagedService service);
}
