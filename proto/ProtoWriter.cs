using System;
using System.IO;
using System.Collections.Generic;

namespace proto
{
    public abstract class ProtoWriter : IProtoWriter
    {
        protected Dictionary<string, string> m_config;
        protected Proto m_proto;
        protected string m_output;
        protected StreamWriter m_writer;
        // 后缀名,无需包含.
        public abstract string Extension { get; }

        public virtual void Write(Proto proto, Dictionary<string, string> config)
        {
            string ext;
            if (Extension.StartsWith("."))
                ext = Extension.Substring(1);
            else
                ext = Extension;

            string directory = config["dir"];
            m_config = config;
            m_proto = proto;
            m_output = String.Format("{0}/{1}.{2}", directory, proto.Name, Extension);
            m_writer = new StreamWriter(File.Open(m_output, FileMode.Create));
            BeginWrite();
            // 序列化
            for (int i = 0; i < m_proto.Imports.Count; ++i)
            {
                WriteImport(m_proto.Imports[i]);
            }
            for (int i = 0; i < m_proto.Messages.Count; ++i)
            {
                Message msg = m_proto.Messages[i];
                if (msg.type == CmdType.ENUM)
                    WriteEnum(msg);
                else
                    WriteStruct(msg);
                m_writer.WriteLine();
            }
            EndWrite();
            m_writer.Close();
        }

        public virtual void BeginWrite() { }
        public virtual void EndWrite() { }
        public abstract void WriteImport(string import);
        public abstract void WriteStruct(Message msg);
        public abstract void WriteEnum(Message msg);
    }
}
