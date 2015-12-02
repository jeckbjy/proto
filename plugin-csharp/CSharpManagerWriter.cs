using System;
using System.IO;
using System.Collections.Generic;
using proto;

namespace plugin_csharp
{
    class CSharpManagerWriter : IManagerWriter
    {
        public void Write(List<Proto> protos, Dictionary<string, string> config)
        {
            string path = config["dir"] + config["mgr"] + ".cs";
            StreamWriter writer = new StreamWriter(File.Open(path, FileMode.Create));
            writer.WriteLine("namespace proto\n{");
            writer.WriteLine("    public static class PacketManager");
            writer.WriteLine("    {");
            writer.WriteLine("        public static void Init()");
            writer.WriteLine("        {");
            // 写入数据
            List<string> packets = new List<string>();
            List<string> models = new List<string>();
            List<string> fields = new List<string>();
            // 写入消息
            foreach(var pro in protos)
            {
                foreach(var msg in pro.Messages)
                {
                    if (!msg.IsStruct)
                        continue;
                    if(msg.HasID)
                        packets.Add(String.Format("            ModelManager.Add({0}, delegate() {{ return new {1}(); }});", msg.GetMsgID("."), msg.name));
                    models.Add(String.Format("            ModelManager.Add(typeof({0}), delegate() {{ return new {0}();}});", msg.name));
                    foreach(var field in msg.fields)
                    {
                        if (field.container != Container.NONE)
                            fields.Add(String.Format("            ModelManager.Add(typeof({0}), delegate() {{ return new {0}();}});", CSharpFormat.GetFieldType(field)));
                    }
                }
            }
            // 写入
            if(packets.Count > 0)
            {
                writer.WriteLine("            // write packet by msgID");
                foreach (var str in packets)
                    writer.WriteLine(str);
                writer.WriteLine();
            }

            if(models.Count > 0)
            {
                writer.WriteLine("            // write packet model");
                foreach (var str in models)
                    writer.WriteLine(str);
                writer.WriteLine();
            }

            if(fields.Count > 0)
            {
                writer.WriteLine("            // write fields model");
                foreach (var str in fields)
                    writer.WriteLine(str);
                writer.WriteLine();
            }

            writer.WriteLine("        }");
            writer.WriteLine("    }");
            writer.WriteLine("}");
            writer.Close();
        }
    }
}
