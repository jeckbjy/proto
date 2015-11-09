using System.Collections.Generic;

namespace proto
{
    public interface IProtoWriter
    {
        // 注意路径中不包含后缀
        void Write(Proto proto, string directory);
    }

    // 可用于反射
    public interface IManagerWriter
    {
        // 输出到制定路径
        void Write(List<Proto> protos, string directory);
    }

    // 插件
    public interface IProtoPlugin
    {
        string Target { get; }
        IProtoWriter    CreateProtoWriter();
        IManagerWriter  CreateManagerWriter();
    }
}
