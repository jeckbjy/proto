using System;
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

        public IManagerWriter CreateManagerWriter()
        {
            throw new NotImplementedException();
        }

        public IProtoWriter CreateProtoWriter()
        {
            throw new NotImplementedException();
        }
    }
}
