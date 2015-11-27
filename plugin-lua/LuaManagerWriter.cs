using System;
using System.IO;
using System.Collections.Generic;
using proto;

namespace plugin_lua
{
    class LuaManagerWriter : IManagerWriter
    {
        public void Write(List<Proto> protos, Dictionary<string, string> config)
        {
            string dir = config["dir"];
            string mgr = config["mgr"];
            string file = dir + mgr + ".lua";
            StreamWriter writer = new StreamWriter(File.Open(file, FileMode.Create));
            // 写入头
            foreach(var proto in protos)
            {
                if (!proto.HasPacket)
                    continue;
                writer.WriteLine(String.Format("require(\"{0}\")", proto.Name));
            }
            // 写入table
            writer.WriteLine();
            writer.WriteLine("local _packet_map = {");
            foreach(var proto in protos)
            {
                if (!proto.HasPacket)
                    continue;
                foreach(var msg in proto.Messages)
                {
                    if (!msg.HasID)
                        continue;
                    writer.WriteLine(String.Format("    [{0}] = {1},", msg.id_name, msg.name));
                }
            }

            writer.WriteLine("}");
            writer.WriteLine();

            // 写入function
            writer.WriteLine("function create_packet(msgid)");
            writer.WriteLine("    return _packet_map[msgid]");
            writer.WriteLine("end");
            writer.Flush();
            writer.Close();
        }
    }
}
