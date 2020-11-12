using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace ComPack
{
    public class Cryptor
    {
        private string PassPhrase = string.Empty;
        private string serialNumber = string.Empty;
        private string cpuInfo = string.Empty;
        private string driveLetter = "C:";

        public Cryptor()
        {
            DefaultPassPhrase();
        }

        public void DefaultPassPhrase()
        {
            try
            {
                ManagementClass managClass = new ManagementClass("win32_processor");
                ManagementObjectCollection managCollec = managClass.GetInstances();

                foreach (ManagementObject managObj in managCollec)
                {
                    cpuInfo = managObj.Properties["processorID"].Value.ToString();
                    break;
                }
            }
            catch (Exception)
            {
                cpuInfo = "CrYpToR1!";
            }

            try
            {
                ManagementObject disk = new ManagementObject("win32_logicaldisk.deviceid=\"" + driveLetter + ":\"");
                disk.Get();
                serialNumber = disk["VolumeSerialNumber"].ToString();
            }
            catch (Exception)
            {
                serialNumber = "cRyPtOr2!";
            }

            cpuInfo = (cpuInfo.Length < 8 ? "CrYpToR1" : cpuInfo);
            serialNumber = (serialNumber.Length < 5 ? "cRyPtOr2" : serialNumber);

            SetPassPhrase(cpuInfo + "_" + serialNumber);
        }

        public void SetPassPhrase(string key)
        {
            PassPhrase = key;
        }

        //=========================================================================
        // Encripts a string based on a passphrase.
        // http://www.dotnetfunda.com/articles/article1808-encrypt-and-decrypt-query-string.aspx
        //
        // Here are some handy string manipulation examples:
        //   System.Text.UTF8Encoding UTF8 = new System.Text.UTF8Encoding();
        //   Byte[] encodedBytes = UTF8.GetBytes("HELLO WORLD! \u00C4 \uD802\u0033 \u00AE");
        //   string test = Convert.ToBase64String(encodedBytes);
        //   encodedBytes = Convert.FromBase64String(test);
        //   lblMsg.Text = UTF8.GetString(encodedBytes);
        //=========================================================================
        public string EncryptString(string Message)
        {
            byte[] Results;
            System.Text.UTF8Encoding UTF8 = new System.Text.UTF8Encoding();
            // Step 1. We hash the passphrase using MD5
            // We use the MD5 hash generator as the result is a 128 bit byte array
            // which is a valid length for the TripleDES encoder we use below
            MD5CryptoServiceProvider HashProvider = new MD5CryptoServiceProvider();
            byte[] TDESKey = HashProvider.ComputeHash(UTF8.GetBytes(PassPhrase));

            // Step 2. Create a new TripleDESCryptoServiceProvider object
            TripleDESCryptoServiceProvider TDESAlgorithm = new TripleDESCryptoServiceProvider();

            // Step 3. Setup the encoder
            TDESAlgorithm.Key = TDESKey;
            TDESAlgorithm.Mode = CipherMode.ECB;
            TDESAlgorithm.Padding = PaddingMode.PKCS7;

            // Step 4. Convert the input string to a byte[]
            byte[] DataToEncrypt = UTF8.GetBytes(Message.Replace("/", "-").Replace("+", "_"));

            // Step 5. Attempt to encrypt the string
            try
            {
                ICryptoTransform Encryptor = TDESAlgorithm.CreateEncryptor();
                Results = Encryptor.TransformFinalBlock(DataToEncrypt, 0, DataToEncrypt.Length);
            }
            finally
            {
                // Clear the TripleDes and Hashprovider services of any sensitive information
                TDESAlgorithm.Clear();
                HashProvider.Clear();
            }

            // Step 6. Return the encrypted string as a base64 encoded string
            return Convert.ToBase64String(Results);
        }


        //=========================================================================
        // Decrypt a string based on a passphrase
        // http://www.dotnetfunda.com/articles/article1808-encrypt-and-decrypt-query-string.aspx
        //=========================================================================
        public string DecryptString(string Message)
        {
            byte[] Results;
            string finalResults;

            UTF8Encoding UTF8 = new UTF8Encoding();
            // Step 1. We hash the passphrase using MD5
            // We use the MD5 hash generator as the result is a 128 bit byte array
            // which is a valid length for the TripleDES encoder we use below
            MD5CryptoServiceProvider HashProvider = new MD5CryptoServiceProvider();
            byte[] TDESKey = HashProvider.ComputeHash(UTF8.GetBytes(PassPhrase));

            // Step 2. Create a new TripleDESCryptoServiceProvider object
            TripleDESCryptoServiceProvider TDESAlgorithm = new TripleDESCryptoServiceProvider();

            // Step 3. Setup the decoder
            TDESAlgorithm.Key = TDESKey;
            TDESAlgorithm.Mode = CipherMode.ECB;
            TDESAlgorithm.Padding = PaddingMode.PKCS7;

            // Step 4. Convert the input string to a byte[]
            byte[] DataToDecrypt = Convert.FromBase64String(Message.Replace("-", "/").Replace("_", "+"));

            // Step 5. Attempt to decrypt the string
            try
            {
                ICryptoTransform Decryptor = TDESAlgorithm.CreateDecryptor();
                Results = Decryptor.TransformFinalBlock(DataToDecrypt, 0, DataToDecrypt.Length);
                finalResults = UTF8.GetString(Results);
            }
            finally
            {
                // Clear the TripleDes and Hashprovider services of any sensitive information
                TDESAlgorithm.Clear();
                HashProvider.Clear();
            }

            // Step 6. Return the decrypted string in UTF8 encoded format
            return UTF8.GetString(Results);
        }
    }
}
