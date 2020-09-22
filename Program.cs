using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;

namespace PasswordSynchronizingServer
{
    class Program
    {
        static async Task Main()
        {
            //Queue<string> queue;
            Hashtable hashtable = new Hashtable();

            Server server = new Server();
            server.Initiate();
            await server.Listening();
        }
    }
}
