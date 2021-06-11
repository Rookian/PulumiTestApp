using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.ResourceManager.KeyVault.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Microsoft.Graph;
using KeyType = Azure.Security.KeyVault.Keys.KeyType;
using Permissions = Azure.ResourceManager.KeyVault.Models.Permissions;
using Sku = Azure.ResourceManager.KeyVault.Models.Sku;
using SkuName = Azure.ResourceManager.KeyVault.Models.SkuName;

namespace PulumiTestApp
{
    public class PulumiPreProvisioning
    {
        public static async Task<Result> PreparePulumi(PulumiResourceConfiguration configuration)
        {
         
            var graphClient = new GraphServiceClient(new AzureCliCredential());
            var currentUser = await graphClient.Me.Request().GetAsync();

            var managementClient = new ResourcesManagementClient(configuration.SubscriptionId, new AzureCliCredential());
            await managementClient.ResourceGroups.CreateOrUpdateAsync(
                configuration.ResourceGroupName,
                new Azure.ResourceManager.Resources.Models.ResourceGroup(configuration.Location));

            var keyVaultManagementClient =
                new Azure.ResourceManager.KeyVault.KeyVaultManagementClient(configuration.SubscriptionId, new AzureCliCredential());

            //await keyVaultManagementClient.Vaults.StartPurgeDeletedAsync(configuration.VaultName,
            //    configuration.Location);

            var vault = await keyVaultManagementClient.Vaults.StartCreateOrUpdateAsync(
                configuration.ResourceGroupName, configuration.VaultName,
                new VaultCreateOrUpdateParameters(configuration.Location, new VaultProperties(configuration.TenantId, new Sku(SkuName.Standard))
                {
                    AccessPolicies = new List<AccessPolicyEntry>
                    {
                        new(configuration.TenantId, currentUser.Id, new Permissions
                        {
                            Keys = new List<KeyPermissions>
                            {
                                KeyPermissions.List, KeyPermissions.Decrypt, KeyPermissions.Encrypt, KeyPermissions.Get, KeyPermissions.Create
                            }
                        })
                    }
                }));

            var vaultResponse = await vault.WaitForCompletionAsync();

            var keyClient = new Azure.Security.KeyVault.Keys.KeyClient(new Uri(vaultResponse.Value.Properties.VaultUri), new AzureCliCredential());
            await keyClient.CreateKeyAsync(configuration.KeyName, KeyType.Rsa);

            var storageManagementClient = new StorageManagementClient(configuration.SubscriptionId, new AzureCliCredential());
            var storageOperation = await storageManagementClient.StorageAccounts.StartCreateAsync(configuration.ResourceGroupName, configuration.AccountName,
                new StorageAccountCreateParameters(
                    new Azure.ResourceManager.Storage.Models.Sku(Azure.ResourceManager.Storage.Models.SkuName.StandardLRS),
                    Kind.StorageV2, configuration.Location));

            var storageResponse = await storageOperation.WaitForCompletionAsync();
            var response = await storageManagementClient.BlobContainers.CreateAsync(configuration.ResourceGroupName, configuration.AccountName, configuration.ContainerName,
                new BlobContainer
                {
                    PublicAccess = PublicAccess.None
                });
            var keys = await storageManagementClient.StorageAccounts.ListKeysAsync(configuration.ResourceGroupName, configuration.AccountName);

            return new Result(keys.Value.Keys.First().Value);
        }
    }

    public record PulumiResourceConfiguration(
        string SubscriptionId,
        string Location,
        Guid TenantId,
        string VaultName,
        string ResourceGroupName,
        string ContainerName,
        string AccountName,
        string KeyName
    );

    public record Result(
        string StorageKey
    );
}