using System;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using System.Data;
using System.Data.Common;
using System.Text;
using Mono;
//using FirebirdSql.Data.FirebirdClient;
//using Mono.Data.Sqlite;
//using MongoDB.Driver;
//using MongoDB.Bson;
using System.Configuration;

using Kraken.Util;
using Kraken.Core;

namespace Kraken.CommandLine
{
    public class Test
    {
        public static void Main5(string[] args)
        {
            const string accessKeyID = "<replace_with_access_id>";
            const string secretKeyID = "<replace_with_secret_id>";
            S3Store s3Store = new S3Store(accessKeyID, secretKeyID);
            const string sourcePath = "../../AssemblyInfo.cs";
            const string s3Path = "bonzailab/testme/please/AssemblyInfo.cs";
            const string newS3Path = "bonzailab/hello/world/you/test.cs";
            const string destPath = "./AssemblyInfo.cs";
            s3Store.SaveFile(sourcePath, s3Path);
            s3Store.MovePath(s3Path, newS3Path);
            s3Store.GetFile(newS3Path, destPath);
            Console.WriteLine("Done");
        }
        
        public static void Main3(string[] args)
        {
            NameValueCollection appSettings = ConfigurationManager.AppSettings;
            PathStore pathStore = new PathStore(appSettings);
            const string fromPath = "../../Main.cs";
            const string toPath = "test/Main.cs";
            Kraken.Core.Path path = pathStore.SavePath(fromPath, toPath);
            BlobStream blob = pathStore.PathToBlob(path);
            Console.WriteLine("File: {0} => {1} as {2}", fromPath, toPath, path.Envelope.Checksum);
            PrintToConsole(blob);
            BlobStream blob2 = pathStore.GetBlob(toPath);
            PrintToConsole(blob2);
            string filePath = "../..";
            pathStore.SaveFolder(filePath, "test");
            // let's now delete test. this should fail.
            //pathStore.DeletePath("test/bin");
            Console.WriteLine("done!");
        }
        
        public static void Main4(string[] args)
        {
            NameValueCollection appSettings = ConfigurationManager.AppSettings;
            BlobStore store = new BlobStore(appSettings);
            string filePath = "../../Main.cs";
            string checksum = store.SaveBlob(filePath);
            using (BlobStream b = store.OpenBlob(checksum))
            {
                Console.WriteLine("File: {0} => {1}", filePath, checksum);
                PrintToConsole(b);
            }
            int number = 1025;
            Console.WriteLine("Little Endian? {0}", BitConverter.IsLittleEndian);
            Console.WriteLine("Number {0}: ", number);
            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);
            writer.Write(number);
            writer.Flush();
            // I want to read the bytes out... // let's convert these to HexString.
            // the question is... do I swap at the point of val or after it's been read?
            byte[] val = ms.ToArray();
            byte[] reversedVal = ByteUtil.ReverseEndian(val);
            string output = ByteUtil.ByteArrayToHexString(val);
            Console.WriteLine("Current Endian: {0}; {1}", output, ByteUtil.ByteArrayToString(val));
            Console.WriteLine("Reverse Endian: {0}; {1}", ByteUtil.ByteArrayToHexString(reversedVal), ByteUtil.ByteArrayToString(reversedVal));
            byte[] input = ByteUtil.HexStringToByteArray(output);
            BinaryReader reader = new BinaryReader(new MemoryStream(input));
            int readIn = reader.ReadInt32();
            Console.WriteLine("Read-Back Number Is: {0}", readIn);
            Console.WriteLine("Read-Back Reversed Endian Is: {0}", ReadBigEndianInt32(new MemoryStream(reversedVal)));
            Console.WriteLine("Read Big Endian Is: {0}", ReadBigEndianInt32(reversedVal));
            DateTime now = DateTime.UtcNow;
            DateTime now1 = DateTime.Parse(now.ToString("o")).ToUniversalTime(); 
            Console.WriteLine("Now is {0}, and {1}... equal? {2}", now.ToString("o"), now1.ToString("o"), now.Equals(now1));
            
        }
        
        public static Int32 ReadBigEndianInt32(byte[] bytes)
        {
            int i = 0;
            return (((int)bytes[i + 0]) << 24)
                | (((int)bytes[i + 1]) << 16)
                    | (((int)bytes[i + 2]) << 8)
                    | (((int)bytes[i + 3]) << 0);
        }
        
        public static Int32 ReadLittleEndianInt32(byte[] bytes)
        {
            int i = 0;
            return (((int)bytes[i + 0]) << 0)
                | (((int)bytes[i + 1]) << 8)
                    | (((int)bytes[i + 2]) << 16)
                    | (((int)bytes[i + 3]) << 24);
        }
        
        
        public static int ReadBigEndianInt32(Stream s)
        {
            // we'll need to reverse the values that are being read.
            byte[] bytes = new byte[4];
            int bytesRead = s.Read(bytes, 0, 4);
            if (BitConverter.IsLittleEndian)
            {
                bytes = ByteUtil.ReverseEndian(bytes);
            } 
            return (new BinaryReader(new MemoryStream(bytes))).ReadInt32();
        }
        
        public static void PrintToConsole(Stream s)
        {
            using (StreamReader reader = new StreamReader(s)) {
                string allText = reader.ReadToEnd();
                Console.WriteLine(allText);
            }
        }
    }
}

