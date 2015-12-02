using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace proto
{
    // 属性
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ProtoFieldAttribute : Attribute
    {
        private uint tag;

        public ProtoFieldAttribute(uint tag)
        {
            this.tag = tag;
        }

        public uint Tag { get { return tag; } }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class ProtoPacketAttribute : Attribute
    {
        private uint msgid;
        public ProtoPacketAttribute(uint msgid)
        {
            this.msgid = msgid;
        }
        public uint MsgID { get { return this.msgid; } }
    }

    class MetaField
    {
        private uint m_tag;
        private MemberInfo m_info;
        public uint Tag { get { return m_tag; } }
        public Type Type { get { return m_info.GetType(); } }

        public MetaField(uint tag, MemberInfo info)
        {
            this.m_tag = tag;
            this.m_info = info;
        }

        public object GetValue(object obj)
        {
            if (m_info.MemberType == MemberTypes.Field)
                return (m_info as FieldInfo).GetValue(obj);
            else
                return (m_info as PropertyInfo).GetValue(obj, null);
        }

        public void SetValue(object obj, object val)
        {
            if (m_info.MemberType == MemberTypes.Field)
                (m_info as FieldInfo).SetValue(obj, val);
            else
                (m_info as PropertyInfo).SetValue(obj, val, null);
        }
    }

    public class MetaType : IEnumerable
    {
        private Type m_type;
        private SortedDictionary<uint, MetaField> m_fields = new SortedDictionary<uint, MetaField>();

        public MetaType(Type type)
        {
            m_type = type;
            Parse(type, true);
        }

        public IEnumerator GetEnumerator()
        {
            return m_fields.GetEnumerator();
        }

        private void Parse(Type type, bool publicOnly)
        {
            BindingFlags flags = publicOnly ? BindingFlags.Public | BindingFlags.Instance : BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;
            PropertyInfo[] props = type.GetProperties(flags);
            FieldInfo[] fields = type.GetFields(flags);
            MemberInfo[] members = new MemberInfo[fields.Length + props.Length];
            foreach (var mem in members)
            {
                object[] attrs = mem.GetCustomAttributes(typeof(ProtoFieldAttribute), true);
                if (attrs == null || attrs.Length == 0)
                    continue;
                ProtoFieldAttribute attr = attrs[0] as ProtoFieldAttribute;
                m_fields.Add(attr.Tag, new MetaField(attr.Tag, mem));
                //m_fields[attr.Tag] = new MetaField(attr.Tag, mem);
            }
        }
    }

    public static class ModelManager
    {
        public delegate object Creator();
        private static Dictionary<uint, Creator> m_packets = new Dictionary<uint, Creator>();
        private static Dictionary<Type, Creator> m_creator = new Dictionary<Type, Creator>();
        private static Dictionary<Type, MetaType> m_metas = new Dictionary<Type, MetaType>();

        public static MetaType GetMeta(Type type)
        {
            MetaType meta;
            if (!m_metas.TryGetValue(type, out meta))
            {
                meta = new MetaType(type);
                m_metas.Add(type, meta);
            }
            return meta;
        }

        public static void Add(Type type, Creator fun)
        {
            if(!m_creator.ContainsKey(type))
                m_creator.Add(type, fun);
        }

        public static void Add(uint msgid, Creator fun)
        {
            m_packets.Add(msgid, fun);
        }

        public static object Create(Type type)
        {
            Creator fun;
            if (!m_creator.TryGetValue(type, out fun))
                return null;
            return fun();
        }

        public static object Create(uint msgid)
        {
            Creator fun;
            if (!m_packets.TryGetValue(msgid, out fun))
                return null;
            return fun();
        }
    }

    public static class PacketManager
    {
        public static void Init()
        {
            //ModelManager.Add(typeof(Person), delegate() { return new Person(); });
            //ModelManager.Add(typeof(Person), delegate() { return new Object(); });
        }
    }

    //
    public class Serializer
    {
        public static T Deserialize<T>(Stream source)
        {
            Type type = typeof(T);
            object result = ModelManager.Create(typeof(T));
            if (result != null)
            {
                // 解析field
                MetaType meta = ModelManager.GetMeta(type);
                // 读取tag等，并根据信息反序列化
            }
            return (T)result;
        }

        public static void Serialize<T>(Stream source, T instance)
        {
            if(instance != null)
            {
                
            }
        }
    }

    //
    class CProto
    {
        CProto()
        {
            //Type type = typeof(Person);

            //bool publicOnly = true;
            //BindingFlags flags = publicOnly ? BindingFlags.Public | BindingFlags.Instance : BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic;
            //PropertyInfo[] props = type.GetProperties(flags);
            //FieldInfo[] fields = type.GetFields(flags);
            //MemberInfo[] members = new MemberInfo[fields.Length + props.Length];
            //props.CopyTo(members, 0);
            //fields.CopyTo(members, props.Length);
            //foreach (var mem in members)
            //{
            //    var attr = mem.GetCustomAttribute<ProtoFieldAttribute>();
            //    if (attr == null)
            //        continue;
            //    Console.WriteLine("{0}:{1}", mem.ToString(), attr.Tag);
            //}

            //Person obj = new Person();
            //MemberInfo mi = typeof(Person).GetField("name");
            //if (mi.GetType() == typeof(FieldInfo))
            //{
            //    ((FieldInfo)mi).SetValue(obj, "asdf");
            //}
            //Console.WriteLine("{0}:{1}", "name", obj.name);
            //Console.ReadLine();
        }
    }
}
