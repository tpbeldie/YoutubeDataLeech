using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YoutubeDataLeech
{
    internal class Program
    {
        [STAThread]
        static void Main(string[] args) {
           new LeechWindow().ShowDialog();
            Console.ReadLine();
        }
    }
}
