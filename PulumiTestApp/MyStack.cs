using System.Net.Http;
using Pulumi;
using Pulumi.AzureAD;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Sql;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;
using ManagedServiceIdentityType = Pulumi.AzureNative.Web.ManagedServiceIdentityType;
using Pulumi.AzureNative.Sql.Inputs;
using Output = Pulumi.Output;
using PrincipalType = Pulumi.AzureNative.Sql.PrincipalType;

// ReSharper disable ObjectCreationAsStatement

namespace PulumiTestApp
{
    public class MyStack : Stack
    {
        [Output]
        public Output<string> SqlConnectionString { get; set; }

        [Output]
        public Output<string> WebAppManagedIdentity { get; set; }

        public MyStack(StackConfig config, ICryptoService cryptoService)
        {
            var resourceGroup = new ResourceGroup($"myapp-{config.Environment}", new ResourceGroupArgs
            {
                Location = config.Location,
                ResourceGroupName = $"myapp-{config.Environment}"
            });

            var plan = new AppServicePlan("pulitestserviceplan", new AppServicePlanArgs
            {
                Location = config.Location,
                ResourceGroupName = resourceGroup.Name,
                Kind = "Linux",
                Reserved = true,
                Sku = new SkuDescriptionArgs { Size = "B1", Tier = "Standard", Name = "B1" }
            });

            var webApp = new WebApp("myApp", new WebAppArgs
            {
                Location = config.Location,
                ResourceGroupName = resourceGroup.Name,
                Identity = new ManagedServiceIdentityArgs { Type = ManagedServiceIdentityType.SystemAssigned },
                ServerFarmId = plan.Id
            });

            const string administratorLogin = "myloginforsql";
            var administratorLoginPassword = Output.Create(cryptoService.Decrypt("VrKPB8XGAwQnF/qgf22370+I0OILIo3DvqSKc4voVVjyvutPIYCvRN6TS/fQZBjtv0UnRw6tD9xGYy/+WA6AMG90R64tfNhVjKMlgoYcKPcz1Bx09YMdu2584wU9Qz6vvxwsVzZthio4mMNd90XqhttsWLkUtEZBGbJGRRzykcTACGpiq6+A1hoOvNPMXOTfJUKCZuEbjD3tE5b+bwtjqKae97OmW66L5fKUttnR3tS9jMhU3KeWPve4mnP8zQnAs0AIjuSoycPOb+ZO09vkR+glhgWyhwYm3lqBxC+75ZlpRYnD17dSCN88aNOdncZJgTAhzKV5AbpSsEjTOT1hgw=="));


            var sqlServer = new Server("myServer", new ServerArgs
            {
                AdministratorLogin = administratorLogin,
                AdministratorLoginPassword = administratorLoginPassword,
                ResourceGroupName = resourceGroup.Name,
                Identity = new ResourceIdentityArgs { Type = IdentityType.SystemAssigned },
                Administrators = new ServerExternalAdministratorArgs
                {
                    PrincipalType = PrincipalType.Application,
                    Login = config.DeploymentAccount.Name,
                    Sid = config.DeploymentAccount.ObjectId
                }
            });

            // This is only possible with a service principal that is a global admin
            // Alternative: Put SQL Server MI in predefined Azure Ad group that is part of directory readers 
            // (only possible with Azure AD premium)
            new DirectoryRoleMember("Sql Server Directory reader role", new DirectoryRoleMemberArgs()
            {
                RoleObjectId = new DirectoryRole("Directory Readers", new DirectoryRoleArgs
                {
                    DisplayName = "Directory Readers"
                }).ObjectId,
                MemberObjectId = sqlServer.Identity.Apply(x => x.PrincipalId)
            });

            //new GroupMember("SqlServers-myServer", new GroupMemberArgs
            //{
            //    GroupObjectId = Group.Get("SqlServer", "a25019f7-d9e1-4292-9d22-e251b8d18d62").ObjectId,
            //    MemberObjectId = sqlServer.Identity.Apply(x => x.PrincipalId)
            //});

            var database = new Database("myDb", new DatabaseArgs
            {
                ResourceGroupName = resourceGroup.Name,
                ServerName = sqlServer.Name,
                Sku = new SkuArgs { Family = "Gen5", Name = "Basic", Tier = "Basic" },
            });

            new FirewallRule("myServerFWApps", new FirewallRuleArgs
            {
                StartIpAddress = "0.0.0.0",
                EndIpAddress = "0.0.0.0",
                FirewallRuleName = "apps",
                ResourceGroupName = resourceGroup.Name,
                ServerName = sqlServer.Name
            });


            var ipTask = new HttpClient().GetStringAsync("https://api.ipify.org");
            new FirewallRule("myServerFWMyIp", new FirewallRuleArgs
            {
                StartIpAddress = Output.Create(ipTask),
                EndIpAddress = Output.Create(ipTask),
                FirewallRuleName = "my ip",
                ResourceGroupName = resourceGroup.Name,
                ServerName = sqlServer.Name
            });

            WebAppManagedIdentity = webApp.Name;
            SqlConnectionString = Output.Format($"Server=tcp:{sqlServer.Name}.database.windows.net;initial catalog={database.Name};Authentication=Active Directory Default;");
        }
    }
}