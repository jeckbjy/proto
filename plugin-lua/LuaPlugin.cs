using proto;

namespace plugin_lua
{
    public class LuaPlugin : IProtoPlugin
    {
        public string Target
        {
            get
            {
                return "lua";
            }
        }

        public IProtoWriter CreateProtoWriter()
        {
            return new LuaProtoWriter();
        }

        public IManagerWriter CreateManagerWriter()
        {
            return new LuaManagerWriter();
        }
    }
}
