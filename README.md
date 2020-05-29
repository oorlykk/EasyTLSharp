EasyTLSharp
===========

Easy wrapper over TLSharp

Depends:
```
https://github.com/sochix/TLSharp
https://github.com/meebey/starksoftproxy
```

Examples:
```
using EasyTLSharp;
using Starksoft.Net.Proxy;
...

var client = new EasyTLSharpClient(1276334, b23cca4e275372cf328818a5ea567402, +7XXXXXXXXXX);
client.AuthCodeHandler += new EventHandler<EasyTLSharpBase.AuthEventArgs>((o, args) => 
{
    Console.WriteLine($"Enter auth CODE (hash {args.hash}):");
    args.code = Console.ReadLine();
});
client.ProxyHost = "127.0.0.1";
client.ProxyPort = 9050;
client.ProxyType = ProxyType.Socks5;
bool is_connected = await client.Connect();
if (is_connected)
{
    foreach (var c in client.ContactList)
        Console.WriteLine(c.Username);
}
else
    Console.WriteLine("Not connected");
```
