using System;
using System.Text;

var b64 = args[0];
var bytes = Convert.FromBase64String(b64);
Console.WriteLine(Encoding.UTF8.GetString(bytes));
