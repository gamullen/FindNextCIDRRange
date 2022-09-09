data "azurerm_resource_group" "source_virtual_network_resource_group" {
  name = var.vnet_rg_name
}

data "azurerm_virtual_network" "source_virtual_network" {
  name                = var.vnet_name
  resource_group_name = data.azurerm_resource_group.source_virtual_network_resource_group.name
}