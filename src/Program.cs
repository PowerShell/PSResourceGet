using System;
using Microsoft.PowerShell.PowerShellGet.Cmdlets;
using Microsoft.PowerShell.PowerShellGet.RepositorySettings;

namespace PowerShellGet
{
    class Program
    {
        static void Main(string[] args)
        {
            RespositorySettings r = new RespositorySettings();



            
            //Find Repository XML
            if (r.FindRepositoryXML())
            {
                Console.Out.WriteLine("Found repository xml!");
            }
            else {
                Console.Out.WriteLine("Did NOT find repository xml");
            }


            //Create a new repository XML -- works
            r.CreateNewRepositoryXML();

            if (r.FindRepositoryXML())
            {
                Console.Out.WriteLine("Found repository xml!");
            }
            else
            {
                Console.Out.WriteLine("Did NOT find repository xml");
            }




            //Test add
            var uri1 = new Uri("https://www.testrepo1.org");
            var uri2 = new Uri("https://www.testrepo7.org");

            r.Add("testRepo1", uri1, 2, true);
            r.Add("testRepo7", uri2, 7, true);

            //Update
            //r.Update("testRepo1", "https://www.testrepo1.org", "2", "untrusted");


            //Test remove
            //r.Remove("testRepo1");



            //Test read
           // r.Read(["testRepo7"]));

            




            Console.WriteLine("Starting program.");
        }
    }
}
