module "plan" {
  source = "registry.terraform.io/libre-devops/service-plan/azurerm"

  rg_name  = module.rg.rg_name
  location = module.rg.rg_location
  tags     = module.rg.rg_tags

  app_service_plan_name          = "asp-${random_string.random.result}"
  add_to_app_service_environment = false

  os_type  = "Linux"
  sku_name = "Y1"
}

#checkov:skip=CKV2_AZURE_145:TLS 1.2 is allegedly the latest supported as per hashicorp docs
module "fnc_app" {
  source = "registry.terraform.io/libre-devops/linux-function-app/azurerm"

  rg_name  = module.rg.rg_name
  location = module.rg.rg_location
  tags     = module.rg.rg_tags

  app_name        = "fnc-${random_string.random.result}"
  service_plan_id = module.plan.service_plan_id

  app_settings = {
    FUNCTIONS_WORKER_RUNTIME = "dotnet"
  }

  storage_account_name          = module.sa.sa_name
  storage_account_access_key    = module.sa.sa_primary_access_key
  storage_uses_managed_identity = "false"

  identity_type               = "SystemAssigned"
  functions_extension_version = "~4"

  settings = {
    site_config = {
      minimum_tls_version = "1.2"
      http2_enabled       = true

      application_stack = {
        dotnet_version = "8.0"
      }
    }

    auth_settings = {
      enabled                       = false
      runtime_version               = "~1"
      unauthenticated_client_action = "AllowAnonymous"
    }
  }
}

# Needed for app to have access to the vnet for reading
resource "azurerm_role_assignment" "id_reader" {
  principal_id         = module.fnc_app.fnc_identity.0.principal_id
  scope                = data.azurerm_virtual_network.source_virtual_network.id
  role_definition_name = "Reader"
}