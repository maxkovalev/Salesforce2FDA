using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
               
            }
            catch (Exception ex)
            {

                Console.WriteLine("Critical error:" + ex.Message);
                System.Threading.Thread.Sleep(5000);
            }
            

        }
    }
}
