using Microsoft.Xrm.Sdk;
using TALXIS.DevKit.Build.Dataverse.Plugin;

namespace Plugins.Warehouse
{
    [PluginStep("Create", "lab_warehousetransaction", PluginStage.PostOperation)]
    [PluginStep("Update", "lab_warehousetransaction", PluginStage.PostOperation,
        Name = UpdateStepName, FilteringAttributes = new[] { "lab_quantity", "lab_itemid", "lab_transactiontype" })]
    [PluginStepImage(PluginImageType.PreImage, "PreImage", "lab_quantity", "lab_itemid", "lab_transactiontype", StepName = UpdateStepName)]
    public class SubtractQuantityPlugin : PluginBase
    {
        private const string UpdateStepName = "Plugins.Warehouse.SubtractQuantityPlugin: Update of lab_warehousetransaction";

        public SubtractQuantityPlugin() : base(typeof(SubtractQuantityPlugin))
        {
        }

        public SubtractQuantityPlugin(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(SubtractQuantityPlugin))
        {
        }

        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            var context = localPluginContext.PluginExecutionContext;
            var service = localPluginContext.OrgSvcFactory.CreateOrganizationService(context.UserId);
            var changes = StockChange.Calculate(context, service);
            if (changes == null)
            {
                return;
            }

            foreach (var change in changes)
            {
                if (change.Value == 0)
                {
                    continue;
                }

                var available = StockChange.GetAvailable(service, change.Key);
                service.Update(new Entity(StockChange.ItemEntity, change.Key)
                {
                    [StockChange.AvailableQuantity] = available + change.Value
                });
            }
        }
    }
}
