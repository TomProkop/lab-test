using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using FakeXrmEasy.Plugins;
using Plugins.Warehouse;

namespace Tests.Plugins
{
    [TestClass]
    public class ValidateWarehouseTransactionPluginTests : FakeXrmEasyTestBase
    {
        private const int Inbound = 100000000;
        private const int Outbound = 100000001;

        private Entity SeedItem(int available)
        {
            var item = new Entity("lab_warehouseitem") { Id = Guid.NewGuid() };
            item["lab_availablequantity"] = available;
            _context.Initialize(new[] { item });
            return item;
        }

        private Entity MakeTransaction(Guid itemId, int quantity, int type)
        {
            var transaction = new Entity("lab_warehousetransaction") { Id = Guid.NewGuid() };
            transaction["lab_quantity"] = quantity;
            transaction["lab_itemid"] = new EntityReference("lab_warehouseitem", itemId);
            transaction["lab_transactiontype"] = new OptionSetValue(type);
            return transaction;
        }

        private void Execute(Entity target)
        {
            var pluginContext = _context.GetDefaultPluginContext();
            pluginContext.MessageName = "Create";
            pluginContext.PrimaryEntityName = "lab_warehousetransaction";
            pluginContext.InputParameters["Target"] = target;

            var plugin = new ValidateWarehouseTransactionPlugin(string.Empty, string.Empty);
            _context.ExecutePluginWith(pluginContext, plugin);
        }

        [TestMethod]
        public void Outbound_With_Enough_Stock_Does_Not_Throw()
        {
            var item = SeedItem(available: 10);

            Execute(MakeTransaction(item.Id, quantity: 3, Outbound));

            var reloaded = _service.Retrieve("lab_warehouseitem", item.Id, new ColumnSet("lab_availablequantity"));
            Assert.AreEqual(10, reloaded.GetAttributeValue<int>("lab_availablequantity"));
        }

        [TestMethod]
        public void Outbound_With_Too_High_Quantity_Throws()
        {
            var item = SeedItem(available: 5);

            var ex = Assert.ThrowsExactly<InvalidPluginExecutionException>(
                () => Execute(MakeTransaction(item.Id, quantity: 10, Outbound)));

            StringAssert.Contains(ex.Message, "Not enough product in stock");
            StringAssert.Contains(ex.Message, "Available: 5");
            StringAssert.Contains(ex.Message, "requested: 10");
        }

        [TestMethod]
        public void Inbound_Is_Not_Validated()
        {
            var item = SeedItem(available: 0);

            // Inbound transactions add stock, so quantity above availability is fine.
            Execute(MakeTransaction(item.Id, quantity: 100, Inbound));
        }

        private void ExecuteUpdate(Entity target, Entity preImage)
        {
            var pluginContext = _context.GetDefaultPluginContext();
            pluginContext.MessageName = "Update";
            pluginContext.PrimaryEntityName = "lab_warehousetransaction";
            pluginContext.InputParameters["Target"] = target;
            pluginContext.PreEntityImages["PreImage"] = preImage;

            var plugin = new ValidateWarehouseTransactionPlugin(string.Empty, string.Empty);
            _context.ExecutePluginWith(pluginContext, plugin);
        }

        private static Entity Change(Entity existing, string attribute, object value)
        {
            var target = new Entity("lab_warehousetransaction") { Id = existing.Id };
            target[attribute] = value;
            return target;
        }

        [TestMethod]
        public void Update_Outbound_Increase_Within_Stock_Does_Not_Throw()
        {
            // 5 already taken, 2 left: going to 6 needs only 1 more.
            var item = SeedItem(available: 2);
            var existing = MakeTransaction(item.Id, quantity: 5, Outbound);

            ExecuteUpdate(Change(existing, "lab_quantity", 6), existing);
        }

        [TestMethod]
        public void Update_Outbound_Increase_Beyond_Stock_Throws()
        {
            var item = SeedItem(available: 2);
            var existing = MakeTransaction(item.Id, quantity: 5, Outbound);

            var ex = Assert.ThrowsExactly<InvalidPluginExecutionException>(
                () => ExecuteUpdate(Change(existing, "lab_quantity", 8), existing));

            StringAssert.Contains(ex.Message, "Available: 2");
            StringAssert.Contains(ex.Message, "requested: 3");
        }

        [TestMethod]
        public void Update_Outbound_Decrease_Does_Not_Throw()
        {
            var item = SeedItem(available: 0);
            var existing = MakeTransaction(item.Id, quantity: 5, Outbound);

            ExecuteUpdate(Change(existing, "lab_quantity", 1), existing);
        }

        [TestMethod]
        public void Update_Inbound_To_Outbound_Beyond_Stock_Throws()
        {
            // The inbound 4 is already in the 5 available: undoing it leaves 1, outbound 4 needs 8 in total.
            var item = SeedItem(available: 5);
            var existing = MakeTransaction(item.Id, quantity: 4, Inbound);

            var ex = Assert.ThrowsExactly<InvalidPluginExecutionException>(
                () => ExecuteUpdate(Change(existing, "lab_transactiontype", new OptionSetValue(Outbound)), existing));

            StringAssert.Contains(ex.Message, "requested: 8");
        }

        [TestMethod]
        public void Update_Moving_Outbound_To_Item_Without_Stock_Throws()
        {
            var oldItem = SeedItem(available: 0);
            var newItem = new Entity("lab_warehouseitem") { Id = Guid.NewGuid() };
            newItem["lab_availablequantity"] = 1;
            _service.Create(newItem);
            var existing = MakeTransaction(oldItem.Id, quantity: 3, Outbound);

            Assert.ThrowsExactly<InvalidPluginExecutionException>(
                () => ExecuteUpdate(Change(existing, "lab_itemid", new EntityReference("lab_warehouseitem", newItem.Id)), existing));
        }

        [TestMethod]
        public void Update_Without_Stock_Fields_Is_Not_Validated()
        {
            var item = SeedItem(available: 0);
            var existing = MakeTransaction(item.Id, quantity: 5, Outbound);

            ExecuteUpdate(Change(existing, "lab_name", "Renamed"), existing);
        }

        [TestMethod]
        public void Update_Without_PreImage_Reads_The_Record()
        {
            var item = SeedItem(available: 2);
            var existing = MakeTransaction(item.Id, quantity: 5, Outbound);
            _service.Create(existing);

            var pluginContext = _context.GetDefaultPluginContext();
            pluginContext.MessageName = "Update";
            pluginContext.PrimaryEntityName = "lab_warehousetransaction";
            pluginContext.InputParameters["Target"] = Change(existing, "lab_quantity", 6);

            _context.ExecutePluginWith(pluginContext, new ValidateWarehouseTransactionPlugin(string.Empty, string.Empty));
        }
    }
}
