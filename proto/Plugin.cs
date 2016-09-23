using System;
using System.Collections.Generic;
using System.IO;

namespace proto
{
    public abstract class ProtoPlugin
    {
        public abstract string Target { get; }
        public abstract string Extension { get; }
        
        // config
        protected Dictionary<string, string> m_config = new Dictionary<string, string>();
        protected string m_outputDir = "./";
        protected string m_managerName = "PacketManager";
        protected bool m_checkWriteTime = false;

        protected string m_output;
        protected StreamWriter m_writer;

        public string OutputDir
        {
            get { return m_outputDir; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                    m_outputDir = value;
            }
        }

        public string ManagerName
        {
            get { return m_managerName; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                    m_managerName = value;
            }
        }

        public bool CheckWriteTime
        {
            get { return m_checkWriteTime; }
            set { m_checkWriteTime = value; }
        }

        public void SetCheckWriteTime(string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            m_checkWriteTime = bool.Parse(value);
        }

        public void SetParam(string param)
        {
            if (string.IsNullOrEmpty(param))
                return;

            if (m_config == null)
                m_config = new Dictionary<string, string>();

            string[] tokens = param.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                string[] datas = token.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (datas.Length != 2)
                    continue;
                m_config.Add(datas[0], datas[1]);
            }
        }

        public string GetParam(string key)
        {
            if (m_config.ContainsKey(key))
                return m_config[key];

            return null;
        }

        public bool NeedWrite(string file, DateTime time)
        {
            if (!CheckWriteTime)
                return false;

            if (!File.Exists(file))
                return true;

            FileInfo fi = new FileInfo(file);

            return fi.LastWriteTime >= time;
        }

        public virtual void WriteManager(List<Proto> protos)
        {
            //m_output = String.Format("{0}/{1}.{2}", m_outputDir, ManagerName, Extension);
            //m_writer = new StreamWriter(File.Open(m_output, FileMode.Create));
            //m_writer.Close();
        }

        public virtual void WriteProto(Proto proto)
        {
            m_output = String.Format("{0}/{1}.{2}", m_outputDir, proto.Name, Extension);

            if (!NeedWrite(m_output, proto.LastWriteTime))
                return;

            m_writer = new StreamWriter(File.Open(m_output, FileMode.Create));

            WriteBegin();

            for(int i = 0; i < proto.Imports.Count; ++i)
            {
                WriteImport(proto.Imports[i]);
            }

            m_writer.WriteLine();

            for(int i = 0; i < proto.Scopes.Count; ++i)
            {
                Scope scope = proto.Scopes[i];
                if (scope is EnumScope)
                    WriteEnum(scope as EnumScope);
                else
                    WriteStruct(scope as StructScope);

                m_writer.WriteLine();
            }

            WriteEnd();

            m_writer.Close();
        }

        protected virtual void WriteBegin() { }
        protected virtual void WriteEnd() { }
        protected virtual void WriteImport(string import) { }
        protected virtual void WriteStruct(StructScope msg) { }
        protected virtual void WriteEnum(EnumScope msg) { }
    }
}
