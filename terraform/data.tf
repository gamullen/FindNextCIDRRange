data "http" "user_ip" {
  url = "https://ipv4.icanhazip.com" // If running locally, running this block will fetch your outbound public IP of your home/office/ISP/VPN and add it.  It will add the hosted agent etc if running from Microsoft/GitLab
}

data "azurerm_resource_group" "source_virtual_network_resource_group" {
  name = var.vnet_rg_name
}

data "azurerm_virtual_network" "source_virtual_network" {
  name                = var.vnet_name
  resource_group_name = data.azurerm_resource_group.source_virtual_network_resource_group.name
}