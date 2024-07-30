using System;
using System.IO;
using System.Security.Cryptography;

namespace HT.Framework.Deployment
{
    /// <summary>
    /// 实用工具
    /// </summary>
    public static class DeploymentUtility
    {
        /// <summary>
        /// 获取文件的MD5校验码
        /// </summary>
        /// <param name="path">文件路径</param>
        public static string GetFileMD5(string path)
        {
            if (!File.Exists(path))
                return null;

            MD5 hash = MD5.Create();
            FileStream stream = File.Open(path, FileMode.Open);
            byte[] hashByte = hash.ComputeHash(stream);
            stream.Close();
            hash.Dispose();
            return BitConverter.ToString(hashByte).Replace("-", "");
        }
        /// <summary>
        /// 获取AB包的校验码
        /// </summary>
        /// <param name="path">AB包的 .manifest 文件路径</param>
        public static string GetCRC(string path)
        {
            if (!File.Exists(path))
                return null;

            string[] content = File.ReadAllLines(path);
            if (content.Length >= 2 && content[1].StartsWith("CRC:"))
            {
                string crc = content[1].Replace("CRC:", "").Trim();
                return crc;
            }
            return null;
        }
        /// <summary>
        /// 获取文件大小（KB）
        /// </summary>
        /// <param name="path">文件路径</param>
        public static int GetFileSize(string path)
        {
            if (!File.Exists(path))
                return 0;

            FileInfo file = new FileInfo(path);
            int size = (int)(file.Length / 1000);
            if (size < 0) size = 0;
            return size;
        }
    }
}