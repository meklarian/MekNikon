<Query Kind="Statements" />

Regex re = new Regex("^((f/?)?(\\d+\\.?\\d*))", RegexOptions.IgnoreCase);
Console.WriteLine(re.IsMatch("f"));
Console.WriteLine(re.IsMatch("f/"));
Console.WriteLine(re.IsMatch("f/1"));
Console.WriteLine(re.IsMatch("f1"));
Console.WriteLine(re.IsMatch("f1.4"));
Console.WriteLine(re.IsMatch("f/1.4"));
Console.WriteLine(re.IsMatch("f22"));
Console.WriteLine(re.IsMatch("f22.0"));
Console.WriteLine(re.IsMatch("f/22.0"));
Console.WriteLine(re.IsMatch("2"));
Console.WriteLine(re.IsMatch("16"));
Console.WriteLine(re.IsMatch("22.0"));
var m = re.Match("f1");
Console.WriteLine(m.Groups.Count);
Console.WriteLine(m.Groups[3].Value);
var m2 = re.Match("f/22");
Console.WriteLine(m2.Groups.Count);
Console.WriteLine(m2.Groups[3].Value);
var m3 = re.Match("1.0");
Console.WriteLine(m3.Groups.Count);
Console.WriteLine(m3.Groups[3].Value);
var m4 = re.Match("f1.4");
Console.WriteLine(m4.Groups.Count);
Console.WriteLine(m4.Groups[3].Value);
var m5 = re.Match("f/2.8");
Console.WriteLine(m5.Groups.Count);
Console.WriteLine(m5.Groups[3].Value);
var m6 = re.Match("F1");
Console.WriteLine(m6.Groups.Count);
Console.WriteLine(m6.Groups[3].Value);
