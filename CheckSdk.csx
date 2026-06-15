using System;
using System.Reflection;
using System.IO;

try
{
    var asm = Assembly.LoadFrom(@"C:\Users\lx105\.nuget\packages\alibabacloud.sdk.ocr-api20210707\1.1.15\lib\netstandard2.0\AlibabaCloud.SDK.Ocr-api20210707.dll");
    File.WriteAllText(@"d:\Documents\Ariel Programs\MyTranslate\sdk_check.txt", "Loaded: " + asm.FullName + "\n");
    foreach (var t in asm.GetTypes())
    {
        if (t.Name.StartsWith("RecognizeGeneral"))
        {
            File.AppendAllText(@"d:\Documents\Ariel Programs\MyTranslate\sdk_check.txt", t.FullName + "\n");
            foreach (var p in t.GetProperties())
                File.AppendAllText(@"d:\Documents\Ariel Programs\MyTranslate\sdk_check.txt", $"  {p.Name}: {p.PropertyType.FullName}\n");
        }
    }
}
catch (Exception ex)
{
    File.WriteAllText(@"d:\Documents\Ariel Programs\MyTranslate\sdk_check.txt", "ERROR: " + ex.Message + "\n" + ex.StackTrace);
}
