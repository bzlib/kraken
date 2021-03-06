using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using Kraken.Util;

namespace Kraken.Core
{
    public class BlobEnvelope {
        ResourceType type = ResourceType.Blob; // this defines the type - perhaps it's outside of the header format.
        public short Version { get ; set; }  // this is the version of the envelope. // 4 bytes --> too much anyways.
        public string Checksum { get; set; }
        public long OriginalLength { get; set; }// tells us how big the file is... and perhaps with a pointer to something longer.
        public CompressionType CompressionScheme { get; set; } // do we want to serialize the string or the number? we'll do string.
        public EncryptionType EncryptionScheme { get ; set ; }
        public byte[] EncryptionIV { get; set; } // we'll always have an encryption IV even when there aren't enryption scheme to make things uniform.

        public BlobEnvelope(short ver, string checksum, long origLen, CompressionType ctype, EncryptionType etype, byte[] iv)
        {
            Version = ver;
            Checksum = checksum;
            OriginalLength = origLen;
            CompressionScheme = ctype;
            EncryptionScheme = etype;
            EncryptionIV = iv;
        }

        public BlobEnvelope() { 
            //Version = 1;
        } 

        public static BlobEnvelope Parse(Reader reader)
        {
            Regex splitter = new Regex(@"\s+");
            string line = reader.ReadLine();
            string[] values = splitter.Split(line);
            // envelope format.
            // 'blob' -> tells us that this is a blob file.
            // <version> -> this tells us which version should be used handle the rest of the header.
            // <original_length>
            // <compress> -> gzip/none
            // <encryption> -> aes128/aes256/none
            // <iv> -> we should always have an IV even when there aren't encryption.
            // <keyvals> -> arbitrary headers held in querystring format.
            
            if (values.Length < 7)
            {
                throw new Exception(string.Format("error_invalid_header_preamble: {0}", line));
            }
            if (!values [0].Equals("blob", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("incorrect_blob_envelope_format_not_start_with_blob");
            }
            short version = short.Parse(values[1]);
            string checksum = values[2];
            long origSize = long.Parse(values[3]);
            CompressionType cType = CompressUtil.StringToCompressionType(values[4]);
            EncryptionType eType = EncryptionUtil.StringToEncryptionType(values[5]); // things aren't by default encrypted... but we would want it soon.
            byte[] iv = ByteUtil.HexStringToByteArray(values[6]);
            return new BlobEnvelope(version, checksum, origSize, cType, eType, iv);
        }

        public byte[] Serialize()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0} {1} {2} {3} {4} {5} {6}\r\n"
                            , type
                            , Version
                            , Checksum
                            , OriginalLength
                            , CompressionScheme
                            , EncryptionScheme
                            , ByteUtil.ByteArrayToHexString(EncryptionIV));
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public void WriteTo(Stream s) {
            byte[] bytes = Serialize();
            s.Write(bytes, 0, bytes.Length);
        }
    }
}

