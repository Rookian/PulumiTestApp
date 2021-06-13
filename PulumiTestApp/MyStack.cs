using Pulumi;
using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.Core;

namespace PulumiTestApp
{
    public class MyStack : Stack
    {
        public MyStack(StackConfig config)
        {
            var resourceGroup = new ResourceGroup($"myapp-{config.Environment}", new ResourceGroupArgs
            {
                Location = config.Location,
                Name = $"myapp-{config.Environment}"
            });

            var plan = new Plan("pulitestserviceplan", new PlanArgs
            {
                Location = config.Location,
                ResourceGroupName = resourceGroup.Name,
                Kind = "Linux",
                Reserved = true,
                Sku = new PlanSkuArgs { Tier = "Standard", Size = "S1" }
            });

            var appService = new AppService("pulitestservice", new AppServiceArgs
            {
                AppServicePlanId = plan.Id,
                Location = config.Location,
                ResourceGroupName = resourceGroup.Name,
            });
        }
    }
}