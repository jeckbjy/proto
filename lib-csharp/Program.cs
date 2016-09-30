using System;
using System.IO;
using System.Collections.Generic;

namespace proto
{
    [ProtoPacket()]
    class Person
    {
        [ProtoField(1)]
        public uint id;
        [ProtoField(2)]
        public string name;
        [ProtoField(3)]
        public string addr;
    }

    [ProtoPacket(1)]
    class LoginMsg
    {
        [ProtoField(1)]
        public uint id;
        [ProtoField(2)]
        public string name;
        [ProtoField(3)]
        public Person info;
        [ProtoField(4)]
        public List<Person> infos = new List<Person>();
    }

    class Program
    {
        static void Main(string[] args)
        {
            ModelManager.Add(typeof(LoginMsg), delegate() { return new LoginMsg(); });
            Person person = new Person();
            person.id = 10;
            person.name = "一阿萨德法萨芬";
            person.addr = "沃尔沃人情味儿阿是否是";

            LoginMsg msg = new LoginMsg();
            msg.id = 1;
            msg.name = "asdfasdfasfasasdfsadfsadfsafsaf";
            msg.info = person;
            msg.infos.Add(person);

            MemoryStream stream = new MemoryStream();
            Encoder encoder = new Encoder(stream);
            encoder.Encode(msg);

            Decoder decoder = new Decoder(stream);
            LoginMsg result = decoder.Decode<LoginMsg>();
        }
    }
}
