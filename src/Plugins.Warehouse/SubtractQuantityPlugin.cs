using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
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

                var item = service.Retrieve(StockChange.ItemEntity, change.Key, new ColumnSet(StockChange.AvailableQuantity));
                long available = item.GetAttributeValue<int?>(StockChange.AvailableQuantity) ?? 0;
                var balance = available + change.Value;

                // Re-checked here because another save may have used the stock since PreValidation ran.
                if (balance < 0)
                {
                    throw new InvalidPluginExecutionException(
                        $"Not enough product in stock. Available: {available}, requested: {-change.Value}.");
                }

                if (balance > int.MaxValue)
                {
                    throw new InvalidPluginExecutionException(
                        $"The available quantity can't exceed {int.MaxValue}. Available: {available}, adding: {change.Value}.");
                }

                // Fails instead of overwriting when another save changed the item after it was read above.
                service.Execute(new UpdateRequest
                {
                    Target = new Entity(StockChange.ItemEntity, change.Key)
                    {
                        [StockChange.AvailableQuantity] = (int)balance,
                        RowVersion = item.RowVersion
                    },
                    ConcurrencyBehavior = string.IsNullOrEmpty(item.RowVersion)
                        ? ConcurrencyBehavior.Default
                        : ConcurrencyBehavior.IfRowVersionMatches
                });
            }
        }
    }
}
