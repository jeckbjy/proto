using System;
using System.IO;
using System.Collections.Generic;
using proto;

namespace plugin_cpp
{
    class CppManagerWriter : IManagerWriter
    {
        List<Proto> m_protos;
        string m_file;
        string m_path;
        string m_dir;
        StreamWriter m_writer;
        public void Write(List<Proto> protos, Dictionary<string, string> config)
        {
            //throw new NotImplementedException();
            m_protos = protos;
            m_file = config["mgr"];
            m_dir = config["dir"];
            m_path = m_dir + m_file;
            WriteHeader();
            WriteCpp();
        }

        // 写入头文件
        void WriteHeader()
        {
            string path_header = m_path + ".h";
            m_writer = new StreamWriter(File.Open(path_header, FileMode.Create));
            m_writer.WriteLine("struct pt_message;");
            m_writer.WriteLine("pt_message* pt_create_packet(unsigned int msgid);");
            m_writer.Close();
        }
        void WriteCpp()
        {
            string path_cpp = m_path + ".cpp";
            m_writer = new StreamWriter(File.Open(path_cpp, FileMode.Create));
            m_writer.WriteLine("#include <PacketFactory.h>");
            m_writer.WriteLine("#include <proto.h>");
            // 写入头文件等
            foreach(var proto in m_protos)
            {
                if (!proto.HasPacket)
                    continue;
                m_writer.WriteLine(String.Format("#include <{0}.h>", proto.Name));
            }
            // 写入函数
            m_writer.WriteLine();
            m_writer.WriteLine("pt_message* pt_create_packet(uint32_t msgid){");
            m_writer.WriteLine("    switch(msgid){");
            foreach(var proto in m_protos)
            {
                if (!proto.HasPacket)
                    continue;
                foreach(var msg in proto.Messages)
                {
                    if (!msg.IsStruct || !msg.HasID)
                        continue;
                    m_writer.WriteLine(String.Format("    case {0}: return new {1}();", msg.id_name, msg.name));
                }
            }
            m_writer.WriteLine("    default:return 0;");
            m_writer.WriteLine("    }");
            m_writer.WriteLine("}");
            m_writer.Close();
        }
    }
}
