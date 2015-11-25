using System;
namespace proto
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                ProtoManager manager = new ProtoManager();
                manager.Process();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
