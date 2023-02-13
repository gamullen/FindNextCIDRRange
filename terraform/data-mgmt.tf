data "azurerm_client_config" "current_creds" {}

data "azurerm_management_group" "current_mgmt_group" {
  display_name = "Tenant Root Group"
}