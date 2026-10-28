using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Plugins.Warehouse
{
    /// <summary>
    /// Works out how a create or update of a lab_warehousetransaction changes the available quantity of its items.
    /// </summary>
    internal static class StockChange
    {
        public const string TransactionEntity = "lab_warehousetransaction";
        public const string ItemEntity = "lab_warehouseitem";
        public const string Quantity = "lab_quantity";
        public const string ItemId = "lab_itemid";
        public const string TransactionType = "lab_transactiontype";
        public const string AvailableQuantity = "lab_availablequantity";
        public const string PreImageName = "PreImage";
        public const int Inbound = 100000000;
        public const int Outbound = 100000001;
        private const int PostOperationStage = 40;

        public static readonly string[] TrackedAttributes = { Quantity, ItemId, TransactionType };

        /// <summary>
        /// Net change of available quantity per item, or null when the message doesn't touch stock.
        /// </summary>
        public static Dictionary<Guid, long> Calculate(IPluginExecutionContext context, IOrganizationService service)
        {
            if (!(context.InputParameters.TryGetValue("Target", out var targetObject) && targetObject is Entity target)
                || target.LogicalName != TransactionEntity)
            {
                return null;
            }

            var changes = new Dictionary<Guid, long>();

            if (context.MessageName == "Create")
            {
                if (!target.Contains(Quantity) || !target.Contains(ItemId) || !target.Contains(TransactionType))
                {
                    return null;
                }

                Add(changes, target);
                return changes;
            }

            if (context.MessageName != "Update")
            {
                return null;
            }

            var touchesStock = false;
            foreach (var attribute in TrackedAttributes)
            {
                touchesStock |= target.Contains(attribute);
            }

            if (!touchesStock)
            {
                return null;
            }

            var before = GetPreImage(context, service, target.Id);
            var after = new Entity(TransactionEntity, target.Id);
            foreach (var attribute in TrackedAttributes)
            {
                after[attribute] = target.Contains(attribute) ? target[attribute] : before.GetAttributeValue<object>(attribute);
            }

            Add(changes, before, -1);
            Add(changes, after);
            return changes;
        }

        public static int GetAvailable(IOrganizationService service, Guid itemId)
        {
            var item = service.Retrieve(ItemEntity, itemId, new ColumnSet(AvailableQuantity));
            return item.GetAttributeValue<int?>(AvailableQuantity) ?? 0;
        }

        private static Entity GetPreImage(IPluginExecutionContext context, IOrganizationService service, Guid id)
        {
            if (context.PreEntityImages != null
                && context.PreEntityImages.TryGetValue(PreImageName, out var image)
                && image != null)
            {
                return image;
            }

            // After the save the record already holds the new values, so only earlier stages can read the old ones.
            if (context.Stage >= PostOperationStage)
            {
                throw new InvalidPluginExecutionException(
                    $"The '{PreImageName}' pre-image is missing from the {context.MessageName} step, so the stock change can't be calculated.");
            }

            return service.Retrieve(TransactionEntity, id, new ColumnSet(TrackedAttributes));
        }

        private static void Add(Dictionary<Guid, long> changes, Entity transaction, int sign = 1)
        {
            var itemRef = transaction.GetAttributeValue<EntityReference>(ItemId);
            if (itemRef == null)
            {
                return;
            }

            var quantity = transaction.GetAttributeValue<int?>(Quantity) ?? 0;
            long effect;
            switch (transaction.GetAttributeValue<OptionSetValue>(TransactionType)?.Value)
            {
                case Inbound:
                    effect = quantity;
                    break;
                case Outbound:
                    effect = -quantity;
                    break;
                default:
                    return;
            }

            changes.TryGetValue(itemRef.Id, out var current);
            changes[itemRef.Id] = current + sign * effect;
        }
    }
}
