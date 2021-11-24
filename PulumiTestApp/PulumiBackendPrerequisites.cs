using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.KeyVault.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Azure.Security.KeyVault.Keys;
using KeyType = Azure.Security.KeyVault.Keys.KeyType;
using Permissions = Azure.ResourceManager.KeyVault.Models.Permissions;
using Sku = Azure.ResourceManager.KeyVault.Models.Sku;
using SkuName = Azure.ResourceManager.KeyVault.Models.SkuName;

namespace PulumiTestApp
{
    public static class PulumiBackendPrerequisites
    {
        public static async Task<PulumiBackendProvisioningResult> RunProvisioning(PulumiBackendConfiguration configuration)
        {
            await CreateResourceGroup(configuration.SubscriptionId, configuration.Location, configuration.ResourceGroupName);
            var (keyVaultKey, keyVaultUri) = await CreateAzureKeyVault(configuration.TenantId, configuration.SubscriptionId, configuration.ResourceGroupName,
                configuration.Location, configuration.VaultName, configuration.KeyName);

            var storageKeys = await CreateBlobStorage(configuration.SubscriptionId, configuration.ResourceGroupName,
                configuration.Location, configuration.AccountName, configuration.ContainerName);

            return new PulumiBackendProvisioningResult(storageKeys, keyVaultKey, keyVaultUri);
        }

        private static async Task CreateResourceGroup(string subscriptionId, string location, string resourceGroupName)
        {
            var managementClient = new ResourcesManagementClient(subscriptionId, new AzureCliCredential());
            await managementClient.ResourceGroups.CreateOrUpdateAsync(
                resourceGroupName,
                new Azure.ResourceManager.Resources.Models.ResourceGroup(location));
        }

        private static async Task<string> CreateBlobStorage(string subscriptionId, string resourceGroupName,
            string location, string accountName, string containerName)
        {
            var storageManagementClient = new StorageManagementClient(subscriptionId, new AzureCliCredential());
            var storageOperation = await storageManagementClient.StorageAccounts.StartCreateAsync(
                resourceGroupName, accountName,
                new StorageAccountCreateParameters(
                    new Azure.ResourceManager.Storage.Models.Sku(Azure.ResourceManager.Storage.Models.SkuName.StandardLRS),
                    Kind.StorageV2, location));

            await storageOperation.WaitForCompletionAsync();
            await storageManagementClient.BlobContainers.CreateAsync(resourceGroupName, accountName,
                containerName,
                new BlobContainer
                {
                    PublicAccess = PublicAccess.None
                });


            var storageKeys =
                await storageManagementClient.StorageAccounts.ListKeysAsync(resourceGroupName, accountName);
            return storageKeys.Value.Keys.First().Value;
        }

        private static async Task<(KeyVaultKey keyVaultKey, Uri keyVaultUri)> CreateAzureKeyVault(Guid tenantId, string subscriptionId, string resourceGroupName,
            string location, string vaultName, string keyName)
        {
            var credential = new AzureCliCredential();
            var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { "https://graph.microsoft.com/.default" }));
            var accessToken = token.Token;

            var jwtSecurityToken = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
            var objectId = jwtSecurityToken.Payload.GetValueOrDefault("sub")?.ToString();

            var keyVaultManagementClient =
                new Azure.ResourceManager.KeyVault.KeyVaultManagementClient(subscriptionId,
                    new AzureCliCredential());

            var vault = await keyVaultManagementClient.Vaults.StartCreateOrUpdateAsync(
                resourceGroupName, vaultName,
                new VaultCreateOrUpdateParameters(location,
                    new VaultProperties(tenantId, new Sku(SkuName.Standard))
                    {
                        AccessPolicies = new List<AccessPolicyEntry>
                        {
                            new(tenantId, objectId, new Permissions
                            {
                                Keys = new List<KeyPermissions>
                                {
                                    KeyPermissions.List, KeyPermissions.Decrypt, KeyPermissions.Encrypt, KeyPermissions.Get,
                                    KeyPermissions.Create
                                },
                                Secrets = new List<SecretPermissions>()
                                {
                                    SecretPermissions.Get, SecretPermissions.List
                                }
                            })
                        }
                    }));

            var vaultResponse = await vault.WaitForCompletionAsync();

            var vaultUri = vaultResponse.Value.Properties.VaultUri;
            var keyVaultKey = await CreateKeyIfNotExist(keyName, vaultUri);
            return (keyVaultKey, new Uri(vaultUri));
        }

        private static async Task<KeyVaultKey> CreateKeyIfNotExist(string keyName, string vaultUri)
        {
            var keyClient = new KeyClient(new Uri(vaultUri), new AzureCliCredential());

            try
            {
                return (await keyClient.GetKeyAsync(keyName)).Value;
            }
            catch (Exception)
            {
                return (await keyClient.CreateKeyAsync(keyName, KeyType.Rsa)).Value;
            }
        }
    }

    public record PulumiBackendConfiguration
    {
        public string SubscriptionId { get; init; }
        public string Location { get; init; }
        public Guid TenantId { get; init; }
        public string VaultName { get; init; }
        public string ResourceGroupName { get; init; }
        public string ContainerName { get; init; }
        public string AccountName { get; init; }
        public string KeyName { get; init; }
    }

    public record PulumiBackendProvisioningResult(string StorageKey, KeyVaultKey KeyVaultKey, Uri KeyVaultUri);
}