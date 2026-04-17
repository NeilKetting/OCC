-- Disable stock tracking (low stock alerts) for all inventory items
UPDATE InventoryItems
SET TrackLowStock = 0;
