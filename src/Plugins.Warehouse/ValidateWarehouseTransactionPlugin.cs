using System;
using Microsoft.Xrm.Sdk;
using TALXIS.DevKit.Build.Dataverse.Plugin;

namespace Plugins.Warehouse
{
    [PluginStep("Create", "lab_warehousetransaction", PluginStage.PreValidation)]
    [PluginStep("Update", "lab_warehousetransaction", PluginStage.PreValidation,
        Name = UpdateStepName, FilteringAttributes = new[] { "lab_quantity", "lab_itemid", "lab_transactiontype" })]
    [PluginStepImage(PluginImageType.PreImage, "PreImage", "lab_quantity", "lab_itemid", "lab_transactiontype", StepName = UpdateStepName)]
    public class ValidateWarehouseTransactionPlugin : PluginBase
    {
        private const string UpdateStepName = "Plugins.Warehouse.ValidateWarehouseTransactionPlugin: Update of lab_warehousetransaction";

        public ValidateWarehouseTransactionPlugin() : base(typeof(ValidateWarehouseTransactionPlugin))
        {
        }

        public ValidateWarehouseTransactionPlugin(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(ValidateWarehouseTransactionPlugin))
        {
        }

        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            var context = localPluginContext.PluginExecutionContext;

            try
            {
                var service = localPluginContext.OrgSvcFactory.CreateOrganizationService(context.UserId);
                var changes = StockChange.Calculate(context, service);
                if (changes == null)
                {
                    return;
                }

                foreach (var change in changes)
                {
                    if (change.Value >= 0)
                    {
                        continue;
                    }

                    var available = StockChange.GetAvailable(service, change.Key);
                    var requested = -change.Value;
                    if (requested > available)
                    {
                        throw new InvalidPluginExecutionException(
                            $"Not enough product in stock. Available: {available}, requested: {requested}.");
                    }
                }
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                localPluginContext.Trace($"ValidateWarehouseTransactionPlugin failed: {ex}");
                throw;
            }
        }
    }
}
