using System;
using System.Security.Cryptography;
using System.Text;

// Console-only IPC helper. Secrets are received on stdin and never placed in command arguments.
class SecureStore
{
    static int Main(string[] args)
    {
        try
        {
            byte[] input = Convert.FromBase64String(Console.In.ReadToEnd().Trim());
            byte[] entropy = Encoding.UTF8.GetBytes("LumaShot.credentials.v1");
            byte[] result =
                args.Length == 1 && args[0] == "protect"
                    ? ProtectedData.Protect(input, entropy, DataProtectionScope.CurrentUser)
                : args.Length == 1 && args[0] == "unprotect"
                    ? ProtectedData.Unprotect(input, entropy, DataProtectionScope.CurrentUser)
                : null;
            if (result == null)
                return 2;
            Console.Write(Convert.ToBase64String(result));
            Array.Clear(input, 0, input.Length);
            return 0;
        }
        catch
        {
            Console.Error.Write("Windows credential protection failed.");
            return 1;
        }
    }
}
