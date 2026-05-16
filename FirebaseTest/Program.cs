using System;
using System.IO;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

class Program
{
    static void Main()
    {
        try
        {
            string path = @"..\src\OCC.Api\service-account.json";
            var bytes = File.ReadAllBytes(path);
            int offset = (bytes.Length > 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191) ? 3 : 0;
            
            using (var ms = new MemoryStream(bytes, offset, bytes.Length - offset))
            {
                var app = FirebaseApp.Create(new AppOptions()
                {
                    Credential = GoogleCredential.FromStream(ms)
                });
                Console.WriteLine("SUCCESS! " + app.Name);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
    }
}
