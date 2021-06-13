using Pulumi;
using Pulumi.Azure.AppService;
using Pulumi.Azure.AppService.Inputs;
using Pulumi.Azure.Core;

namespace PulumiTestApp
{
    public class MyStack : Stack
    {
        public MyStack(MyConfig config)
        {
            var resourceGroup = new ResourceGroup("myapp", new ResourceGroupArgs
            {
                Location = config.Location,
            });

            var plan = new Plan("pulitestserviceplan", new PlanArgs
            {
                Location = config.Location,
                ResourceGroupName = resourceGroup.Name,
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