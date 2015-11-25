using proto;

namespace plugin_cpp
{
    class CppPlugin : IProtoPlugin
    {
        public string Target
        {
            get
            {
                return "cpp";
            }
        }

        public IProtoWriter CreateProtoWriter()
        {
            return new CppProtoWriter();
        }

        public IManagerWriter CreateManagerWriter()
        {
            return new CppManagerWriter();
        }
    }
}
