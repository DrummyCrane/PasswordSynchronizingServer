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
            Server server = new Server();
            server.Initiate();
            await server.Listening();
        }
    }
}
