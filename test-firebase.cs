using System;
using System.IO;
using Google.Apis.Auth.OAuth2;

class Program
{
    static void Main()
    {
        try
        {
            string path = "src/OCC.Api/service-account.json";
            var json = File.ReadAllText(path);
            var credential = GoogleCredential.FromJson(json);
            Console.WriteLine("Success! " + credential.UnderlyingCredential.GetType().Name);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.ToString());
        }
    }
}
