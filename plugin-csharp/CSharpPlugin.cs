using proto;

namespace plugin_csharp
{
    class CSharpPlugin : IProtoPlugin
    {
        public string Target
        {
            get
            {
                return "csharp";
            }
        }

        public IProtoWriter CreateProtoWriter()
        {
            return new CSharpProtoWriter();
        }

        public IManagerWriter CreateManagerWriter()
        {
            return new CSharpManagerWriter();
        }
    }
}
