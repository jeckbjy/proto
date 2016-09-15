using System;
using System.Collections.Generic;
using System.Text;

namespace proto
{
    interface IMessage
    {
        uint MsgID { get; }
        void Encode(Encoder encoder);
        void Decode(Decoder decoder);
    }

    public class MessageManager
    {

    }
}