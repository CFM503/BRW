using System;
using System.IO;

class PatchPE
{
    static int Main(string[] args)
    {
        try
        {
            string file = args.Length > 0 ? args[0] : "EBSBrowser.exe";
            if (!File.Exists(file))
            {
                Console.WriteLine("[DEP优化错误] 找不到目标文件: " + file);
                return 1;
            }

            byte[] bytes = File.ReadAllBytes(file);
            int peOffset = BitConverter.ToInt32(bytes, 0x3C);
            int optHeaderOffset = peOffset + 4 + 20;
            int dllCharOffset = optHeaderOffset + 70;

            ushort dllChar = BitConverter.ToUInt16(bytes, dllCharOffset);
            dllChar = (ushort)(dllChar & ~0x0100 & ~0x0040); // Clear IMAGE_DLLCHARACTERISTICS_NX_COMPAT (0x100) and DYNAMIC_BASE (0x40)

            byte[] newBytes = BitConverter.GetBytes(dllChar);
            bytes[dllCharOffset] = newBytes[0];
            bytes[dllCharOffset + 1] = newBytes[1];

            for (int retry = 0; retry < 10; retry++)
            {
                try
                {
                    File.WriteAllBytes(file, bytes);
                    Console.WriteLine("[DEP/ASLR优化] 成功解除 NX_COMPAT 与 DYNAMIC_BASE 限制，允许 Java 6 原生执行！");
                    return 0;
                }
                catch
                {
                    System.Threading.Thread.Sleep(200);
                }
            }
            Console.WriteLine("[DEP/ASLR优化警告] 写入超时");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[DEP优化警告] " + ex.Message);
            return 0;
        }
    }
}
