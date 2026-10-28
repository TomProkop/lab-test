using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using FakeXrmEasy.Plugins;
using Plugins.Warehouse;

namespace Tests.Plugins
{
    [TestClass]
    public class SubtractQuantityPluginTests : FakeXrmEasyTestBase
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

        private void Execute(Guid itemId, int quantity, int type)
        {
            var transaction = new Entity("lab_warehousetransaction") { Id = Guid.NewGuid() };
            transaction["lab_quantity"] = quantity;
            transaction["lab_itemid"] = new EntityReference("lab_warehouseitem", itemId);
            transaction["lab_transactiontype"] = new OptionSetValue(type);

            var pluginContext = _context.GetDefaultPluginContext();
            pluginContext.MessageName = "Create";
            pluginContext.PrimaryEntityName = "lab_warehousetransaction";
            pluginContext.InputParameters["Target"] = transaction;

            var plugin = new SubtractQuantityPlugin(string.Empty, string.Empty);
            _context.ExecutePluginWith(pluginContext, plugin);
        }

        private int AvailableQuantity(Guid itemId) =>
            _service.Retrieve("lab_warehouseitem", itemId, new ColumnSet("lab_availablequantity"))
                .GetAttributeValue<int>("lab_availablequantity");

        [TestMethod]
        public void Outbound_Subtracts_Quantity_From_Item()
        {
            var item = SeedItem(available: 10);

            Execute(item.Id, quantity: 3, Outbound);

            Assert.AreEqual(7, AvailableQuantity(item.Id));
        }

        [TestMethod]
        public void Inbound_Adds_Quantity_To_Item()
        {
            var item = SeedItem(available: 10);

            Execute(item.Id, quantity: 5, Inbound);

            Assert.AreEqual(15, AvailableQuantity(item.Id));
        }

        private Entity MakeTransaction(Guid itemId, int quantity, int type)
        {
            var transaction = new Entity("lab_warehousetransaction") { Id = Guid.NewGuid() };
            transaction["lab_quantity"] = quantity;
            transaction["lab_itemid"] = new EntityReference("lab_warehouseitem", itemId);
            transaction["lab_transactiontype"] = new OptionSetValue(type);
            return transaction;
        }

        private void ExecuteUpdate(Entity existing, string attribute, object value)
        {
            var target = new Entity("lab_warehousetransaction") { Id = existing.Id };
            target[attribute] = value;

            var pluginContext = _context.GetDefaultPluginContext();
            pluginContext.MessageName = "Update";
            pluginContext.PrimaryEntityName = "lab_warehousetransaction";
            pluginContext.InputParameters["Target"] = target;
            pluginContext.PreEntityImages["PreImage"] = existing;

            _context.ExecutePluginWith(pluginContext, new SubtractQuantityPlugin(string.Empty, string.Empty));
        }

        [TestMethod]
        public void Update_Outbound_Increase_Subtracts_The_Difference()
        {
            var item = SeedItem(available: 2);

            ExecuteUpdate(MakeTransaction(item.Id, 5, Outbound), "lab_quantity", 6);

            Assert.AreEqual(1, AvailableQuantity(item.Id));
        }

        [TestMethod]
        public void Update_Outbound_Decrease_Returns_The_Difference()
        {
            var item = SeedItem(available: 2);

            ExecuteUpdate(MakeTransaction(item.Id, 5, Outbound), "lab_quantity", 1);

            Assert.AreEqual(6, AvailableQuantity(item.Id));
        }

        [TestMethod]
        public void Update_Inbound_To_Outbound_Reverses_And_Subtracts()
        {
            var item = SeedItem(available: 10);

            ExecuteUpdate(MakeTransaction(item.Id, 4, Inbound), "lab_transactiontype", new OptionSetValue(Outbound));

            Assert.AreEqual(2, AvailableQuantity(item.Id));
        }

        [TestMethod]
        public void Update_Item_Moves_Stock_Between_Items()
        {
            var oldItem = SeedItem(available: 2);
            var newItem = new Entity("lab_warehouseitem") { Id = Guid.NewGuid() };
            newItem["lab_availablequantity"] = 10;
            _service.Create(newItem);

            ExecuteUpdate(MakeTransaction(oldItem.Id, 3, Outbound), "lab_itemid", new EntityReference("lab_warehouseitem", newItem.Id));

            Assert.AreEqual(5, AvailableQuantity(oldItem.Id));
            Assert.AreEqual(7, AvailableQuantity(newItem.Id));
        }

        [TestMethod]
        public void Update_Without_Stock_Fields_Leaves_Stock_Alone()
        {
            var item = SeedItem(available: 2);

            ExecuteUpdate(MakeTransaction(item.Id, 5, Outbound), "lab_name", "Renamed");

            Assert.AreEqual(2, AvailableQuantity(item.Id));
        }
    }
}
