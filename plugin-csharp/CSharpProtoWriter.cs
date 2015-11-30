using System;
using System.Collections.Generic;
using proto;

namespace plugin_csharp
{
    class CSharpProtoWriter : ProtoWriter
    {
        public override string Extension
        {
            get
            {
                return "cs";
            }
        }

        public override void BeginWrite()
        {
        }

        public override void WriteImport(string import)
        {
        }

        public override void WriteEnum(Message msg)
        {
        }

        public override void WriteStruct(Message msg)
        {
        }
    }
}
