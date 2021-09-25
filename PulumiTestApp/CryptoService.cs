using System;
using System.Text;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace PulumiTestApp
{
    public interface ICryptoService
    {
        Task<string> Encrypt(string content);
        Task<string> Decrypt(string encryptedContent);
    }

    public class CryptoService : ICryptoService
    {
        private readonly CryptographyClient _client;

        public CryptoService(Uri keyIdentifier)
        {
            _client = new CryptographyClient(keyIdentifier, new ChainedTokenCredential(new ManagedIdentityCredential(), new AzureCliCredential()));
        }

        public async Task<string> Encrypt(string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            var encryptResult = await _client.EncryptAsync(EncryptionAlgorithm.Rsa15, bytes);
            return Convert.ToBase64String(encryptResult.Ciphertext);
        }

        public async Task<string> Decrypt(string encryptedContent)
        {
            var bytes = Convert.FromBase64String(encryptedContent);
            var decryptResult = await _client.DecryptAsync(EncryptionAlgorithm.Rsa15, bytes);
            var plainText = Encoding.UTF8.GetString(decryptResult.Plaintext);
            return plainText;
        }
    }
}